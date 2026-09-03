// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.GraphEditor
{
    /// <summary>
    /// Sync between a <see cref="WitWeaverConversationGraph"/> and its bound
    /// <see cref="WitWeaverConversationData"/>: generating a graph from existing conversation
    /// data (this file) and, in later phases, baking the graph back into the asset.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1GraphEditor_1_1WitWeaverGraphSync.html")]
    internal static class WitWeaverGraphSync
    {
        internal const float ColumnWidth = 380f;
        internal const float RowHeight = 260f;

        /// <summary>
        /// True while sync code is mutating the graph programmatically. The graph's
        /// OnGraphChanged self-heal (which restores deleted line nodes) must stay out of the way
        /// during populate/refresh, or wiping the graph would immediately re-create nodes.
        /// </summary>
        internal static bool IsSyncInProgress { get; private set; }

        // ---- External-mutation stamps ----
        // Bumped by the bridge after every out-of-band mutation of a graph asset. Each loaded
        // graph instance records the stamp it loaded under; a window whose OnGraphChanged sees a
        // newer stamp knows its in-memory model is stale and schedules a reload. This is the
        // safety net for windows the close-and-reopen pass failed to find.
        private static readonly Dictionary<string, int> _externalMutationStamps =
            new(StringComparer.OrdinalIgnoreCase);

        internal static int GetMutationStamp(string graphAssetPath)
        {
            if (string.IsNullOrEmpty(graphAssetPath)) return 0;
            return _externalMutationStamps.TryGetValue(graphAssetPath, out var stamp) ? stamp : 0;
        }

        internal static void BumpMutationStamp(string graphAssetPath)
        {
            if (string.IsNullOrEmpty(graphAssetPath)) return;
            _externalMutationStamps[graphAssetPath] = GetMutationStamp(graphAssetPath) + 1;
        }

        // Parsed YAML section per conversation, cached by content hash so the graph's
        // OnGraphChanged can consult it cheaply on every change.
        private static readonly Dictionary<UnityEngine.EntityId, (string hash, List<DialogueYamlConfig> configs)> _yamlSectionCache = new();

        /// <summary>
        /// The conversation's current YAML line configs (from the embedded copy), or null when
        /// unavailable. Cached by YAML content hash.
        /// </summary>
        public static List<DialogueYamlConfig> GetYamlLineConfigs(WitWeaverConversationData data)
        {
            var text = data != null && data.ConversationYaml != null ? data.ConversationYaml.text : null;
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(data.ConversationKey)) return null;

            var key = data.GetEntityId();
            var hash = ComputeYamlHash(data);
            if (_yamlSectionCache.TryGetValue(key, out var cached) && cached.hash == hash)
                return cached.configs;

            List<DialogueYamlConfig> configs = null;
            if (WitWeaverYamlParser.TryParse(text, out var dict, out string _))
                dict.TryGetValue(data.ConversationKey, out configs);

            _yamlSectionCache[key] = (hash, configs);
            return configs;
        }

        /// <summary>
        /// Rebuilds the graph's nodes and wires from the conversation's dialogue lines and their
        /// <c>LineContinuationSettings</c>. Existing graph content is discarded — this is the
        /// "generate/refresh from conversation" direction, used on first creation.
        /// </summary>
        public static void PopulateFromConversation(WitWeaverConversationGraph graph, WitWeaverConversationData data)
        {
            if (graph == null || data == null) return;

            IsSyncInProgress = true;
            try
            {
                PopulateFromConversationInternal(graph, data);
            }
            finally
            {
                IsSyncInProgress = false;
            }
        }

        private static void PopulateFromConversationInternal(WitWeaverConversationGraph graph, WitWeaverConversationData data)
        {
            foreach (var node in graph.GetNodes().ToList())
                graph.RemoveNode(node);

            var start = new ConversationStartNode();
            graph.AddNode(start);
            start.Position = new Vector2(-ColumnWidth, 0f);

            var lines = data.DialogueLines;
            if (lines == null || lines.Count == 0)
            {
                graph.LastSyncedYamlHash = ComputeYamlSectionHash(data);
                return;
            }

            // Pass 1 — one node per line, indexed by LineID for jump/choice wiring.
            var lineNodes = new Dictionary<string, DialogueLineNode>();
            var nodeByIndex = new DialogueLineNode[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null) continue;

                var node = new DialogueLineNode();
                graph.AddNode(node);
                node.SetLineId(line.LineID);
                node.EnsureLineId();
                node.Position = new Vector2(i * ColumnWidth, 0f);

                node.SetCharacterId(line.characterID);
                node.ClearTexts();
                foreach (var language in WitWeaverGraphSchema.GetLanguages())
                    node.SetText(language, FindLocalizedText(line.LocalizedDialogues, language));

                nodeByIndex[i] = node;
                if (!string.IsNullOrEmpty(node.LineId))
                    lineNodes[node.LineId] = node;
            }

            Connect(graph, start, WitWeaverGraphSchema.NextPort, nodeByIndex[0]);

            // Degenerate-topology repair: every line marked EndConversation is not authorable
            // intent (the conversation would end on line one) — it is the residue of baking a
            // fully unwired graph under the old bake semantics. Restore sequential flow instead,
            // with a single end at the tail.
            int lineCount = 0, endCount = 0;
            foreach (var line in lines)
            {
                if (line == null) continue;
                lineCount++;
                if (line.LineContinuationSettings.Mode == WitWeaverConversationData.LineContinuationMode.EndConversation)
                    endCount++;
            }
            bool repairAllEnd = lineCount > 1 && endCount == lineCount;
            if (repairAllEnd)
                Debug.LogWarning(
                    "[WitWeaver] Graph rebuild: every line was marked 'End Conversation' (residue of an unwired " +
                    "bake) — restoring sequential start-to-end flow instead.", data);

            // Pass 2 — wire continuations.
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var node = nodeByIndex[i];
                if (line == null || node == null) continue;

                var cont = line.LineContinuationSettings;
                if (repairAllEnd)
                    cont.Mode = WitWeaverConversationData.LineContinuationMode.Continue;
                var satellitePosition = new Vector2(i * ColumnWidth, RowHeight);

                switch (cont.Mode)
                {
                    case WitWeaverConversationData.LineContinuationMode.Continue:
                        var nextNode = FindNextLineNode(nodeByIndex, i);
                        if (nextNode != null)
                        {
                            Connect(graph, node, WitWeaverGraphSchema.NextPort, nextNode);
                        }
                        else
                        {
                            // Last line: the runtime loop ends here, so make the end explicit.
                            var end = new EndConversationNode();
                            graph.AddNode(end);
                            end.Position = new Vector2((i + 1) * ColumnWidth, 0f);
                            Connect(graph, node, WitWeaverGraphSchema.NextPort, end);
                        }
                        break;

                    case WitWeaverConversationData.LineContinuationMode.EndConversation:
                        var endNode = new EndConversationNode();
                        graph.AddNode(endNode);
                        endNode.Position = satellitePosition;
                        Connect(graph, node, WitWeaverGraphSchema.NextPort, endNode);
                        break;

                    case WitWeaverConversationData.LineContinuationMode.GoToLine:
                        if (!string.IsNullOrEmpty(cont.TargetLineID) &&
                            lineNodes.TryGetValue(cont.TargetLineID, out var jumpTarget))
                            Connect(graph, node, WitWeaverGraphSchema.NextPort, jumpTarget);
                        else
                            Debug.LogWarning(
                                $"[WitWeaver] Graph sync: GoToLine target '{cont.TargetLineID}' not found in '{data.name}'; left unconnected.");
                        break;

                    case WitWeaverConversationData.LineContinuationMode.ContainerBranch:
                        var branch = CreateContainerBranchNode(graph, satellitePosition,
                            cont.TargetContainer, cont.TargetAliasOrName, cont.PushReturnPoint);
                        Connect(graph, node, WitWeaverGraphSchema.NextPort, branch);
                        break;

                    case WitWeaverConversationData.LineContinuationMode.PlayerChoice:
                        WirePlayerChoice(graph, data, node, cont, lineNodes, satellitePosition);
                        break;
                }
            }

            graph.LastSyncedYamlHash = ComputeYamlSectionHash(data);
        }

        private static void WirePlayerChoice(
            WitWeaverConversationGraph graph,
            WitWeaverConversationData data,
            DialogueLineNode sourceLine,
            WitWeaverConversationData.LineContinuation cont,
            Dictionary<string, DialogueLineNode> lineNodes,
            Vector2 position)
        {
            var choiceNode = new PlayerChoiceNode();
            graph.AddNode(choiceNode);
            choiceNode.Position = position;
            SetPortValue(choiceNode, WitWeaverGraphSchema.AllowGoBackPort, cont.AllowGoBack);
            Connect(graph, sourceLine, WitWeaverGraphSchema.NextPort, choiceNode);

            if (cont.Choices == null) return;

            for (int j = 0; j < cont.Choices.Count; j++)
            {
                var choice = cont.Choices[j];
                var block = new ChoiceOptionBlock();
                choiceNode.AddBlockNode(block);

                foreach (var language in WitWeaverGraphSchema.GetLanguages())
                    SetPortValue(block, WitWeaverGraphSchema.LabelPortName(language),
                        FindLocalizedText(choice.Labels, language));
                SetPortValue(block, WitWeaverGraphSchema.PushReturnPort, choice.PushReturnPoint);

                var targetPort = block.GetOutputPortByName(WitWeaverGraphSchema.TargetPort);
                if (targetPort == null) continue;

                if (!string.IsNullOrEmpty(choice.TargetLineID) &&
                    lineNodes.TryGetValue(choice.TargetLineID, out var targetLine))
                {
                    var inPort = targetLine.GetInputPortByName(WitWeaverGraphSchema.InPort);
                    if (inPort != null) graph.Connect(targetPort, inPort);
                }
                else if (choice.TargetContainer != null)
                {
                    // The choice's own PushReturnPoint lives on the block; the satellite
                    // container node keeps its push flag off to avoid double-pushing.
                    var branch = CreateContainerBranchNode(graph,
                        position + new Vector2(0f, RowHeight * (j + 1)),
                        choice.TargetContainer, choice.TargetAliasOrName, pushReturnPoint: false);
                    var inPort = branch.GetInputPortByName(WitWeaverGraphSchema.InPort);
                    if (inPort != null) graph.Connect(targetPort, inPort);
                }
                else if (!string.IsNullOrEmpty(choice.TargetLineID))
                {
                    Debug.LogWarning(
                        $"[WitWeaver] Graph sync: choice target LineID '{choice.TargetLineID}' not found in '{data.name}'; left unconnected.");
                }
            }
        }

        private static ContainerBranchNode CreateContainerBranchNode(
            WitWeaverConversationGraph graph, Vector2 position,
            ConversationContainer container, string aliasOrName, bool pushReturnPoint)
        {
            var branch = new ContainerBranchNode();
            graph.AddNode(branch);
            branch.Position = position;
            SetPortValue(branch, WitWeaverGraphSchema.ContainerPort, container);
            SetPortValue(branch, WitWeaverGraphSchema.AliasOrNamePort, aliasOrName ?? "");
            SetPortValue(branch, WitWeaverGraphSchema.PushReturnPort, pushReturnPoint);
            return branch;
        }

        private static DialogueLineNode FindNextLineNode(DialogueLineNode[] nodeByIndex, int currentIndex)
        {
            for (int i = currentIndex + 1; i < nodeByIndex.Length; i++)
                if (nodeByIndex[i] != null)
                    return nodeByIndex[i];
            return null;
        }

        private static void Connect(WitWeaverConversationGraph graph, Node from, string outputPortName, Node to)
        {
            var output = from?.GetOutputPortByName(outputPortName);
            var input = to?.GetInputPortByName(WitWeaverGraphSchema.InPort);
            if (output != null && input != null)
                graph.Connect(output, input);
        }

        private static void SetPortValue<T>(Node node, string portName, T value)
        {
            var port = node.GetInputPortByName(portName);
            if (port == null || !port.TrySetValue(value))
                Debug.LogWarning($"[WitWeaver] Graph sync: could not set port '{portName}' on {node?.GetType().Name}.");
        }

        private static string FindLocalizedText(
            List<WitWeaverConversationData.LocalizedDialogue> localized, string language)
        {
            if (localized == null) return "";
            for (int i = 0; i < localized.Count; i++)
            {
                if (string.Equals(localized[i].Language, language, StringComparison.OrdinalIgnoreCase))
                    return localized[i].Text ?? "";
            }
            return "";
        }

        /// <summary>
        /// Pulls YAML-side changes into an existing graph without touching topology: updates the
        /// read-only character/text display on nodes matched by LineID, adds nodes for new
        /// LineIDs (placed in a column past the current content, unwired — validation flags them
        /// as unreachable until the user wires them in), and removes nodes whose line was removed
        /// from the YAML (the YAML owns line lifecycle; nodes cannot be deleted on the canvas).
        /// </summary>
        public static void RefreshFromYaml(WitWeaverConversationGraph graph, WitWeaverConversationData data)
        {
            if (graph == null || data == null) return;

            IsSyncInProgress = true;
            try
            {
                RefreshFromYamlInternal(graph, data);
            }
            finally
            {
                IsSyncInProgress = false;
            }
        }

        /// <summary>
        /// Full rebuild: regenerates every node and wire from the conversation's last-baked
        /// topology (including the all-EndConversation repair), then applies the YAML text pass
        /// on top. Node positions reset to the generated layout.
        /// </summary>
        public static void RebuildFromConversation(WitWeaverConversationGraph graph, WitWeaverConversationData data)
        {
            if (graph == null || data == null) return;

            IsSyncInProgress = true;
            try
            {
                PopulateFromConversationInternal(graph, data);
                RefreshFromYamlInternal(graph, data); // applies YAML text, adds YAML-new lines, saves
            }
            finally
            {
                IsSyncInProgress = false;
            }
        }

        private static void RefreshFromYamlInternal(WitWeaverConversationGraph graph, WitWeaverConversationData data)
        {
            var yamlText = data.ConversationYaml != null ? data.ConversationYaml.text : null;
            if (string.IsNullOrEmpty(yamlText))
            {
                Debug.LogError("[WitWeaver] Refresh From YAML failed: the conversation has no embedded YAML.", data);
                return;
            }
            if (!WitWeaverYamlParser.TryParse(yamlText, out var dict, out string parseError))
            {
                Debug.LogError($"[WitWeaver] Refresh From YAML failed: could not parse YAML. {parseError}", data);
                return;
            }

            if (!dict.TryGetValue(data.ConversationKey ?? "", out var configs) || configs == null)
            {
                Debug.LogError(
                    $"[WitWeaver] Refresh From YAML failed: key '{data.ConversationKey}' not found in YAML.", data);
                return;
            }

            // Structural damage check: a previously-baked line's node was deleted from the
            // canvas, or the Start node is gone. Deleted nodes lose their wires, and partial
            // patching cannot recover them — but the conversation asset still holds the full
            // topology from the last bake, so rebuild the whole graph from it (positions reset),
            // then apply the YAML text pass on top.
            bool startMissing = !graph.GetNodes().OfType<ConversationStartNode>().Any();
            bool deletedLineNodes = false;
            if (data.DialogueLines != null)
            {
                var presentIds = new HashSet<string>();
                foreach (var node in graph.GetNodes())
                    if (node is DialogueLineNode line && !string.IsNullOrEmpty(line.LineId))
                        presentIds.Add(line.LineId);

                foreach (var bakedLine in data.DialogueLines)
                    if (bakedLine != null && !string.IsNullOrEmpty(bakedLine.LineID) &&
                        !presentIds.Contains(bakedLine.LineID))
                    {
                        deletedLineNodes = true;
                        break;
                    }
            }

            if (startMissing || deletedLineNodes)
            {
                Debug.LogWarning(
                    "[WitWeaver] Refresh From YAML: deleted node(s) detected — the graph was rebuilt from the " +
                    "conversation's last-baked topology (node positions were reset). Line nodes cannot be " +
                    "deleted; remove lines from the YAML source instead.", data);
                PopulateFromConversationInternal(graph, data);
            }

            var nodesById = new Dictionary<string, DialogueLineNode>();
            float maxX = 0f;
            foreach (var node in graph.GetNodes())
            {
                if (node is DialogueLineNode line)
                {
                    if (!string.IsNullOrEmpty(line.LineId))
                        nodesById[line.LineId] = line;
                    maxX = Mathf.Max(maxX, line.Position.x);
                }
            }

            int updated = 0, added = 0;
            var yamlIds = new HashSet<string>();
            foreach (var config in configs)
            {
                if (config == null || string.IsNullOrEmpty(config.LineID)) continue;
                yamlIds.Add(config.LineID);

                if (nodesById.TryGetValue(config.LineID, out var node))
                {
                    ApplyYamlConfigToNode(node, config);
                    updated++;
                }
                else
                {
                    var newNode = new DialogueLineNode();
                    graph.AddNode(newNode);
                    newNode.SetLineId(config.LineID);
                    newNode.Position = new Vector2(maxX + ColumnWidth, added * RowHeight);
                    ApplyYamlConfigToNode(newNode, config);
                    added++;
                }
            }

            // Lines removed from the YAML take their nodes with them — the YAML owns line
            // lifecycle, so this is the deletion path (canvas deletion is not allowed).
            int removed = 0;
            foreach (var pair in nodesById)
            {
                if (yamlIds.Contains(pair.Key)) continue;
                graph.RemoveNode(pair.Value);
                removed++;
            }

            graph.LastSyncedYamlHash = ComputeYamlSectionHash(data);
            GraphDatabase.SaveGraph(graph);
            Debug.Log(
                $"[WitWeaver] Refresh From YAML: updated {updated}, added {added}, removed {removed} line node(s)"
                + (added > 0 ? " — wire the new node(s) into the flow before baking." : "."), data);
        }

        /// <summary>Copies a YAML line's character/text into the node's read-only display fields.</summary>
        internal static void ApplyYamlConfigToNode(DialogueLineNode node, DialogueYamlConfig config)
        {
            node.SetCharacterId(config.CharacterID);
            node.ClearTexts();
            foreach (var language in WitWeaverGraphSchema.GetLanguages())
                node.SetText(language, FindYamlText(config, language));
        }

        private static string FindYamlText(DialogueYamlConfig config, string language)
        {
            if (config.LocalizedDialogue == null) return "";
            foreach (var kvp in config.LocalizedDialogue)
                if (string.Equals(kvp.Key, language, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value ?? "";
            return "";
        }

        /// <summary>
        /// Raw-text hash of the conversation's embedded YAML, used only as a cheap
        /// change-detection key for the parsed-section cache. Staleness decisions use
        /// <see cref="ComputeYamlSectionHash"/> instead.
        /// </summary>
        private static string ComputeYamlHash(WitWeaverConversationData data)
        {
            var text = data?.ConversationYaml != null ? data.ConversationYaml.text : "";
            if (string.IsNullOrEmpty(text)) return "";
            using var sha = SHA1.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        /// <summary>
        /// Canonical hash of the conversation's own YAML section: the text is resolved with the
        /// same priority the import pipeline uses (embedded copy first, then the linked source
        /// file), parsed, the section re-serialized alone, and SHA-256 hex hashed. Formatting-only
        /// file changes therefore do not read as stale. Returns null when the YAML is missing,
        /// unparseable, or has no section for the conversation's key.
        /// </summary>
        internal static string ComputeYamlSectionHash(WitWeaverConversationData data)
        {
            if (data == null || string.IsNullOrEmpty(data.ConversationKey)) return null;

            string text = null;
            if (data.ConversationYaml != null)
                text = data.ConversationYaml.text;
            else if (!string.IsNullOrEmpty(data.SourceYamlAssetPath))
            {
                var fullPath = System.IO.Path.GetFullPath(data.SourceYamlAssetPath);
                if (System.IO.File.Exists(fullPath))
                    text = System.IO.File.ReadAllText(fullPath);
            }
            if (string.IsNullOrEmpty(text)) return null;

            if (!WitWeaverYamlParser.TryParse(text, out var dict, out string _) || dict == null)
                return null;
            if (!dict.TryGetValue(data.ConversationKey, out var section) || section == null)
                return null;

            var canonical = WitWeaverYamlSerializer.Serialize(
                new Dictionary<string, List<DialogueYamlConfig>> { [data.ConversationKey] = section });

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        /// <summary>
        /// True when the YAML section changed since the graph last synced (or cannot be found at
        /// all — <paramref name="sectionMissing"/> distinguishes that case). The hash comparison
        /// is the single source of truth; no stale flag is persisted anywhere.
        /// </summary>
        internal static bool IsYamlStale(WitWeaverConversationGraph graph, WitWeaverConversationData data,
            out bool sectionMissing)
        {
            sectionMissing = false;
            if (graph == null || data == null) return false;

            var currentHash = ComputeYamlSectionHash(data);
            if (currentHash == null)
            {
                sectionMissing = true;
                return true;
            }

            // A graph that never synced has nothing to be stale against.
            return !string.IsNullOrEmpty(graph.LastSyncedYamlHash) && graph.LastSyncedYamlHash != currentHash;
        }

        internal static bool IsYamlStale(WitWeaverConversationGraph graph, WitWeaverConversationData data)
            => IsYamlStale(graph, data, out _);
    }
}
