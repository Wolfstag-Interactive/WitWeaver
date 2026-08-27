using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.GraphEditor
{
    /// <summary>
    /// Makes the companion graph asset behave like part of the conversation asset: it is renamed,
    /// moved, and deleted together with its <see cref="ConvoCoreConversationData"/>, and
    /// double-clicking a graph-authored conversation opens the graph directly. Users never manage
    /// the graph file by hand.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1GraphEditor_1_1ConvoCoreGraphAssetProcessor.html")]
    internal sealed class ConvoCoreGraphAssetProcessor : AssetModificationProcessor
    {
        private static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
        {
            if (!sourcePath.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                return AssetMoveResult.DidNotMove;

            var data = AssetDatabase.LoadAssetAtPath<ConvoCoreConversationData>(sourcePath);
            if (data == null)
                return AssetMoveResult.DidNotMove;

            var graphPath = ConvoCoreGraphBridge.ResolveGraphPath(data);
            if (graphPath != null)
            {
                var folder = Path.GetDirectoryName(destinationPath)?.Replace('\\', '/') ?? "Assets";
                var newName = Path.GetFileNameWithoutExtension(destinationPath);
                var newGraphPath = $"{folder}/{newName}.{ConvoCoreConversationGraph.AssetExtension}";
                if (newGraphPath != graphPath)
                {
                    newGraphPath = AssetDatabase.GenerateUniqueAssetPath(newGraphPath);
                    var error = AssetDatabase.MoveAsset(graphPath, newGraphPath);
                    if (!string.IsNullOrEmpty(error))
                        Debug.LogWarning($"[ConvoCore] Could not move companion graph with '{data.name}': {error}");
                }
            }

            return AssetMoveResult.DidNotMove; // Unity performs the conversation move itself.
        }

        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            if (!assetPath.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                return AssetDeleteResult.DidNotDelete;

            var data = AssetDatabase.LoadAssetAtPath<ConvoCoreConversationData>(assetPath);
            if (data != null)
            {
                var graphPath = ConvoCoreGraphBridge.ResolveGraphPath(data);
                if (graphPath != null)
                {
                    AssetDatabase.DeleteAsset(graphPath);
                    Debug.Log($"[ConvoCore] Deleted companion graph '{graphPath}' with conversation '{data.name}'.");
                }
            }

            return AssetDeleteResult.DidNotDelete; // Unity performs the conversation delete itself.
        }
    }

    internal static class ConvoCoreGraphOpenHandler
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
            if (target is ConvoCoreConversationData data &&
                data.AuthoringMode == ConvoCoreConversationData.ConversationAuthoringMode.Graph)
            {
                ConvoCoreGraphBridge.OpenGraphFor(data);
                return true;
            }
            return false;
        }
    }
}
