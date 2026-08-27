using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    /// <summary>
    /// AssetPostprocessor that automatically runs the Excel round-trip pipeline
    /// when a linked .xlsx file is imported or renamed in the Unity project.
    /// Mirrors <see cref="WitWeaverYamlWatcher"/> for .xlsx files.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverExcelWatcher.html")]
    public class WitWeaverExcelWatcher : AssetPostprocessor
    {
        // Guard against re-entrant calls triggered by the pipeline's own AssetDatabase.ImportAsset.
        // Unity's AssetPostprocessor callbacks always run on the main thread, so no lock needed.
        private static readonly HashSet<string> _processing = new HashSet<string>();

        // Centralised property names — if WitWeaverConversationData renames these fields,
        // compilation will not catch the break but at least a single place needs updating.
        private const string PropSourceExcelAssetPath = "SourceExcelAssetPath";
        private const string PropSourceExcelAsset     = "SourceExcelAsset";

        private static bool HasXlsx(string[] paths) =>
            paths != null &&
            System.Array.Exists(paths, p => p.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase));

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssets)
        {
            // Early exit: skip the full WitWeaverConversationData scan when no .xlsx files
            // are involved — this fires for every Unity import (textures, scripts, prefabs…).
            if (!HasXlsx(importedAssets) && !HasXlsx(movedAssets) && !HasXlsx(movedFromAssets))
                return;

            var importedSet = new HashSet<string>(importedAssets);

            // Build moved path map: old path -> new path
            var movedMap = new Dictionary<string, string>();
            if (movedAssets != null && movedFromAssets != null)
            {
                int len = UnityEngine.Mathf.Min(movedAssets.Length, movedFromAssets.Length);
                for (int i = 0; i < len; i++)
                {
                    var from = movedFromAssets[i];
                    var to = movedAssets[i];
                    if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                        movedMap[from] = to;
                }
            }

            // Find all WitWeaverConversationData assets
            var guids = AssetDatabase.FindAssets("t:WolfstagInteractive.WitWeaver.WitWeaverConversationData");
            if (guids == null || guids.Length == 0) return;

            foreach (var guid in guids)
            {
                var soPath = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<WitWeaverConversationData>(soPath);
                if (data == null) continue;

                var so = new SerializedObject(data);
                var excelPathProp = so.FindProperty(PropSourceExcelAssetPath);
                if (excelPathProp == null) continue;

                var linkedPath = excelPathProp.stringValue;
                if (string.IsNullOrEmpty(linkedPath)) continue;

                // Handle file moved/renamed
                if (movedMap.TryGetValue(linkedPath, out var newPath))
                {
                    excelPathProp.stringValue = newPath;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    // Update the asset reference as well
                    var excelAssetProp = so.FindProperty(PropSourceExcelAsset);
                    if (excelAssetProp != null)
                        excelAssetProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(newPath);
                    so.ApplyModifiedPropertiesWithoutUndo();

                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();

                    Debug.Log(
                        $"WitWeaver Excel: Updated SourceExcelAssetPath after move/rename:\n" +
                        $"  {linkedPath} -> {newPath}\n  Asset: {soPath}");

                    linkedPath = newPath;
                }

                // Run pipeline if the linked .xlsx was imported
                if (!importedSet.Contains(linkedPath)) continue;

                if (_processing.Contains(linkedPath)) continue;
                _processing.Add(linkedPath);

                try
                {
                    bool success = WitWeaverExcelUtilities.RunFullPipeline(data, linkedPath, out var msg);

                    if (success)
                        Debug.Log($"WitWeaver Excel: Auto-synced '{System.IO.Path.GetFileName(linkedPath)}' into '{data.name}'. {msg}");
                    else
                        Debug.LogError($"WitWeaver Excel: Auto-sync failed for '{System.IO.Path.GetFileName(linkedPath)}' into '{data.name}'. {msg}");
                }
                finally
                {
                    _processing.Remove(linkedPath);
                }
            }
        }
    }
}
