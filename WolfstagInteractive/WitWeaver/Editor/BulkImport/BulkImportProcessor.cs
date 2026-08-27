using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WolfstagInteractive.ConvoCore;

namespace WolfstagInteractive.ConvoCore.Editor
{
    internal enum AssetNamingMode
    {
        YamlFileAndKey,
        ConversationKeyOnly
    }

    internal static class BulkImportProcessor
    {
        public static List<BulkImportManifestEntry> BuildManifest(string inputFolderPath, bool recursive)
        {
            var manifest = new List<BulkImportManifestEntry>();

            // Normalize to forward slashes and strip trailing slash
            inputFolderPath = inputFolderPath.Replace('\\', '/').TrimEnd('/');

            // AssetDatabase.FindAssets("") is unreliable in Unity 2021 as it can return nothing
            // even when files exist. Use the filesystem directly and convert back to asset paths.
            var dataPath = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            var absoluteInput = dataPath.Substring(0, dataPath.Length - "Assets".Length) + inputFolderPath;

            if (!Directory.Exists(absoluteInput))
                return manifest;

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            var yamlPaths = new List<string>();
            foreach (var ext in new[] { "*.yml", "*.yaml" })
            {
                foreach (var file in Directory.GetFiles(absoluteInput, ext, searchOption))
                {
                    // Convert absolute path back to Assets-relative (forward slashes)
                    var normalized = file.Replace('\\', '/');
                    var relPath = "Assets" + normalized.Substring(dataPath.Length);
                    yamlPaths.Add(relPath);
                }
            }

            // Build existing SO lookup
            var existingByKey = BuildExistingAssetMap();

            // Track first occurrence of each key for conflict detection
            var firstOccurrence = new Dictionary<string, BulkImportManifestEntry>();

            foreach (var yamlPath in yamlPaths)
            {
                // .yaml is imported as TextAsset; .yml may come back as DefaultAsset, so fall back to disk read.
                string yamlText;
                var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(yamlPath);
                if (ta != null)
                {
                    yamlText = ta.text;
                }
                else
                {
                    try { yamlText = File.ReadAllText(yamlPath); }
                    catch (Exception ex)
                    {
                        manifest.Add(new BulkImportManifestEntry
                        {
                            ConversationKey = Path.GetFileNameWithoutExtension(yamlPath),
                            YamlAssetPath = yamlPath,
                            Status = BulkImportEntryStatus.Error,
                            StatusDetail = $"Could not read file: {ex.Message}",
                            Selected = false
                        });
                        continue;
                    }
                }

                if (!ConvoCoreYamlParser.TryParse(yamlText, out var dict,
                        out IReadOnlyList<ConvoCoreYamlDiagnostic> diagnostics))
                {
                    var errDetail = ConvoCoreYamlDiagnostic.Format(yamlPath, diagnostics);
                    manifest.Add(new BulkImportManifestEntry
                    {
                        ConversationKey = Path.GetFileNameWithoutExtension(yamlPath),
                        YamlAssetPath = yamlPath,
                        LineCount = 0,
                        Status = BulkImportEntryStatus.Error,
                        StatusDetail = errDetail,
                        Selected = false
                    });
                    continue;
                }

                if (dict == null || dict.Count == 0)
                {
                    Debug.Log($"ConvoCore Bulk Import: '{yamlPath}' parsed successfully but contains no conversation keys. Skipping.");
                    continue;
                }

                foreach (var kvp in dict)
                {
                    var key = kvp.Key;
                    var lines = kvp.Value;

                    if (lines == null || lines.Count == 0)
                    {
                        manifest.Add(new BulkImportManifestEntry
                        {
                            ConversationKey = key,
                            YamlAssetPath = yamlPath,
                            LineCount = 0,
                            Status = BulkImportEntryStatus.Error,
                            StatusDetail = $"Conversation key '{key}' exists in the YAML but has no dialogue lines. Add content before importing.",
                            Selected = false
                        });
                        continue;
                    }

                    if (firstOccurrence.TryGetValue(key, out var prior))
                    {
                        // Conflict — mark the prior entry and create a new conflict entry
                        if (prior.Status != BulkImportEntryStatus.Conflict)
                        {
                            prior.Status = BulkImportEntryStatus.Conflict;
                            prior.StatusDetail = $"Also found in '{yamlPath}'.";
                            prior.Selected = false;
                        }

                        manifest.Add(new BulkImportManifestEntry
                        {
                            ConversationKey = key,
                            YamlAssetPath = yamlPath,
                            LineCount = lines.Count,
                            Status = BulkImportEntryStatus.Conflict,
                            StatusDetail = $"Also found in '{prior.YamlAssetPath}'.",
                            Selected = false
                        });
                        continue;
                    }

                    existingByKey.TryGetValue(key, out var existingSo);

                    var entry = new BulkImportManifestEntry
                    {
                        ConversationKey = key,
                        YamlAssetPath = yamlPath,
                        LineCount = lines.Count,
                        Status = existingSo != null ? BulkImportEntryStatus.Update : BulkImportEntryStatus.New,
                        StatusDetail = null,
                        Selected = true,
                        ExistingAsset = existingSo
                    };

                    manifest.Add(entry);
                    firstOccurrence[key] = entry;
                }
            }

            return manifest;
        }

