// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    public enum ConversationContainerMode
    {
        Playlist,
        Selector
    }

    public enum ConversationSelectionMode
    {
        First,
        Sequential,
        Random,
        WeightedRandom,
    }

    public readonly struct ConversationBranchResult
    {
        public readonly WitWeaverConversationData Conversation;
        public readonly int StartLineIndex;

        public ConversationBranchResult(WitWeaverConversationData conversation, int startLineIndex)
        {
            Conversation = conversation;
            StartLineIndex = startLineIndex;
        }
    }

    /// <summary>
    /// ScriptableObject that groups one or more <see cref="WitWeaverConversationData"/> assets
    /// into a single addressable unit for branching and playback. Supports two modes:
    /// <b>Playlist</b> (play entries in sequence with optional looping) and
    /// <b>Selector</b> (pick one entry by alias, first match, random, sequential, or weighted random).
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/classWolfstagInteractive_1_1WitWeaver_1_1ConversationContainer.html")]
    [CreateAssetMenu(menuName = "WitWeaver/Conversation Container")]
    public sealed class ConversationContainer : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Alias;
            public WitWeaverConversationData ConversationData;
            public bool Enabled = true;

            [Tooltip("Only used when this container is played as a Playlist.")]
            public float DelayAfterEndSeconds = 0f;

            public string[] Tags;

            [Min(0f)]
            [Tooltip("Only used in WeightedRandom selection mode.")]
            public float Weight = 1f;

            [Min(0)]
            [Tooltip("Legacy start line by list index. Superseded by StartLineID; only used when StartLineID is empty.")]
            public int StartLineIndex = 0;

            [Tooltip("Stable LineID of the line to start from when this entry is chosen as a branch target. " +
                     "Survives line reordering and YAML reimports. Empty = start from the first line " +
                     "(or the legacy StartLineIndex when set).")]
            public string StartLineID;
        }

        [Header("Common")]
        public ConversationContainerMode ContainerMode = ConversationContainerMode.Playlist;

        [Tooltip("Used when ContainerMode is Selector.")]
        public ConversationSelectionMode SelectionMode = ConversationSelectionMode.First;

        public List<Entry> Conversations = new();

        [Header("Playlist Mode")]
        public bool Loop = false;
        public string DefaultStart;

        private static readonly Dictionary<ConversationContainer, int> _sequentialIndices =
            new Dictionary<ConversationContainer, int>();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSequentialIndices() => _sequentialIndices.Clear();

        public ConversationBranchResult ResolveForBranch(IConversationContext context, string aliasOrName = null)
        {
            if (Conversations == null || Conversations.Count == 0)
                return default;

            var candidates = Conversations
                .Where(e => e != null && e.Enabled && e.ConversationData != null)
                .ToList();

            if (!string.IsNullOrEmpty(aliasOrName))
            {
                var filtered = candidates.Where(e =>
                        (!string.IsNullOrEmpty(e.Alias) &&
                         e.Alias.Equals(aliasOrName, StringComparison.OrdinalIgnoreCase)) ||
                        (e.ConversationData != null &&
                         e.ConversationData.name.Equals(aliasOrName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (filtered.Count > 0)
                    candidates = filtered;
            }

            if (candidates.Count == 0)
                return default;

            var mode = SelectionMode;
            if (ContainerMode == ConversationContainerMode.Playlist)
            {
                Debug.LogWarning($"[WitWeaver] ConversationContainer '{name}' used as branch target but is in Playlist mode. Treating as 'First' selector.");
                mode = ConversationSelectionMode.First;
            }

            Entry chosen;
            switch (mode)
            {
                case ConversationSelectionMode.First:
                    chosen = candidates[0];
                    break;

                case ConversationSelectionMode.Random:
                    chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                    break;

                case ConversationSelectionMode.Sequential:
                    chosen = ResolveSequentialEntry(candidates);
                    break;

                case ConversationSelectionMode.WeightedRandom:
                    chosen = ResolveWeightedRandomEntry(candidates);
                    break;

                default:
                    chosen = candidates[0];
                    break;
            }

            if (chosen == null || chosen.ConversationData == null)
                return default;

            int startIndex;
            if (!string.IsNullOrEmpty(chosen.StartLineID))
            {
                // Stable LineID target — immune to line reordering and YAML reimports.
                startIndex = chosen.ConversationData.GetLineIndexById(chosen.StartLineID);
                if (startIndex < 0)
                {
                    Debug.LogWarning(
                        $"[WitWeaver] Container '{name}': StartLineID '{chosen.StartLineID}' not found in " +
                        $"'{chosen.ConversationData.name}'. Starting from the beginning.");
                    startIndex = 0;
                }
            }
            else
            {
                startIndex = Mathf.Max(0, chosen.StartLineIndex);

                // Clamp against actual line count when possible
                var lines = chosen.ConversationData.DialogueLines;
                if (lines != null && lines.Count > 0)
                    startIndex = Mathf.Clamp(startIndex, 0, lines.Count - 1);
                else
                    startIndex = 0;
            }

            return new ConversationBranchResult(chosen.ConversationData, startIndex);
        }

        // ----- GUID-based lookups -----

        /// <summary>Returns the <see cref="WitWeaverConversationData"/> whose ConversationGuid matches,
        /// or null if not found.</summary>
        public WitWeaverConversationData GetByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid) || Conversations == null) return null;
            for (int i = 0; i < Conversations.Count; i++)
            {
                var data = Conversations[i]?.ConversationData;
                if (data != null && data.ConversationGuid == guid)
                    return data;
            }
            return null;
        }

        /// <summary>Returns the index of the entry whose ConversationData reference equals
        /// <paramref name="data"/>, or -1.</summary>
        public int IndexOf(WitWeaverConversationData data)
        {
            if (data == null || Conversations == null) return -1;
            for (int i = 0; i < Conversations.Count; i++)
            {
                if (Conversations[i]?.ConversationData == data) return i;
            }
            return -1;
        }

        /// <summary>Returns the index of the entry whose ConversationData.ConversationGuid matches,
        /// or -1.</summary>
        public int IndexOfGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid) || Conversations == null) return -1;
            for (int i = 0; i < Conversations.Count; i++)
            {
                var data = Conversations[i]?.ConversationData;
                if (data != null && data.ConversationGuid == guid) return i;
            }
            return -1;
        }

        private Entry ResolveSequentialEntry(List<Entry> candidates)
        {
            if (!_sequentialIndices.TryGetValue(this, out var idx))
                idx = 0;

            if (candidates.Count == 0)
                return null;

            if (idx >= candidates.Count)
                idx = candidates.Count - 1;

            var entry = candidates[idx];
            idx = (idx + 1) % candidates.Count;
            _sequentialIndices[this] = idx;

            return entry;
        }

        private static Entry ResolveWeightedRandomEntry(List<Entry> candidates)
        {
            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
                total += Mathf.Max(0f, candidates[i].Weight);

            if (total <= 0f)
                return candidates[0];

            float roll = UnityEngine.Random.value * total;
            float accum = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                accum += Mathf.Max(0f, candidates[i].Weight);
                if (roll <= accum)
                    return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }
    }
}