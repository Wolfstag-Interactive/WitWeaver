using UnityEngine;
using System;

namespace WolfstagInteractive.ConvoCore.Editor
{
    /// <summary>
    /// Extension point letting optional editor assemblies (e.g. the Graph Toolkit-based
    /// conversation graph editor) inject sections into the conversation inspector without the
    /// core editor assembly referencing them. If the optional assembly is absent or fails to
    /// compile, the inspector simply lacks that section — nothing else breaks.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreConversationInspectorHooks.html")]
    public static class ConvoCoreConversationInspectorHooks
    {
        /// <summary>Invoked near the top of the ConvoCoreConversationData inspector.</summary>
        public static event Action<ConvoCoreConversationData> DrawExtraSection;

        internal static void InvokeDrawExtraSection(ConvoCoreConversationData data)
            => DrawExtraSection?.Invoke(data);

        // ---- Graph tooling integration (registered by the ConvoCoreGraphEditor assembly) ----

        /// <summary>File extension (without dot) of conversation graph assets; null when graph tooling is unavailable.</summary>
        public static string GraphAssetExtension;

        /// <summary>Resolves a graph asset path to the conversation it is bound to.</summary>
        public static Func<string, ConvoCoreConversationData> ResolveGraphConversationByPath;

        /// <summary>Opens (creating if needed) the graph for a conversation in the Graph window.</summary>
        public static Action<ConvoCoreConversationData> OpenGraphForConversation;

        /// <summary>True when a project asset path points to a conversation graph and tooling is available.</summary>
        public static bool IsGraphAssetPath(string assetPath) =>
            !string.IsNullOrEmpty(GraphAssetExtension) &&
            !string.IsNullOrEmpty(assetPath) &&
            assetPath.EndsWith("." + GraphAssetExtension, StringComparison.OrdinalIgnoreCase);
    }
}
