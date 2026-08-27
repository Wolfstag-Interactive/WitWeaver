#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ConvoCoreEditor")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ConvoCoreGraphEditor")]

namespace WolfstagInteractive.ConvoCore.Editor
{
    [HelpURL(
        "https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreYamlWatcher.html")]
    public class ConvoCoreYamlWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var guids = AssetDatabase.FindAssets("t:WolfstagInteractive.ConvoCore.ConvoCoreConversationData");
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
                var data = AssetDatabase.LoadAssetAtPath<ConvoCoreConversationData>(assetPath);
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
                        $"ConvoCore: Updated SourceYamlAssetPath after move/rename:\n  {linkedPath} -> {newPath}\n  Asset: {assetPath}");

                    if (importedSet.Contains(newPath))
                    {
                        if (TryEmbedFromPath(data, newPath))
                        {
                            so.Update();
                            so.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(data);
                            AssetDatabase.SaveAssets();
                            Debug.Log(
                                $"ConvoCore: Auto-synced embedded YAML after move from '{newPath}' into '{assetPath}'.");
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
                        Debug.Log($"ConvoCore: Auto-synced embedded YAML from '{linkedPath}' into '{assetPath}'.");
                    }
                }
            }
        }

        internal static bool TryEmbedFromPath(ConvoCoreConversationData data, string sourcePath)
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
            if (!ConvoCoreYamlParser.TryParse(srcText, out var dict,
                    out IReadOnlyList<ConvoCoreYamlDiagnostic> parseDiagnostics))
            {
                // Errors always surface regardless of VerboseLogs.
                Debug.LogError(
                    $"ConvoCore: YAML parse failed, cannot embed.\n" +
                    ConvoCoreYamlDiagnostic.Format(sourcePath, parseDiagnostics),
                    data);
                return false;
            }

            // Surface any warnings (smart quotes, duplicate locales, etc.) only when verbose logging is on.
            if ((ConvoCoreYamlLoader.Settings?.VerboseLogs ?? false) && parseDiagnostics.Count > 0)
                Debug.LogWarning(
                    $"ConvoCore: YAML parsed with warnings.\n" +
                    ConvoCoreYamlDiagnostic.Format(sourcePath, parseDiagnostics),
                    data);

            // Ensure IDs (and validate uniqueness)
            bool changed = ConvoCoreLineIDUtility.EnsureLineIds(dict, out var idErr);
            if (idErr != null)
            {
                Debug.LogError($"ConvoCore: {idErr}\nSource: {sourcePath}", data);
                return false;
            }

            // Hard validation (Fix 4): refuse to embed if any LineID is missing
            if (HasMissingLineIds(dict, out var missingDetails))
            {
                Debug.LogError(
                    $"ConvoCore: Embed refused. YAML still contains missing LineID after ensure. {missingDetails}\nSource: {sourcePath}",
                    data);
                return false;
            }

            // Serialize back if we touched anything
            if (changed)
            {
                srcText = ConvoCoreYamlSerializer.Serialize(dict);

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
                        Debug.LogWarning($"ConvoCore: Failed to write back LineIDs to '{sourcePath}'. {ex.Message}",
                            data);
                    }
                }
            }

            // If the embedded text is already identical, no work
            if (data.ConversationYaml != null && data.ConversationYaml.text == srcText)
                return false;

            // Remove any prior embedded subasset(s) named EmbeddedYaml
            var convoAssetPath = AssetDatabase.GetAssetPath(data);
            if (!string.IsNullOrEmpty(convoAssetPath))
            {
                var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(convoAssetPath);
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
                data.FilePath = $"ConvoCore/Dialogue/{baseName}";
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
                var convoKey = kv.Key;
                var list = kv.Value;
                if (list == null) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    var cfg = list[i];
                    if (cfg == null) continue;

                    if (string.IsNullOrWhiteSpace(cfg.LineID))
                    {
                        details = $"Conversation '{convoKey}', index {i}.";
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
#endif