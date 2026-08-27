using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.GraphEditor
{
    /// <summary>
    /// Bakes a conversation graph into its bound <see cref="WitWeaverConversationData"/>: the
    /// YAML section is rewritten in the baked order — <b>preserving each existing line's text and
    /// character untouched</b> (the YAML owns them; the graph only displays them) and appending
    /// empty stubs for canvas-created lines — then reimported, and topology is written into each
    /// line's <c>LineContinuationSettings</c>. Per-line data that is not graph-authored
    /// (representations, actions, audio, progression) is preserved across the reimport by LineID.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1GraphEditor_1_1WitWeaverGraphBake.html")]
    internal static class WitWeaverGraphBake
    {
        /// <summary>Bake-relevant extraction of the graph: deterministic line order + diagnostics.</summary>
        internal sealed class BakeModel
        {
            public readonly List<DialogueLineNode> Order = new();
            public readonly List<string> Warnings = new();
            public readonly List<string> Errors = new();
            public string ContentHash;
        }

        /// <summary>
        /// Builds the deterministic bake order (DFS from Start, choice blocks in block order,
        /// unreachable lines appended by canvas position) and a content hash for dirty checks.
        /// </summary>
        public static BakeModel BuildModel(WitWeaverConversationGraph graph)
        {
            var model = new BakeModel();
            var nodes = graph.GetNodes().ToList();

            var starts = nodes.OfType<ConversationStartNode>().ToList();
            if (starts.Count == 0) model.Errors.Add("Graph has no Conversation Start node.");
            if (starts.Count > 1) model.Errors.Add("Graph has more than one Conversation Start node.");

            var lineNodes = nodes.OfType<DialogueLineNode>().ToList();
            var seenIds = new HashSet<string>();
            foreach (var line in lineNodes)
            {
                line.EnsureLineId();
                if (!seenIds.Add(line.LineId))
                    model.Errors.Add($"Duplicate LineID '{line.LineId}'.");
            }

            // Every YAML line must have a node: a node deleted on the canvas would otherwise
            // silently drop its line from the YAML on bake. Refresh From YAML restores nodes.
            var yamlConfigs = WitWeaverGraphSync.GetYamlLineConfigs(graph.Conversation);
            if (yamlConfigs != null)
            {
                int missingNodes = yamlConfigs.Count(c =>
                    c != null && !string.IsNullOrEmpty(c.LineID) && !seenIds.Contains(c.LineID));
                if (missingNodes > 0)
                    model.Errors.Add(
                        $"{missingNodes} dialogue line(s) in the YAML have no node (deleted from the canvas). " +
                        "Run 'Refresh Graph From YAML' to restore them; to remove a line, delete it from the " +
                        "YAML source first.");
            }

            foreach (var choice in nodes.OfType<PlayerChoiceNode>())
            {
                if (choice.BlockCount == 0)
                    model.Warnings.Add("A Player Choice node has no choice blocks.");
            }

            if (model.Errors.Count > 0)
                return model;

            // DFS from Start; explore each node's successors first-target-first.
            var visited = new HashSet<DialogueLineNode>();
            var stack = new Stack<Node>();
            if (starts.Count == 1)
            {
                var first = GetSingleTarget(starts[0], WitWeaverGraphSchema.NextPort, model);
                if (first != null) stack.Push(first);
            }

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                switch (node)
                {
                    case DialogueLineNode line:
                        if (!visited.Add(line)) break;
                        model.Order.Add(line);
                        var next = GetSingleTarget(line, WitWeaverGraphSchema.NextPort, model);
                        if (next != null) stack.Push(next);
                        break;

                    case PlayerChoiceNode choice:
                        // Push block targets in reverse so the first block is explored first.
                        var targets = new List<Node>();
                        foreach (var blockNode in choice.BlockNodes)
                            if (blockNode is ChoiceOptionBlock block)
                            {
                                var target = GetSingleTarget(block, WitWeaverGraphSchema.TargetPort, model);
                                if (target != null) targets.Add(target);
                            }
                        for (int i = targets.Count - 1; i >= 0; i--)
                            stack.Push(targets[i]);
                        break;

                    // ContainerBranchNode / EndConversationNode: control leaves the conversation.
                }
            }

            var unreachable = lineNodes.Where(l => !visited.Contains(l))
                .OrderBy(l => l.Position.y).ThenBy(l => l.Position.x).ToList();
            if (unreachable.Count > 0)
            {
                model.Warnings.Add(
                    $"{unreachable.Count} dialogue line(s) are not reachable from Start; they are appended at the end.");
                model.Order.AddRange(unreachable);
            }

            model.ContentHash = ComputeContentHash(model.Order);
            return model;
        }

        /// <summary>True when the graph's bake-relevant content differs from its last bake.</summary>
        public static bool IsDirty(WitWeaverConversationGraph graph)
        {
            if (string.IsNullOrEmpty(graph.LastBakedGraphHash)) return graph.NodeCount > 0;
            var model = BuildModel(graph);
            return model.Errors.Count > 0 || model.ContentHash != graph.LastBakedGraphHash;
        }

        /// <summary>
        /// Runs the full bake. Returns false (with <paramref name="report"/> explaining why)
        /// when validation fails or a pipeline step cannot complete.
        /// </summary>
        public static bool Bake(WitWeaverConversationGraph graph, WitWeaverConversationData data, out string report)
        {
            if (graph == null || data == null) { report = "Graph or conversation is missing."; return false; }
            if (string.IsNullOrEmpty(data.ConversationKey))
            {
                report = $"Conversation '{data.name}' has no ConversationKey — set one before baking.";
                return false;
            }

            // Stale gate: when the YAML changed after the last sync (or its section cannot be
            // found), baking is refused unconditionally — the user must Refresh From YAML and
            // review first. There is no override; YAML is text truth.
            if (WitWeaverGraphSync.IsYamlStale(graph, data, out bool sectionMissing))
            {
                report = sectionMissing
                    ? $"Bake refused: the source YAML section for key '{data.ConversationKey}' was not found " +
                      "(file missing, unparseable, or the key was removed). Fix the YAML link before baking."
                    : "Bake refused: the YAML changed outside this graph since the last sync. Run " +
                      "'Refresh Graph From YAML', review the result, then bake.";
                return false;
            }

            var model = BuildModel(graph);
            if (model.Errors.Count > 0)
            {
                report = "Bake refused:\n- " + string.Join("\n- ", model.Errors);
                return false;
            }

            // 1) Reorder the YAML section to the baked order. Existing lines keep their YAML
            //    text/character verbatim (the graph never authors text — this also protects
            //    newer YAML edits from being clobbered by a stale graph). Canvas-created lines
            //    are appended as empty stubs for the writer to fill in.
            var fullDict = LoadFullYamlDict(data);
            var existingById = new Dictionary<string, DialogueYamlConfig>();
            if (fullDict.TryGetValue(data.ConversationKey, out var existingConfigs) && existingConfigs != null)
                foreach (var config in existingConfigs)
                    if (config != null && !string.IsNullOrEmpty(config.LineID))
                        existingById[config.LineID] = config;

            int stubCount = 0;
            var configs = new List<DialogueYamlConfig>(model.Order.Count);
            foreach (var node in model.Order)
            {
                if (existingById.TryGetValue(node.LineId, out var existing))
                {
                    configs.Add(existing);
                }
                else
                {
                    configs.Add(BuildStubYamlConfig(node));
                    stubCount++;
                }
            }
            if (stubCount > 0)
                model.Warnings.Add(
                    $"{stubCount} new line(s) were added to the YAML with empty text — write their dialogue " +
                    "in the YAML source, then Refresh Graph From YAML.");

            fullDict[data.ConversationKey] = configs;
            var newYamlText = WitWeaverYamlSerializer.Serialize(fullDict);

            WriteYaml(data, newYamlText, model.Warnings);

            // 2) Rebuild DialogueLines from the new YAML (preserves non-graph data by LineID).
            Undo.RecordObject(data, "Bake Conversation Graph");
            data.WitWeaverYamlUtilities.ImportFromYamlForKey(data.ConversationKey);

            // Sanity: every baked line must now exist on the asset.
            var lineByIndex = new Dictionary<string, int>();
            for (int i = 0; i < model.Order.Count; i++)
                lineByIndex[model.Order[i].LineId] = i;

            var missing = model.Order.Where(n => data.GetLineIndexById(n.LineId) < 0).ToList();
            if (missing.Count > 0)
            {
                report = "Bake aborted after YAML import: " +
                         $"{missing.Count} line(s) missing from the conversation (YAML import failed?). " +
                         "Check the Console for YAML errors.";
                return false;
            }

            // 3) Topology → LineContinuationSettings.
            for (int i = 0; i < model.Order.Count; i++)
            {
                var node = model.Order[i];
                var line = data.DialogueLines[data.GetLineIndexById(node.LineId)];
                line.LineContinuationSettings = BuildContinuation(node, i, model, lineByIndex);
            }

            data.ValidateAndFixDialogueLines();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            // 4) Mark the graph as in-sync: first the section hash of the YAML just written,
            //    then the graph content hash — an immediate re-bake must be clean and gate-free.
            graph.LastSyncedYamlHash = WitWeaverGraphSync.ComputeYamlSectionHash(data);
            graph.LastBakedGraphHash = model.ContentHash;
            GraphDatabase.SaveGraph(graph);

            report = $"Baked {model.Order.Count} line(s) into '{data.name}'.";
            if (model.Warnings.Count > 0)
                report += "\nWarnings:\n- " + string.Join("\n- ", model.Warnings);
            return true;
        }

        // ------------------------------------------------------------------
        // Extraction helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// YAML entry for a canvas-created line that does not exist in the YAML yet: empty text
        /// per configured language, ready for the writer to fill in.
        /// </summary>
        private static DialogueYamlConfig BuildStubYamlConfig(DialogueLineNode node)
        {
            var config = new DialogueYamlConfig
            {
                CharacterID = node.CharacterId ?? "",
                LineID = node.LineId,
                LocalizedDialogue = new Dictionary<string, string>()
            };
            foreach (var language in WitWeaverGraphSchema.GetLanguages())
                config.LocalizedDialogue[language] = node.GetText(language);
            return config;
        }

        private static WitWeaverConversationData.LineContinuation BuildContinuation(
            DialogueLineNode node, int bakedIndex, BakeModel model, Dictionary<string, int> lineByIndex)
        {
            var target = GetSingleTarget(node, WitWeaverGraphSchema.NextPort, model);
            switch (target)
            {
                case null:
                    // Sequential default: an unwired line continues to the next line in bake
                    // order. Ending mid-flow requires explicitly wiring an End node. Only the
                    // final line falls back to ending the conversation.
                    return new WitWeaverConversationData.LineContinuation
                    {
                        Mode = bakedIndex + 1 < lineByIndex.Count
                            ? WitWeaverConversationData.LineContinuationMode.Continue
                            : WitWeaverConversationData.LineContinuationMode.EndConversation
                    };

                case EndConversationNode:
                    return new WitWeaverConversationData.LineContinuation
                    {
                        Mode = WitWeaverConversationData.LineContinuationMode.EndConversation
                    };

                case DialogueLineNode nextLine:
                    // Adjacent in baked order stays a plain Continue — keeps assets close to
                    // hand-authored ones and reverse/history working on the linear spine.
                    if (lineByIndex.TryGetValue(nextLine.LineId, out var idx) && idx == bakedIndex + 1)
                        return new WitWeaverConversationData.LineContinuation
                        {
                            Mode = WitWeaverConversationData.LineContinuationMode.Continue
                        };
                    return new WitWeaverConversationData.LineContinuation
                    {
                        Mode = WitWeaverConversationData.LineContinuationMode.GoToLine,
                        TargetLineID = nextLine.LineId
                    };

                case ContainerBranchNode branch:
                    return new WitWeaverConversationData.LineContinuation
                    {
                        Mode = WitWeaverConversationData.LineContinuationMode.ContainerBranch,
                        TargetContainer = GetPortValue<ConversationContainer>(branch, WitWeaverGraphSchema.ContainerPort),
                        TargetAliasOrName = GetStringPort(branch, WitWeaverGraphSchema.AliasOrNamePort),
                        PushReturnPoint = GetPortValue<bool>(branch, WitWeaverGraphSchema.PushReturnPort)
                    };

                case PlayerChoiceNode choice:
                    return BuildChoiceContinuation(choice, model);

                default:
                    model.Warnings.Add($"Line '{node.LineId}' connects to unsupported node '{target.GetType().Name}'; baked as End.");
                    return new WitWeaverConversationData.LineContinuation
                    {
                        Mode = WitWeaverConversationData.LineContinuationMode.EndConversation
                    };
            }
        }

        private static WitWeaverConversationData.LineContinuation BuildChoiceContinuation(
            PlayerChoiceNode choice, BakeModel model)
        {
            var choices = new List<WitWeaverConversationData.ChoiceOption>();
            foreach (var blockNode in choice.BlockNodes)
            {
                if (blockNode is not ChoiceOptionBlock block) continue;

                var option = new WitWeaverConversationData.ChoiceOption
                {
                    Labels = new List<WitWeaverConversationData.LocalizedDialogue>(),
                    PushReturnPoint = GetPortValue<bool>(block, WitWeaverGraphSchema.PushReturnPort)
                };
                foreach (var language in WitWeaverGraphSchema.GetLanguages())
                    option.Labels.Add(new WitWeaverConversationData.LocalizedDialogue
                    {
                        Language = language,
                        Text = GetStringPort(block, WitWeaverGraphSchema.LabelPortName(language))
                    });

                var target = GetSingleTarget(block, WitWeaverGraphSchema.TargetPort, model);
                switch (target)
                {
                    case DialogueLineNode targetLine:
                        option.TargetLineID = targetLine.LineId;
                        break;
                    case ContainerBranchNode branch:
                        option.TargetContainer = GetPortValue<ConversationContainer>(branch, WitWeaverGraphSchema.ContainerPort);
                        option.TargetAliasOrName = GetStringPort(branch, WitWeaverGraphSchema.AliasOrNamePort);
                        break;
                    case EndConversationNode:
                    case null:
                        // Empty target = the runtime ends the conversation when picked.
                        break;
                    default:
                        model.Warnings.Add($"A choice targets unsupported node '{target.GetType().Name}'; it will end the conversation.");
                        break;
                }
                choices.Add(option);
            }

            return new WitWeaverConversationData.LineContinuation
            {
                Mode = WitWeaverConversationData.LineContinuationMode.PlayerChoice,
                AllowGoBack = GetPortValue<bool>(choice, WitWeaverGraphSchema.AllowGoBackPort),
                Choices = choices
            };
        }

        /// <summary>
        /// Resolves the node connected to an output port, warning when more than one edge exists
        /// (only the first is baked).
        /// </summary>
        private static Node GetSingleTarget(Node node, string outputPortName, BakeModel model)
        {
            var port = node.GetOutputPortByName(outputPortName);
            if (port == null) return null;

            var connected = new List<IPort>();
            port.GetConnectedPorts(connected);
            if (connected.Count > 1)
                model.Warnings.Add(
                    $"'{node.GetType().Name}' port '{outputPortName}' has {connected.Count} connections; only the first is baked.");

            return connected.Count > 0 ? connected[0].GetNode() as Node : null;
        }

        private static string GetStringPort(Node node, string portName) =>
            GetPortValue<string>(node, portName) ?? "";

        private static T GetPortValue<T>(Node node, string portName)
        {
            var port = node.GetInputPortByName(portName);
            if (port != null && port.TryGetValue<T>(out var value))
                return value;
            return default;
        }

        private static string ComputeContentHash(List<DialogueLineNode> order)
        {
            var sb = new StringBuilder();
            var throwaway = new BakeModel();
            var lineByIndex = new Dictionary<string, int>();
            for (int i = 0; i < order.Count; i++)
                lineByIndex[order[i].LineId] = i;

            for (int i = 0; i < order.Count; i++)
            {
                var node = order[i];
                // Text/character are intentionally NOT hashed: they are read-only YAML
                // projections, and a Refresh From YAML must not flag the graph as unbaked.
                sb.Append(node.LineId).Append('|');

                var cont = BuildContinuation(node, i, throwaway, lineByIndex);
                sb.Append((int)cont.Mode).Append('|')
                  .Append(cont.TargetLineID).Append('|')
                  .Append(cont.TargetContainer != null ? cont.TargetContainer.name : "").Append('|')
                  .Append(cont.TargetAliasOrName).Append('|')
                  .Append(cont.PushReturnPoint).Append('|')
                  .Append(cont.AllowGoBack).Append('|');
                if (cont.Choices != null)
                    foreach (var c in cont.Choices)
                    {
                        sb.Append("c:").Append(c.TargetLineID).Append(',')
                          .Append(c.TargetContainer != null ? c.TargetContainer.name : "").Append(',')
                          .Append(c.TargetAliasOrName).Append(',')
                          .Append(c.PushReturnPoint).Append(',');
                        if (c.Labels != null)
                            foreach (var l in c.Labels)
                                sb.Append(l.Language).Append('=').Append(l.Text).Append(';');
                        sb.Append('|');
                    }
                sb.Append('\n');
            }

            using var sha = SHA1.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        // ------------------------------------------------------------------
        // YAML pipeline
        // ------------------------------------------------------------------

        private static Dictionary<string, List<DialogueYamlConfig>> LoadFullYamlDict(WitWeaverConversationData data)
        {
            // Same source priority as ImportFromYamlForKey: embedded text first, then linked file.
            string text = null;
            if (data.ConversationYaml != null)
                text = data.ConversationYaml.text;
            else if (!string.IsNullOrEmpty(data.SourceYamlAssetPath) && File.Exists(Path.GetFullPath(data.SourceYamlAssetPath)))
                text = File.ReadAllText(Path.GetFullPath(data.SourceYamlAssetPath));

            if (!string.IsNullOrEmpty(text) &&
                WitWeaverYamlParser.TryParse(text, out var dict, out string _) && dict != null)
                return dict;

            return new Dictionary<string, List<DialogueYamlConfig>>();
        }

        private static void WriteYaml(WitWeaverConversationData data, string yamlText, List<string> warnings)
        {
            // Write the linked source file when it is a writable project asset (never Packages).
            var sourcePath = data.SourceYamlAssetPath;
            if (!string.IsNullOrEmpty(sourcePath) && sourcePath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                try
                {
                    File.WriteAllText(Path.GetFullPath(sourcePath), yamlText);
                    AssetDatabase.ImportAsset(sourcePath);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Could not write YAML source '{sourcePath}': {ex.Message}");
                }
            }
            else if (!string.IsNullOrEmpty(sourcePath))
            {
                warnings.Add($"YAML source '{sourcePath}' is not under Assets/ — only the embedded copy was updated.");
            }

            // Always refresh the embedded copy deterministically (the runtime + reimport read it).
            EmbedYamlText(data, yamlText);
        }

        /// <summary>Replaces the conversation's embedded YAML sub-asset with the given text.</summary>
        private static void EmbedYamlText(WitWeaverConversationData data, string yamlText)
        {
            if (data.ConversationYaml != null && data.ConversationYaml.text == yamlText)
                return;

            var convoAssetPath = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(convoAssetPath))
            {
                Debug.LogError("[WitWeaver] Cannot embed YAML into an unsaved conversation asset.");
                return;
            }

            if (data.ConversationYaml != null)
            {
                UnityEngine.Object.DestroyImmediate(data.ConversationYaml, true);
                data.ConversationYaml = null;
            }

            var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(convoAssetPath);
            if (reps != null)
                foreach (var rep in reps)
                    if (rep is TextAsset { name: "EmbeddedYaml" } stray)
                        UnityEngine.Object.DestroyImmediate(stray, true);

            var embedded = new TextAsset(yamlText) { name = "EmbeddedYaml" };
            AssetDatabase.AddObjectToAsset(embedded, data);
            data.ConversationYaml = embedded;

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
    }
}
