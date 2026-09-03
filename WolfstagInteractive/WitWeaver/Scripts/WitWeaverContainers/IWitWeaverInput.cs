// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Input strategy interface. Implement to define how and which conversation(s) a
    /// <see cref="WitWeaver"/> runner should play. The two built-in implementations are
    /// <see cref="SingleConversationInput"/> for a single fixed conversation, and
    /// <see cref="ContainerInput"/> for container-driven selection.
    /// </summary>
    public interface IWitWeaverInput
    {
        void Play(MonoBehaviour host, IWitWeaverRunner runner);
    }

    /// <summary>
    /// Plays a single <see cref="WitWeaverConversationData"/> asset directly.
    /// The simplest input strategy — suitable for NPCs with one fixed conversation.
    /// </summary>
    [System.Serializable]
    public sealed class SingleConversationInput : IWitWeaverInput
    {
        public WitWeaverConversationData Conversation;

        public void Play(MonoBehaviour host, IWitWeaverRunner runner)
        {
            if (Conversation == null)
            {
                Debug.LogWarning("[WitWeaver] SingleConversationInput: Conversation is null.");
                return;
            }
            runner.PlayConversation(Conversation);
        }
    }

    
    /// <summary>
    /// Plays a conversation authored as a node graph. You assign the conversation asset only —
    /// its companion graph is an editor-only authoring artifact (stripped from builds), managed
    /// automatically alongside the conversation. At runtime the baked conversation plays.
    /// </summary>
    [System.Serializable]
    public sealed class GraphConversationInput : IWitWeaverInput
    {
        [Tooltip("A graph-authored conversation. Use 'Open Graph' below (or double-click the asset) to edit it.")]
        public WitWeaverConversationData Conversation;

        public void Play(MonoBehaviour host, IWitWeaverRunner runner)
        {
            if (Conversation == null)
            {
                Debug.LogWarning("[WitWeaver] GraphConversationInput: no conversation assigned.");
                return;
            }
            if (Conversation.AuthoringMode != WitWeaverConversationData.ConversationAuthoringMode.Graph)
                Debug.LogWarning($"[WitWeaver] GraphConversationInput: '{Conversation.name}' is not graph-authored. " +
                                 "It will play, but consider using the Single input mode instead.");
            runner.PlayConversation(Conversation);
        }
    }

    /// <summary>
    /// Plays one or more conversations from a <see cref="ConversationContainer"/> asset.
    /// Use this strategy when you want to pick a conversation by alias, play a playlist,
    /// or use random or weighted selection.
    /// </summary>
    [System.Serializable]
    public sealed class ContainerInput : IWitWeaverInput
    {
        public ConversationContainer Container;
        public string StartAlias;
        public bool? LoopOverride; // null = use container’s own Loop

        public void Play(MonoBehaviour host, IWitWeaverRunner runner)
        {
            if (Container == null)
            {
                Debug.LogWarning("[WitWeaver] No container assigned."); return;
            }
            host.StartCoroutine(
                ConversationContainerRuntime.Play(Container, runner, StartAlias, LoopOverride, hubSelector: null));
        }
    }
    

}