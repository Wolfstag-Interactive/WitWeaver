// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;
using System;

namespace WolfstagInteractive.WitWeaver.Editor
{
    /// <summary>
    /// Extension point letting optional editor assemblies (e.g. the Graph Toolkit-based
    /// conversation graph editor) inject sections into the conversation inspector without the
    /// core editor assembly referencing them. If the optional assembly is absent or fails to
    /// compile, the inspector simply lacks that section — nothing else breaks.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverConversationInspectorHooks.html")]
    public static class WitWeaverConversationInspectorHooks
    {
        /// <summary>Invoked near the top of the WitWeaverConversationData inspector.</summary>
        public static event Action<WitWeaverConversationData> DrawExtraSection;

        internal static void InvokeDrawExtraSection(WitWeaverConversationData data)
            => DrawExtraSection?.Invoke(data);

        // ---- Graph tooling integration (registered by the WitWeaverGraphEditor assembly) ----

        /// <summary>File extension (without dot) of conversation graph assets; null when graph tooling is unavailable.</summary>
        public static string GraphAssetExtension;

        /// <summary>Resolves a graph asset path to the conversation it is bound to.</summary>
        public static Func<string, WitWeaverConversationData> ResolveGraphConversationByPath;

        /// <summary>Opens (creating if needed) the graph for a conversation in the Graph window.</summary>
        public static Action<WitWeaverConversationData> OpenGraphForConversation;

        /// <summary>True when a project asset path points to a conversation graph and tooling is available.</summary>
        public static bool IsGraphAssetPath(string assetPath) =>
            !string.IsNullOrEmpty(GraphAssetExtension) &&
            !string.IsNullOrEmpty(assetPath) &&
            assetPath.EndsWith("." + GraphAssetExtension, StringComparison.OrdinalIgnoreCase);
    }
}
