// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Controls how a <see cref="WitWeaverAudioManifest"/> is authored and populated.
    /// </summary>
    public enum AudioManifestMode
    {
        /// <summary>
        /// Rows are populated by syncing from a <see cref="WitWeaverConversationData"/> asset.
        /// Used for text+audio conversations where YAML defines the structure.
        /// </summary>
        ConversationDriven,

        /// <summary>
        /// Lines are authored directly in this manifest.
        /// A <see cref="WitWeaverConversationData"/> asset is generated from it.
        /// Used for audio-only conversations where no YAML is needed.
        /// </summary>
        Standalone
    }

    /// <summary>
    /// Maps dialogue line IDs to audio clips or middleware references, supporting
    /// Unity AudioSource, FMOD, Wwise, and custom provider backends.
    /// Assign to <see cref="WitWeaverConversationData.AudioManifest"/> to enable voice playback.
    /// </summary>
    [CreateAssetMenu(menuName = "WitWeaver/Audio Manifest")]
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/")]
    public class WitWeaverAudioManifest : ScriptableObject
    {
        [Serializable]
        public class AudioEntry
        {
            /// <summary>
            /// Stable line ID. In ConversationDriven mode this is populated by sync.
            /// In Standalone mode this is auto-generated on row creation and never edited manually.
            /// </summary>
            public string LineID;

            /// <summary>
            /// Character ID for this line. In ConversationDriven mode populated by sync.
            /// In Standalone mode set by the developer via character dropdown in the inspector.
            /// </summary>
            public string CharacterID;

            /// <summary>
            /// BCP-47 language code (e.g. "en", "fr", "ja"). Empty means language-agnostic
            /// and will match any locale that has no exact entry.
            /// </summary>
            public string Language;

            /// <summary>
            /// AudioClip for Unity AudioSource playback. Used when the manifest
            /// <see cref="WitWeaverAudioManifest.Backend"/> is <see cref="AudioBackend.UnityAudioSource"/>.
            /// Drag an AudioClip directly here — no wrapper ScriptableObject needed.
            /// </summary>
            public AudioClip Clip;

            /// <summary>
            /// Middleware event identifier. For FMOD: full event path e.g.
            /// <c>"event:/VO/CharA/Line001"</c>. For Wwise: event name e.g.
            /// <c>"VO_CharA_Intro_01"</c>. For custom backends: any string your provider
            /// interprets. Ignored by the <see cref="AudioBackend.UnityAudioSource"/> backend.
            /// </summary>
            public string EventKey;

            /// <summary>
            /// Optional. Assign a <see cref="WitWeaverAudioReference"/> subtype for custom middleware
            /// integrations, or to share a single reference asset across multiple lines.
            /// When assigned, takes priority over <see cref="Clip"/> at runtime.
            /// Not shown in the inspector for <see cref="AudioBackend.UnityAudioSource"/> mode
            /// unless explicitly expanded.
            /// </summary>
            public WitWeaverAudioReference Reference;
        }

        [Tooltip("Selects the audio playback backend. Controls which fields are shown in the inspector and how audio is resolved at runtime.")]
        public AudioBackend Backend = AudioBackend.UnityAudioSource;

        public AudioManifestMode Mode = AudioManifestMode.ConversationDriven;

        [Tooltip("In ConversationDriven mode, the conversation this manifest is derived from.")]
        public WitWeaverConversationData SourceConversation;

        public List<AudioEntry> Entries = new List<AudioEntry>();

        /// <summary>
        /// Resolve a <see cref="WitWeaverAudioReference"/> for a given line ID and language.
        /// Only returns a value when an entry has a non-null <see cref="AudioEntry.Reference"/> assigned.
        /// Tries exact locale match first, then language-agnostic fallback.
        /// For the common Unity AudioSource path, use <see cref="ResolveClip"/> instead.
        /// </summary>
        public WitWeaverAudioReference Resolve(string lineID, string language)
        {
            if (Entries == null) return null;

            // Pass 1: exact locale match
            foreach (var entry in Entries)
            {
                if (entry.LineID == lineID &&
                    string.Equals(entry.Language, language, StringComparison.OrdinalIgnoreCase) &&
                    entry.Reference != null)
                    return entry.Reference;
            }

            // Pass 2: language-agnostic fallback
            foreach (var entry in Entries)
            {
                if (entry.LineID == lineID &&
                    string.IsNullOrEmpty(entry.Language) &&
                    entry.Reference != null)
                    return entry.Reference;
            }

            return null;
        }

        /// <summary>
        /// Resolve the middleware event key string for a given line ID and language.
        /// Used by FMOD, Wwise, and Custom backend paths. Tries exact locale match first,
        /// then language-agnostic fallback. Returns <c>null</c> if no matching entry has an
        /// <see cref="AudioEntry.EventKey"/> assigned.
        /// </summary>
        public string ResolveEventKey(string lineID, string language)
        {
            if (Entries == null) return null;

            // Pass 1: exact locale match
            foreach (var entry in Entries)
            {
                if (entry.LineID == lineID &&
                    string.Equals(entry.Language, language, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(entry.EventKey))
                    return entry.EventKey;
            }

            // Pass 2: language-agnostic fallback
            foreach (var entry in Entries)
            {
                if (entry.LineID == lineID &&
                    string.IsNullOrEmpty(entry.Language) &&
                    !string.IsNullOrEmpty(entry.EventKey))
                    return entry.EventKey;
            }

            return null;
        }

        /// <summary>
        /// Resolve an <see cref="AudioClip"/> for a given line ID and language.
        /// Used by the Unity AudioSource backend path. Tries exact locale match first,
        /// then language-agnostic fallback.
        /// </summary>
        public AudioClip ResolveClip(string lineID, string language)
        {
            if (Entries == null) return null;

            // Pass 1: exact locale match
            foreach (var entry in Entries)
            {
                if (entry.LineID == lineID &&
                    string.Equals(entry.Language, language, StringComparison.OrdinalIgnoreCase) &&
                    entry.Clip != null)
                    return entry.Clip;
            }

            // Pass 2: language-agnostic fallback
            foreach (var entry in Entries)
            {
                if (entry.LineID == lineID &&
                    string.IsNullOrEmpty(entry.Language) &&
                    entry.Clip != null)
                    return entry.Clip;
            }

            return null;
        }
    }
}
