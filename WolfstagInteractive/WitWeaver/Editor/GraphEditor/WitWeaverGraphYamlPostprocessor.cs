// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;
using System;
using UnityEditor;

namespace WolfstagInteractive.WitWeaver.GraphEditor
{
    /// <summary>
    /// Stateless import hook: when a YAML file linked to a graph-authored conversation is
    /// (re)imported, repaint open editor windows so the stale banner (driven purely by the
    /// hash comparison in <see cref="WitWeaverGraphSync.IsYamlStale"/>) appears promptly.
    /// No stale flag is stored anywhere — the hash comparison is the single source of truth.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1GraphEditor_1_1WitWeaverGraphYamlPostprocessor.html")]
    internal sealed class WitWeaverGraphYamlPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!AnyYaml(importedAssets) && !AnyYaml(deletedAssets) && !AnyYaml(movedAssets))
                return;

            foreach (var guid in AssetDatabase.FindAssets("t:WitWeaverConversationData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<WitWeaverConversationData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (data == null ||
                    data.AuthoringMode != WitWeaverConversationData.ConversationAuthoringMode.Graph ||
                    string.IsNullOrEmpty(data.SourceYamlAssetPath))
                    continue;

                if (Contains(importedAssets, data.SourceYamlAssetPath) ||
                    Contains(deletedAssets, data.SourceYamlAssetPath) ||
                    Contains(movedAssets, data.SourceYamlAssetPath))
                {
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    return;
                }
            }
        }

        private static bool AnyYaml(string[] paths)
        {
            if (paths == null) return false;
            foreach (var path in paths)
                if (path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool Contains(string[] paths, string target)
        {
            if (paths == null) return false;
            foreach (var path in paths)
                if (string.Equals(path, target, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