        public static List<BulkImportResult> Execute(
            List<BulkImportManifestEntry> manifest,
            string outputFolderPath,
            AssetNamingMode namingMode,
            Action<int, int, string> onProgress = null)
        {
            var results = new List<BulkImportResult>();

            outputFolderPath = outputFolderPath.Replace('\\', '/').TrimEnd('/');

            var selected = new List<BulkImportManifestEntry>();
            foreach (var entry in manifest)
            {
                if (entry.Selected &&
                    entry.Status != BulkImportEntryStatus.Conflict &&
                    entry.Status != BulkImportEntryStatus.Error &&
                    entry.Status != BulkImportEntryStatus.Skipped)
                    selected.Add(entry);
            }

            // Ensure output folder exists before batching, so AssetDatabase reflects it immediately.
            EnsureOutputFolderExists(outputFolderPath);

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    var entry = selected[i];
                    onProgress?.Invoke(i, selected.Count, entry.ConversationKey);

                    if (entry.Status == BulkImportEntryStatus.New)
                        results.Add(ExecuteNew(entry, outputFolderPath, namingMode));
                    else
                        results.Add(ExecuteUpdate(entry));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return results;
        }

        // ----- Private helpers -----

        private static BulkImportResult ExecuteNew(
            BulkImportManifestEntry entry, string outputFolderPath, AssetNamingMode namingMode)
        {
            string outputPath = null;
            try
            {
                outputPath = BuildOutputPath(entry, outputFolderPath, namingMode);

                var data = ScriptableObject.CreateInstance<ConvoCoreConversationData>();
                data.ConversationKey = entry.ConversationKey;
                data.ConversationTitle = entry.ConversationKey;

                // CreateAsset MUST come before TryEmbedFromPath: the embed method calls
                // AssetDatabase.GetAssetPath(data) to locate the asset for subasset attachment.
                AssetDatabase.CreateAsset(data, outputPath);

                // TryEmbedFromPath writes the YAML text into ConversationYaml (subasset).
                // ImportFromYamlForKey reads it back and creates the DialogueLineInfo objects.
                // Both calls are required: embed first so the text is available to import.
                ConvoCoreYamlWatcher.TryEmbedFromPath(data, entry.YamlAssetPath);
                new ConvoCoreYamlUtilities(data).ImportFromYamlForKey(entry.ConversationKey);

                data.SourceYamlAssetPath = entry.YamlAssetPath;
                EditorUtility.SetDirty(data);

                return new BulkImportResult
                {
                    ConversationKey = entry.ConversationKey,
                    YamlAssetPath = entry.YamlAssetPath,
                    Outcome = BulkImportOutcome.Created,
                    OutputAssetPath = outputPath
                };
            }
            catch (Exception ex)
            {
                if (outputPath != null && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outputPath) != null)
                    AssetDatabase.DeleteAsset(outputPath);

                return new BulkImportResult
                {
                    ConversationKey = entry.ConversationKey,
                    YamlAssetPath = entry.YamlAssetPath,
                    Outcome = BulkImportOutcome.Failed,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static BulkImportResult ExecuteUpdate(BulkImportManifestEntry entry)
        {
            var data = entry.ExistingAsset;
            var assetPath = AssetDatabase.GetAssetPath(data);

            var backup = data.DialogueLines != null
                ? new List<ConvoCoreConversationData.DialogueLineInfo>(data.DialogueLines)
                : new List<ConvoCoreConversationData.DialogueLineInfo>();
            try
            {
                ConvoCoreYamlWatcher.TryEmbedFromPath(data, entry.YamlAssetPath);
                new ConvoCoreYamlUtilities(data).ImportFromYamlForKey(entry.ConversationKey);

                if (data.SourceYamlAssetPath != entry.YamlAssetPath)
                {
                    Debug.Log($"ConvoCore Bulk Import: YAML source changed for '{entry.ConversationKey}': '{data.SourceYamlAssetPath}' → '{entry.YamlAssetPath}'.");
                    data.SourceYamlAssetPath = entry.YamlAssetPath;
                }

                EditorUtility.SetDirty(data);

                return new BulkImportResult
                {
                    ConversationKey = entry.ConversationKey,
                    YamlAssetPath = entry.YamlAssetPath,
                    Outcome = BulkImportOutcome.Updated,
                    OutputAssetPath = assetPath
                };
            }
            catch (Exception ex)
            {
                data.DialogueLines = backup;

                return new BulkImportResult
                {
                    ConversationKey = entry.ConversationKey,
                    YamlAssetPath = entry.YamlAssetPath,
                    Outcome = BulkImportOutcome.Failed,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static Dictionary<string, ConvoCoreConversationData> BuildExistingAssetMap()
        {
            var map = new Dictionary<string, ConvoCoreConversationData>(StringComparer.Ordinal);
            var guids = AssetDatabase.FindAssets("t:ConvoCoreConversationData");
            if (guids == null) return map;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ConvoCoreConversationData>(path);
                if (so == null || string.IsNullOrEmpty(so.ConversationKey)) continue;
                if (!map.ContainsKey(so.ConversationKey))
                    map[so.ConversationKey] = so;
            }

            return map;
        }

        private static void EnsureOutputFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string BuildOutputPath(
            BulkImportManifestEntry entry, string outputFolderPath, AssetNamingMode namingMode)
        {
            string baseName;
            if (namingMode == AssetNamingMode.YamlFileAndKey)
            {
                var yamlFileName = Path.GetFileNameWithoutExtension(entry.YamlAssetPath);
                baseName = SanitizeFileName($"{yamlFileName}_{entry.ConversationKey}");
            }
            else
            {
                baseName = SanitizeFileName(entry.ConversationKey);
            }

            var path = $"{outputFolderPath}/{baseName}.asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null)
                return path;

            int suffix = 1;
            while (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>($"{outputFolderPath}/{baseName}_{suffix}.asset") != null)
                suffix++;

            return $"{outputFolderPath}/{baseName}_{suffix}.asset";
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' })
                name = name.Replace(c, '_');
            return name.Trim('.', ' ');
        }
    }
}