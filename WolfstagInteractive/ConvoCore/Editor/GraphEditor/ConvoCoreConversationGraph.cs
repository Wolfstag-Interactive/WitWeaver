using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.GraphEditor
{
    /// <summary>
    /// Node-graph authoring surface for a single <see cref="ConvoCoreConversationData"/> asset.
    /// The graph owns conversation topology (line order, jumps, choices, branches, endings) and
    /// line text/character authoring; baking writes topology into the conversation asset and text
    /// back to YAML. The graph asset is editor-only — deleting it never breaks a conversation.
    /// </summary>
    [Graph(AssetExtension)]
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1GraphEditor_1_1ConvoCoreConversationGraph.html")]
[Serializable]
    public class ConvoCoreConversationGraph : Graph
    {
        public const string AssetExtension = "ConvoCoreConversationGraph";

        [SerializeField] private ConvoCoreConversationData m_Conversation;
        [SerializeField] private string m_LastSyncedYamlHash;
        [SerializeField] private string m_LastBakedGraphHash;

        public ConvoCoreConversationData Conversation
        {
            get => m_Conversation;
            internal set => m_Conversation = value;
        }

        internal string LastSyncedYamlHash
        {
            get => m_LastSyncedYamlHash;
            set => m_LastSyncedYamlHash = value;
        }

        internal string LastBakedGraphHash
        {
            get => m_LastBakedGraphHash;
            set => m_LastBakedGraphHash = value;
        }

        [NonSerialized] private int _loadedMutationStamp;
        [NonSerialized] private bool _reloadScheduled;

        public override void OnEnable()
        {
            base.OnEnable();
            // Remember which external-mutation generation this instance was loaded under; see
            // the stale-instance check in OnGraphChanged.
            _loadedMutationStamp = ConvoCoreGraphSync.GetMutationStamp(GraphDatabase.GetGraphAssetPath(this));
        }

        public override void OnGraphChanged(GraphLogger graphLogger)
        {
            base.OnGraphChanged(graphLogger);

            // Stale-instance safety net: if the bridge mutated this asset after this instance
            // loaded (refresh/rebuild/bake) and the close-and-reopen pass missed this window,
            // reload it now instead of letting its outdated model overwrite the asset. Detection
            // rides on OnGraphChanged, so it triggers on the first interaction with the window.
            if (!_reloadScheduled && !ConvoCoreGraphSync.IsSyncInProgress)
            {
                var assetPath = GraphDatabase.GetGraphAssetPath(this);
                if (!string.IsNullOrEmpty(assetPath) &&
                    ConvoCoreGraphSync.GetMutationStamp(assetPath) != _loadedMutationStamp)
                {
                    _reloadScheduled = true;
                    graphLogger.LogWarning("This graph changed on disk — the window is reloading.");
                    EditorApplication.delayCall += () => ConvoCoreGraphBridge.ReloadWindowsForExternalChange(assetPath);
                    return;
                }
            }

            var startNodes = new List<ConversationStartNode>();
            var lineNodes = new List<DialogueLineNode>();
            var lineIdsSeen = new Dictionary<string, DialogueLineNode>();

            foreach (var node in GetNodes())
            {
                switch (node)
                {
                    case ConversationStartNode start:
                        startNodes.Add(start);
                        break;

                    case DialogueLineNode line:
                        line.EnsureLineId();
                        lineNodes.Add(line);
                        if (lineIdsSeen.TryGetValue(line.LineId, out _))
                            graphLogger.LogError(
                                $"Duplicate LineID '{line.LineId}'. Each dialogue line must have a unique id.", line);
                        else
                            lineIdsSeen[line.LineId] = line;
                        RefreshLineNodeHeader(line);
                        break;

                    case PlayerChoiceNode choice:
                        if (choice.BlockCount == 0)
                            graphLogger.LogWarning("Player Choice has no choice blocks — add at least one.", choice);
                        var inPort = choice.GetInputPortByName(ConvoCoreGraphSchema.InPort);
                        if (inPort is { IsConnected: false })
                            graphLogger.LogWarning("Player Choice is not fed by a dialogue line.", choice);
                        break;

                    case ContainerBranchNode branch:
                        ValidateContainerBranch(branch, graphLogger);
                        break;
                }
            }

            if (startNodes.Count == 0)
                graphLogger.LogError("Graph has no Conversation Start node.");
            else if (startNodes.Count > 1)
                for (int i = 1; i < startNodes.Count; i++)
                    graphLogger.LogError("Only one Conversation Start node is allowed.", startNodes[i]);

            if (startNodes.Count == 1)
            {
                var next = startNodes[0].GetOutputPortByName(ConvoCoreGraphSchema.NextPort);
                if (next is { IsConnected: false })
                    graphLogger.LogWarning("Conversation Start is not connected to a first line.", startNodes[0]);
            }

            foreach (var line in lineNodes)
            {
                var next = line.GetOutputPortByName(ConvoCoreGraphSchema.NextPort);
                if (next is { IsConnected: false })
                    graphLogger.LogWarning(
                        "Line has no continuation — it will bake as 'continue to the next line in order' " +
                        "(or End Conversation if it is the last line). Wire an End node to end here explicitly.",
                        line);
            }

            if (m_Conversation == null)
                graphLogger.LogWarning(
                    "Graph is not bound to a conversation asset. Create graphs from a conversation's inspector.");
            else
            {
                // Informational alongside the hard gate: bake itself refuses while stale.
                if (ConvoCoreGraphSync.IsYamlStale(this, m_Conversation))
                    graphLogger.LogWarning(
                        "The conversation's YAML changed outside this graph — baking is blocked until you run " +
                        "'Refresh Graph From YAML' (conversation inspector or Assets menu).");

                ReportMissingLineNodes(lineIdsSeen, graphLogger);
            }
        }

        /// <summary>
        /// Line nodes cannot be deleted from the canvas: every line in the conversation's YAML
        /// must have a node. The open Graph window cannot be mutated from outside its own command
        /// stack (the experimental API neither allows changes in OnGraphChanged nor picks up
        /// deferred external ones), so deletion is enforced by reporting: a bake-blocking error
        /// badge here, the matching refusal in <see cref="ConvoCoreGraphBake.BuildModel"/>, and
        /// 'Refresh Graph From YAML' as the restore path. Removing a line for real happens in the
        /// YAML, and Refresh removes its node.
        /// </summary>
        private void ReportMissingLineNodes(Dictionary<string, DialogueLineNode> existingLineNodes, GraphLogger graphLogger)
        {
            if (ConvoCoreGraphSync.IsSyncInProgress) return;

            var configs = ConvoCoreGraphSync.GetYamlLineConfigs(m_Conversation);
            if (configs == null) return;

            int missing = 0;
            foreach (var config in configs)
            {
                if (config == null || string.IsNullOrEmpty(config.LineID)) continue;
                if (!existingLineNodes.ContainsKey(config.LineID))
                    missing++;
            }
            if (missing == 0) return;

            graphLogger.LogError(
                $"{missing} dialogue line node(s) were deleted but their lines still exist in the YAML. " +
                "Line nodes cannot be deleted — baking is blocked. Run 'Refresh Graph From YAML' to restore " +
                "them. To remove a line, delete it from the YAML source first.");
        }

        /// <summary>
        /// Validates a Container Branch node (missing container, Playlist-mode target, unknown
        /// alias) and surfaces the container's contents on the node so the branch boundary is a
        /// window rather than a black box: title = container name, subtitle = mode/entry summary.
        /// </summary>
        private static void ValidateContainerBranch(ContainerBranchNode branch, GraphLogger graphLogger)
        {
            ConversationContainer container = null;
            var containerPort = branch.GetInputPortByName(ConvoCoreGraphSchema.ContainerPort);
            containerPort?.TryGetValue(out container);

            string alias = null;
            branch.GetInputPortByName(ConvoCoreGraphSchema.AliasOrNamePort)?.TryGetValue(out alias);

            if (container == null)
            {
                if (containerPort is { IsConnected: false })
                    graphLogger.LogWarning("Container Branch has no container assigned.", branch);
                branch.Title = "Container Branch";
                branch.Subtitle = string.Empty;
                return;
            }

            if (container.ContainerMode == ConversationContainerMode.Playlist)
                graphLogger.LogWarning(
                    $"'{container.name}' is a Playlist container — playlists can't be branch targets. " +
                    "Use a Selector container, or target its first conversation directly.", branch);

            int entryCount = 0;
            bool aliasFound = string.IsNullOrEmpty(alias);
            if (container.Conversations != null)
            {
                foreach (var entry in container.Conversations)
                {
                    if (entry?.ConversationData == null) continue;
                    entryCount++;
                    if (!aliasFound &&
                        (string.Equals(entry.Alias, alias, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(entry.ConversationData.name, alias, StringComparison.OrdinalIgnoreCase)))
                        aliasFound = true;
                }
            }

            if (!aliasFound)
                graphLogger.LogWarning(
                    $"Alias '{alias}' not found in container '{container.name}'. " +
                    "The container will fall back to its own selection.", branch);

            branch.Title = container.name;
            branch.Subtitle = $"{container.ContainerMode} · {entryCount} entr{(entryCount == 1 ? "y" : "ies")}" +
                              (!string.IsNullOrEmpty(alias) ? $" → {alias}" : "");
        }

        /// <summary>
        /// Keeps the canvas readable: title = speaker, subtitle = text preview, tooltip = full
        /// text. All read-only projections of the YAML — text is not editable on the node.
        /// </summary>
        private static void RefreshLineNodeHeader(DialogueLineNode line)
        {
            line.Title = string.IsNullOrWhiteSpace(line.CharacterId) ? "Dialogue Line" : line.CharacterId;

            var text = line.GetFirstNonEmptyText();
            if (string.IsNullOrWhiteSpace(text))
            {
                line.Subtitle = "(no text yet — write it in the YAML)";
                line.Tooltip = string.Empty;
                return;
            }

            var preview = text.Replace('\n', ' ').Trim();
            line.Subtitle = preview.Length > 40 ? preview.Substring(0, 37) + "..." : preview;
            line.Tooltip = text;
        }
    }
}
