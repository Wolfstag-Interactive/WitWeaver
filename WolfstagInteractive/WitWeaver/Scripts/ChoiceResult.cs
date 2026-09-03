// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Shared mutable reference used to pass a player's choice selection back from the UI
    /// to the WitWeaver runner after PresentChoices completes.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1ChoiceResult.html")]
    public class ChoiceResult
    {
        /// <summary>The index into the ChoiceOption list that the player selected. -1 means unresolved.</summary>
        public int SelectedIndex = -1;

        /// <summary>True once the player has made a selection.</summary>
        public bool IsResolved => SelectedIndex >= 0;
    }
}