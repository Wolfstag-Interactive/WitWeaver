using System.IO;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.GraphEditor
{
    /// <summary>
    /// Entry points for working with a conversation's graph asset: locating it, creating it
    /// beside the conversation asset, and opening it in the Graph window. The conversation
    /// stores the graph's asset GUID (<see cref="WitWeaverConversationData.GraphAssetGuid"/>);
    /// a project-wide scan repairs that link when it goes stale.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1GraphEditor_1_1WitWeaverGraphBridge.html")]
    public static class WitWeaverGraphBridge
    {
        public static bool HasGraph(WitWeaverConversationData data) => ResolveGraphPath(data) != null;

        /// <summary>
        /// Returns the asset path of the conversation's graph, repairing the stored GUID link if
        /// needed, or null when no graph asset exists for it.
        /// </summary>
        public static string ResolveGraphPath(WitWeaverConversationData data)
        {
            if (data == null) return null;

            if (!string.IsNullOrEmpty(data.GraphAssetGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(data.GraphAssetGuid);
                if (!string.IsNullOrEmpty(path) && path.EndsWith("." + WitWeaverConversationGraph.AssetExtension))
                    return path;
            }

            // Fallback: scan graph assets for one bound to this conversation, then repair the link.
            foreach (var guid in AssetDatabase.FindAssets("glob:\"*." + WitWeaverConversationGraph.AssetExtension + "\""))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var graph = GraphDatabase.LoadGraph<WitWeaverConversationGraph>(path);
                if (graph != null && graph.Conversation == data)
                {
                    data.GraphAssetGuid = guid;
                    EditorUtility.SetDirty(data);
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Loads the conversation's graph, creating and populating a new one beside the
        /// conversation asset when none exists yet.
        /// </summary>
        public static WitWeaverConversationGraph GetOrCreateGraphFor(WitWeaverConversationData data)
        {
            if (data == null) return null;

            var existingPath = ResolveGraphPath(data);
            if (existingPath != null)
                return GraphDatabase.LoadGraph<WitWeaverConversationGraph>(existingPath);

            var conversationPath = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(conversationPath))
            {
                Debug.LogError("[WitWeaver] Cannot create a graph for an unsaved conversation asset.");
                return null;
            }

            var folder = Path.GetDirectoryName(conversationPath)?.Replace('\\', '/') ?? "Assets";
            var graphPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{data.name}.{WitWeaverConversationGraph.AssetExtension}");

            var graph = GraphDatabase.CreateGraph<WitWeaverConversationGraph>(graphPath);
            if (graph == null)
            {
                Debug.LogError($"[WitWeaver] Failed to create graph asset at '{graphPath}'.");
                return null;
            }

            graph.Conversation = data;
            WitWeaverGraphSync.PopulateFromConversation(graph, data);
            GraphDatabase.SaveGraph(graph);

            // Creating a graph switches the conversation to graph authoring: the graph becomes
            // the sole editing surface until the user reverts to linear authoring.
            data.GraphAssetGuid = AssetDatabase.AssetPathToGUID(graphPath);
            data.AuthoringMode = WitWeaverConversationData.ConversationAuthoringMode.Graph;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            Debug.Log($"[WitWeaver] Created conversation graph at '{graphPath}'.");
            return graph;
        }

        /// <summary>
        /// Switches the conversation back to linear-list authoring. The baked lines stay intact.
        /// Optionally deletes the companion graph asset.
        /// </summary>
        public static void ConvertToLinear(WitWeaverConversationData data, bool deleteGraphAsset)
        {
            if (data == null) return;

            data.AuthoringMode = WitWeaverConversationData.ConversationAuthoringMode.LinearList;

            if (deleteGraphAsset)
            {
                var path = ResolveGraphPath(data);
                if (path != null)
                    AssetDatabase.DeleteAsset(path);
                data.GraphAssetGuid = "";
            }

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }

        /// <summary>Opens the conversation's graph in the Graph window, creating it if missing.</summary>
        public static void OpenGraphFor(WitWeaverConversationData data)
        {
            var graph = GetOrCreateGraphFor(data);
            if (graph == null) return;

            var path = GraphDatabase.GetGraphAssetPath(graph);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
                AssetDatabase.OpenAsset(asset);
            else
                Debug.LogError($"[WitWeaver] Could not load graph asset at '{path}'.");
        }

        /// <summary>
        /// True when the conversation's YAML section changed after the graph was last synced —
        /// the graph's view of the lines is stale and baking is blocked until a refresh.
        /// </summary>
        public static bool IsGraphStaleRelativeToYaml(WitWeaverConversationData data)
        {
            var path = ResolveGraphPath(data);
            if (path == null) return false;

            var graph = GraphDatabase.LoadGraph<WitWeaverConversationGraph>(path);
            return graph != null && WitWeaverGraphSync.IsYamlStale(graph, data);
        }

        /// <summary>Pulls YAML text/character changes into the graph's nodes (matched by LineID).</summary>
        public static void RefreshGraphFromYamlFor(WitWeaverConversationData data)
        {
            var path = ResolveGraphPath(data);
            if (path == null)
            {
                Debug.LogWarning($"[WitWeaver] '{data?.name}' has no conversation graph to refresh.");
                return;
            }

            var graph = GraphDatabase.LoadGraph<WitWeaverConversationGraph>(path);
            if (graph == null) return;

            WitWeaverGraphSync.RefreshFromYaml(graph, data);
            ReloadOpenGraphWindows(path);
        }

        /// <summary>
        /// Regenerates the graph entirely from the conversation's last-baked topology plus the
        /// current YAML text — every node and wire is rebuilt (positions reset). This is the
        /// repair action for fragmented or damaged graphs.
        /// </summary>
        public static void RebuildGraphFor(WitWeaverConversationData data, bool interactive = true)
        {
            var path = ResolveGraphPath(data);
            if (path == null)
            {
                Debug.LogWarning($"[WitWeaver] '{data?.name}' has no conversation graph to rebuild.");
                return;
            }

            if (interactive && !EditorUtility.DisplayDialog(
                    "Rebuild Graph From Conversation",
                    $"Rebuild the graph for '{data.name}' from its last-baked flow and current YAML?\n\n" +
                    "All nodes and wires are regenerated (start-to-end flow restored) and node " +
                    "positions reset to the generated layout. Un-baked wiring changes are discarded.",
                    "Rebuild", "Cancel"))
                return;

            var graph = GraphDatabase.LoadGraph<WitWeaverConversationGraph>(path);
            if (graph == null) return;

            WitWeaverGraphSync.RebuildFromConversation(graph, data);
            ReloadOpenGraphWindows(path);
        }

        /// <summary>
        /// Makes any open Graph Toolkit window show the current on-disk state of the graph. The
        /// window keeps a private in-memory model that never observes external changes, and the
        /// experimental API offers no reload entry point (in-place reload via its internals
        /// proved unstable), so the window is closed and reopened. A docked window may reopen
        /// floating — the cost of guaranteeing the canvas matches the asset.
        /// </summary>
        private static void ReloadOpenGraphWindows(string graphPath)
        {
            // Stamp first: any window instance this pass fails to close will notice the newer
            // stamp in its OnGraphChanged and schedule its own reload on first interaction.
            WitWeaverGraphSync.BumpMutationStamp(graphPath);

            if (!CloseOpenGraphWindows(graphPath))
                return;

            var asset = AssetDatabase.LoadMainAssetAtPath(graphPath);
            if (asset != null)
                AssetDatabase.OpenAsset(asset);
        }

        /// <summary>Reload entry for the stale-instance safety net in the graph's OnGraphChanged.</summary>
        internal static void ReloadWindowsForExternalChange(string graphPath)
            => ReloadOpenGraphWindows(graphPath);

        /// <summary>
        /// Closes any Graph Toolkit window currently showing the given graph asset. Returns true
        /// when at least one window was closed. The window type and its backing asset are not
        /// public API, so matching is by window title against the asset name; when no title
        /// matches, ALL Graph Toolkit windows are closed — leaving a stale window open is worse
        /// (its in-memory model would overwrite the changes about to be saved).
        /// </summary>
        private static bool CloseOpenGraphWindows(string graphPath)
        {
            var assetName = Path.GetFileNameWithoutExtension(graphPath);

            var gtkWindows = new System.Collections.Generic.List<EditorWindow>();
            var matches = new System.Collections.Generic.List<EditorWindow>();
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                var typeName = window.GetType().FullName ?? "";
                if (!typeName.StartsWith("Unity.GraphToolkit", System.StringComparison.Ordinal))
                    continue;
                gtkWindows.Add(window);

                var title = (window.titleContent != null ? window.titleContent.text : "").TrimEnd('*').Trim();
                if (title.Length > 0 &&
                    (string.Equals(title, assetName, System.StringComparison.OrdinalIgnoreCase) ||
                     title.IndexOf(assetName, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     assetName.IndexOf(title, System.StringComparison.OrdinalIgnoreCase) >= 0))
                    matches.Add(window);
            }

            if (gtkWindows.Count == 0) return false;

            var toClose = matches.Count > 0 ? matches : gtkWindows;
            foreach (var window in toClose)
            {
                Debug.Log($"[WitWeaver] Closing graph window '{window.titleContent?.text}' to apply external changes.");
                window.Close();
            }
            return true;
        }

        /// <summary>True when the graph has bake-relevant changes not yet applied to the conversation.</summary>
        public static bool IsGraphDirtyRelativeToConversation(WitWeaverConversationData data)
        {
            var path = ResolveGraphPath(data);
            if (path == null) return false;

            var graph = GraphDatabase.LoadGraph<WitWeaverConversationGraph>(path);
            return graph != null && WitWeaverGraphBake.IsDirty(graph);
        }

        /// <summary>
        /// Bakes the conversation's graph into the conversation asset and its YAML. When
        /// <paramref name="interactive"/>, asks for confirmation (the YAML file is rewritten —
        /// comments and custom formatting outside the schema are not preserved).
        /// </summary>
        public static bool BakeGraphFor(WitWeaverConversationData data, bool interactive = false)
        {
            var path = ResolveGraphPath(data);
            if (path == null)
            {
                Debug.LogWarning($"[WitWeaver] '{data?.name}' has no conversation graph to bake.");
                return false;
            }

            var graph = GraphDatabase.LoadGraph<WitWeaverConversationGraph>(path);
            if (graph == null)
            {
                Debug.LogError($"[WitWeaver] Could not load graph at '{path}'.");
                return false;
            }

            // Stale gate (mirrors the refusal inside Bake): the only offered action is the
            // refresh — there is no "bake anyway".
            if (WitWeaverGraphSync.IsYamlStale(graph, data, out bool sectionMissing))
            {
                if (!interactive)
                {
                    Debug.LogError($"[WitWeaver] Bake refused for '{data.name}': YAML is stale" +
                                   (sectionMissing ? " (section not found)." : " — refresh the graph first."));
                    return false;
                }

                if (sectionMissing)
                {
                    EditorUtility.DisplayDialog(
                        "YAML section not found",
                        $"The YAML section for key '{data.ConversationKey}' could not be found — the file is " +
                        "missing, unparseable, or the key was removed. Fix the YAML link before baking.",
                        "OK");
                }
                else if (EditorUtility.DisplayDialog(
                             "YAML changed outside this graph",
                             $"The YAML for '{data.name}' was edited since the graph last synced (text edits, " +
                             "translations, or spreadsheet imports). Baking now is blocked to protect those " +
                             "edits.\n\nRefresh the graph from the YAML, review the result, then bake.",
                             "Refresh From YAML", "Cancel"))
                {
                    RefreshGraphFromYamlFor(data);
                }
                return false;
            }

            if (interactive && !EditorUtility.DisplayDialog(
                    "Bake Conversation Graph",
                    $"Bake the graph into '{data.name}'?\n\n" +
                    "This rewrites the conversation's section of its YAML source (comments and " +
                    "custom formatting are not preserved) and updates the conversation asset. " +
                    "The YAML rewrite is not undoable.",
                    "Bake", "Cancel"))
                return false;

            bool ok = WitWeaverGraphBake.Bake(graph, data, out var report);
            if (ok) Debug.Log($"[WitWeaver] {report}", data);
            else Debug.LogError($"[WitWeaver] {report}", data);

            // Push the saved state (updated hashes) into any open window so its private model
            // does not later overwrite the bake results.
            if (ok)
                ReloadOpenGraphWindows(path);

            if (interactive && !ok)
                EditorUtility.DisplayDialog("Bake Failed", report, "OK");
            return ok;
        }
    }
}
