// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

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

                    // Repair the object reference and embed provenance alongside the path
                    // (parity with the spreadsheet watcher)
                    var srcObjProp = so.FindProperty("SourceYaml");
                    if (srcObjProp != null)
                        srcObjProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(newPath);

                    var provenanceProp = so.FindProperty("EmbeddedFromSourcePath");
                    if (provenanceProp != null && provenanceProp.stringValue == linkedPath)
                        provenanceProp.stringValue = newPath;

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

        internal enum EmbedResult
        {
            Failed,
            Unchanged,
            Embedded
        }

        internal static bool TryEmbedFromPath(WitWeaverConversationData data, string sourcePath)
            => EmbedFromPath(data, sourcePath) == EmbedResult.Embedded;

        internal static EmbedResult EmbedFromPath(WitWeaverConversationData data, string sourcePath)
        {
            if (data == null) return EmbedResult.Failed;
            if (string.IsNullOrEmpty(sourcePath)) return EmbedResult.Failed;

            // Read source text
            string srcText;
            var srcObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(sourcePath);
            if (srcObj is TextAsset ta)
                srcText = ta.text;
            else
                srcText = File.ReadAllText(sourcePath);

            if (string.IsNullOrEmpty(srcText))
                return EmbedResult.Failed;

            // Parse
            if (!WitWeaverYamlParser.TryParse(srcText, out var dict,
                    out IReadOnlyList<WitWeaverYamlDiagnostic> parseDiagnostics))
            {
                // Errors always surface regardless of VerboseLogs.
                Debug.LogError(
                    $"WitWeaver: YAML parse failed, cannot embed.\n" +
                    WitWeaverYamlDiagnostic.Format(sourcePath, parseDiagnostics),
                    data);
                return EmbedResult.Failed;
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
                return EmbedResult.Failed;
            }

            // Hard validation (Fix 4): refuse to embed if any LineID is missing
            if (HasMissingLineIds(dict, out var missingDetails))
            {
                Debug.LogError(
                    $"WitWeaver: Embed refused. YAML still contains missing LineID after ensure. {missingDetails}\nSource: {sourcePath}",
                    data);
                return EmbedResult.Failed;
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

            // If the embedded text is already identical, the compiled lines may still be behind
            // (e.g. assets embedded before text syncing existed); heal them, otherwise no work.
            if (data.ConversationYaml != null && data.ConversationYaml.text == srcText)
            {
                // Record provenance for legacy embeds or a re-link to a different file with
                // identical content
                bool provenanceChanged = data.EmbeddedFromSourcePath != sourcePath;
                if (provenanceChanged)
                    WitWeaverEmbedUtility.RecordProvenance(data, sourcePath);

                bool linesChanged = SyncCompiledText(data, dict);
                if (linesChanged || provenanceChanged)
                {
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                }
                return linesChanged ? EmbedResult.Embedded : EmbedResult.Unchanged;
            }

            // Replace the embedded sub-asset via the shared helper (also records provenance and
            // defaults the persistent-override stem)
            WitWeaverEmbedUtility.ReplaceEmbeddedYaml(data, srcText, sourcePath);

            // Keep the compiled lines' text in step with what was just embedded (text only;
            // structural changes still require an explicit Import From YAML For Key)
            SyncCompiledText(data, dict);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            return EmbedResult.Embedded;
        }

        /// <summary>
        /// Text-only refresh of the serialized DialogueLines from already-validated parsed YAML.
        /// Skipped during play mode (a running conversation owns its state; the runtime merge
        /// happens on the next conversation start anyway). Warns once per sync when the YAML's
        /// structure has drifted from the compiled lines. Returns true when any line changed.
        /// </summary>
        static bool SyncCompiledText(WitWeaverConversationData data,
            Dictionary<string, List<DialogueYamlConfig>> dict)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return false;

            bool changed = WitWeaverYamlTextSync.MergeLocalizedText(data, dict);

            WitWeaverYamlTextSync.Analyze(data, dict, out _, out int yamlOnly, out int assetOnly);
            if (yamlOnly > 0 || assetOnly > 0)
            {
                Debug.LogWarning(
                    $"WitWeaver: '{data.name}' dialogue structure differs from its YAML source " +
                    $"({yamlOnly} line(s) only in YAML, {assetOnly} line(s) only in the asset). " +
                    "Text was synced for matching lines; use 'Import From YAML For Key' on the asset to sync structure.",
                    data);
            }

            return changed;
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