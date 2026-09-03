// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    /// <summary>
    /// The single sanctioned way to replace the EmbeddedYaml sub-asset on a conversation asset,
    /// shared by the YAML watcher/inspector path and the spreadsheet import pipeline. Callers are
    /// responsible for validating the YAML text BEFORE calling (parse + LineID checks) and for
    /// SetDirty/SaveAssets afterward. Also owns the embed provenance fields.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverEmbedUtility.html")]
    internal static class WitWeaverEmbedUtility
    {
        /// <summary>
        /// Destroys any existing "EmbeddedYaml" sub-asset(s), creates a new TextAsset from
        /// yamlText, adds it as a sub-asset, assigns data.ConversationYaml, records provenance,
        /// and defaults data.FilePath to the source file's stem when empty.
        /// </summary>
        internal static TextAsset ReplaceEmbeddedYaml(WitWeaverConversationData data, string yamlText,
            string sourceAssetPath)
        {
            // Remove any prior embedded subasset(s) named EmbeddedYaml
            var conversationAssetPath = AssetDatabase.GetAssetPath(data);
            if (data.ConversationYaml != null)
            {
                UnityEngine.Object.DestroyImmediate(data.ConversationYaml, true);
                data.ConversationYaml = null;
            }

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

            // Create the new embedded YAML TextAsset subasset
            var embedded = new TextAsset(yamlText) { name = "EmbeddedYaml" };
            AssetDatabase.AddObjectToAsset(embedded, data);
            data.ConversationYaml = embedded;

            RecordProvenance(data, sourceAssetPath);

            // Default persistent-override stem: file name only, resolved under
            // persistentDataPath/WitWeaver/Dialogue/ at runtime
            if (string.IsNullOrEmpty(data.FilePath))
            {
                data.FilePath = Path.GetFileNameWithoutExtension(sourceAssetPath);
            }

            return embedded;
        }

        /// <summary>Stamps which source file produced the current embed and when.</summary>
        internal static void RecordProvenance(WitWeaverConversationData data, string sourceAssetPath)
        {
            data.EmbeddedFromSourcePath = sourceAssetPath;
            data.EmbeddedAtTicks = DateTime.UtcNow.Ticks;
        }

        /// <summary>Clears the embed provenance (used when the embed itself is removed).</summary>
        internal static void ClearProvenance(WitWeaverConversationData data)
        {
            data.EmbeddedFromSourcePath = string.Empty;
            data.EmbeddedAtTicks = 0;
        }

        /// <summary>True when the path points at an .xlsx spreadsheet.</summary>
        internal static bool IsSpreadsheetSource(string path)
            => !string.IsNullOrEmpty(path) && path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);
    }
}
#endif
