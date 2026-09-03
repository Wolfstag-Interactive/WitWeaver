// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Context container passed to renderers on initialization.
    /// Allows renderer implementations to extract whatever they need.
    /// </summary>
    [System.Serializable]
    public struct DialogueHistoryRendererContext
    {
        public IDialogueHistoryOutput OutputHandler;

        public Color DefaultSpeakerColor;
        public int MaxEntries;
    }
}