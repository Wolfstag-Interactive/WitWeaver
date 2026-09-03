// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.GraphEditor
{
    /// <summary>
    /// Makes the companion graph asset behave like part of the conversation asset: it is renamed,
    /// moved, and deleted together with its <see cref="WitWeaverConversationData"/>, and
    /// double-clicking a graph-authored conversation opens the graph directly. Users never manage
    /// the graph file by hand.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1GraphEditor_1_1WitWeaverGraphAssetProcessor.html")]
    internal sealed class WitWeaverGraphAssetProcessor : AssetModificationProcessor
    {
        private static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
        {
            if (!sourcePath.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                return AssetMoveResult.DidNotMove;

            var data = AssetDatabase.LoadAssetAtPath<WitWeaverConversationData>(sourcePath);
            if (data == null)
                return AssetMoveResult.DidNotMove;

            var graphPath = WitWeaverGraphBridge.ResolveGraphPath(data);
            if (graphPath != null)
            {
                var folder = Path.GetDirectoryName(destinationPath)?.Replace('\\', '/') ?? "Assets";
                var newName = Path.GetFileNameWithoutExtension(destinationPath);
                var newGraphPath = $"{folder}/{newName}.{WitWeaverConversationGraph.AssetExtension}";
                if (newGraphPath != graphPath)
                {
                    newGraphPath = AssetDatabase.GenerateUniqueAssetPath(newGraphPath);
                    var error = AssetDatabase.MoveAsset(graphPath, newGraphPath);
                    if (!string.IsNullOrEmpty(error))
                        Debug.LogWarning($"[WitWeaver] Could not move companion graph with '{data.name}': {error}");
                }
            }

            return AssetMoveResult.DidNotMove; // Unity performs the conversation move itself.
        }

        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            if (!assetPath.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                return AssetDeleteResult.DidNotDelete;

            var data = AssetDatabase.LoadAssetAtPath<WitWeaverConversationData>(assetPath);
            if (data != null)
            {
                var graphPath = WitWeaverGraphBridge.ResolveGraphPath(data);
                if (graphPath != null)
                {
                    AssetDatabase.DeleteAsset(graphPath);
                    Debug.Log($"[WitWeaver] Deleted companion graph '{graphPath}' with conversation '{data.name}'.");
                }
            }

            return AssetDeleteResult.DidNotDelete; // Unity performs the conversation delete itself.
        }
    }

    internal static class WitWeaverGraphOpenHandler
    {
        /// <summary>Double-clicking a graph-authored conversation opens its graph.</summary>
        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceID, int line)
        {
            // The int-based callback signature is the stable one; converting to EntityId for
            // the lookup is deliberate (the operator obsoletion warns about future versions).
#pragma warning disable CS0618
            var target = EditorUtility.EntityIdToObject(instanceID);
#pragma warning restore CS0618
            if (target is WitWeaverConversationData data &&
                data.AuthoringMode == WitWeaverConversationData.ConversationAuthoringMode.Graph)
            {
                WitWeaverGraphBridge.OpenGraphFor(data);
                return true;
            }
            return false;
        }
    }
}
