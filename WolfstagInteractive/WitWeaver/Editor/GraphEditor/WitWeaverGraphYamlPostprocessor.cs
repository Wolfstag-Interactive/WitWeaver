using UnityEngine;
using System;
using UnityEditor;

namespace WolfstagInteractive.ConvoCore.GraphEditor
{
    /// <summary>
    /// Stateless import hook: when a YAML file linked to a graph-authored conversation is
    /// (re)imported, repaint open editor windows so the stale banner (driven purely by the
    /// hash comparison in <see cref="ConvoCoreGraphSync.IsYamlStale"/>) appears promptly.
    /// No stale flag is stored anywhere — the hash comparison is the single source of truth.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1GraphEditor_1_1ConvoCoreGraphYamlPostprocessor.html")]
    internal sealed class ConvoCoreGraphYamlPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!AnyYaml(importedAssets) && !AnyYaml(deletedAssets) && !AnyYaml(movedAssets))
                return;

            foreach (var guid in AssetDatabase.FindAssets("t:ConvoCoreConversationData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<ConvoCoreConversationData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (data == null ||
                    data.AuthoringMode != ConvoCoreConversationData.ConversationAuthoringMode.Graph ||
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
