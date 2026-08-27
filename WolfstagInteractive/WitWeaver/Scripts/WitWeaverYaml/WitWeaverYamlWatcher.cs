#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("WitWeaverEditor")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("WitWeaverGraphEditor")]

namespace WolfstagInteractive.WitWeaver.Editor
{
    [HelpURL(
        "https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverYamlWatcher.html")]
    public class WitWeaverYamlWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var guids = AssetDatabase.FindAssets("t:WolfstagInteractive.WitWeaver.WitWeaverConversationData");
            if (guids == null || guids.Length == 0) return;

            var importedSet = new HashSet<string>(imported);

            var movedMap = new Dictionary<string, string>();
            if (moved != null && movedFrom != null)
            {
                int len = Mathf.Min(moved.Length, movedFrom.Length);
                for (int i = 0; i < len; i++)
                {
                    var from = movedFrom[i];
                    var to = moved[i];
                    if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                        movedMap[from] = to;
                }
            }

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<WitWeaverConversationData>(assetPath);
                if (data == null) continue;

                var so = new SerializedObject(data);
                var srcPathProp = so.FindProperty("SourceYamlAssetPath");
                if (srcPathProp == null) continue;

                var linkedPath = srcPathProp.stringValue;
                if (string.IsNullOrEmpty(linkedPath)) continue;

                if (movedMap.TryGetValue(linkedPath, out var newPath))
                {
                    srcPathProp.stringValue = newPath;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();

                    Debug.Log(
                        $"WitWeaver: Updated SourceYamlAssetPath after move/rename:\n  {linkedPath} -> {newPath}\n  Asset: {assetPath}");

                    if (importedSet.Contains(newPath))
                    {
                        if (TryEmbedFromPath(data, newPath))
                        {
                            so.Update();
                            so.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(data);
                            AssetDatabase.SaveAssets();
                            Debug.Log(
                                $"WitWeaver: Auto-synced embedded YAML after move from '{newPath}' into '{assetPath}'.");
                        }
                    }

                    linkedPath = newPath;
                }

                if (importedSet.Contains(linkedPath))
                {
                    if (TryEmbedFromPath(data, linkedPath))
                    {
                        so.Update();
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(data);
                        AssetDatabase.SaveAssets();
                        Debug.Log($"WitWeaver: Auto-synced embedded YAML from '{linkedPath}' into '{assetPath}'.");
                    }
                }
            }
        }

        internal static bool TryEmbedFromPath(WitWeaverConversationData data, string sourcePath)
        {
            if (data == null) return false;
            if (string.IsNullOrEmpty(sourcePath)) return false;

            // Read source text
            string srcText;
            var srcObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(sourcePath);
            if (srcObj is TextAsset ta)
                srcText = ta.text;
            else
                srcText = File.ReadAllText(sourcePath);

            if (string.IsNullOrEmpty(srcText))
                return false;

            // Parse
            if (!WitWeaverYamlParser.TryParse(srcText, out var dict,
                    out IReadOnlyList<WitWeaverYamlDiagnostic> parseDiagnostics))
            {
                // Errors always surface regardless of VerboseLogs.
                Debug.LogError(
                    $"WitWeaver: YAML parse failed, cannot embed.\n" +
                    WitWeaverYamlDiagnostic.Format(sourcePath, parseDiagnostics),
                    data);
                return false;
            }

            // Surface any warnings (smart quotes, duplicate locales, etc.) only when verbose logging is on.
            if ((WitWeaverYamlLoader.Settings?.VerboseLogs ?? false) && parseDiagnostics.Count > 0)
                Debug.LogWarning(
                    $"WitWeaver: YAML parsed with warnings.\n" +
                    WitWeaverYamlDiagnostic.Format(sourcePath, parseDiagnostics),
                    data);

            // Ensure IDs (and validate uniqueness)
            bool changed = WitWeaverLineIDUtility.EnsureLineIds(dict, out var idErr);
            if (idErr != null)
            {
                Debug.LogError($"WitWeaver: {idErr}\nSource: {sourcePath}", data);
                return false;
            }

            // Hard validation (Fix 4): refuse to embed if any LineID is missing
            if (HasMissingLineIds(dict, out var missingDetails))
            {
                Debug.LogError(
                    $"WitWeaver: Embed refused. YAML still contains missing LineID after ensure. {missingDetails}\nSource: {sourcePath}",
                    data);
                return false;
            }

            // Serialize back if we touched anything
            if (changed)
            {
                srcText = WitWeaverYamlSerializer.Serialize(dict);

                // Persist to the source file only when writable
                if (sourcePath.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    try
                    {
                        File.WriteAllText(Path.GetFullPath(sourcePath), srcText);
                        AssetDatabase.ImportAsset(sourcePath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"WitWeaver: Failed to write back LineIDs to '{sourcePath}'. {ex.Message}",
                            data);
                    }
                }
            }

            // If the embedded text is already identical, no work
            if (data.ConversationYaml != null && data.ConversationYaml.text == srcText)
                return false;

            // Remove any prior embedded subasset(s) named EmbeddedYaml
            var conversationAssetPath = AssetDatabase.GetAssetPath(data);
            if (!string.IsNullOrEmpty(conversationAssetPath))
            {
                var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(conversationAssetPath);
                if (reps != null)
                {
                    for (int i = 0; i < reps.Length; i++)
                    {
                        if (reps[i] is TextAsset { name: "EmbeddedYaml" } repTa)
                            UnityEngine.Object.DestroyImmediate(repTa, true);
                    }
                }
            }

            // Create a new embedded YAML TextAsset subasset
            var embedded = new TextAsset(srcText) { name = "EmbeddedYaml" };
            AssetDatabase.AddObjectToAsset(embedded, data);
            data.ConversationYaml = embedded;

            // Keep the default FilePath stable if not set
            if (string.IsNullOrEmpty(data.FilePath))
            {
                var baseName = Path.GetFileNameWithoutExtension(sourcePath);
                data.FilePath = $"WitWeaver/Dialogue/{baseName}";
            }

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            return true;
        }

        private static bool HasMissingLineIds(
            Dictionary<string, List<DialogueYamlConfig>> dict,
            out string details)
        {
            details = null;
            if (dict == null) return true;

            foreach (var kv in dict)
            {
                var conversationKey = kv.Key;
                var list = kv.Value;
                if (list == null) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    var cfg = list[i];
                    if (cfg == null) continue;

                    if (string.IsNullOrWhiteSpace(cfg.LineID))
                    {
                        details = $"Conversation '{conversationKey}', index {i}.";
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
#endif