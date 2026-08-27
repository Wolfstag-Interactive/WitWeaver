using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Shared mutable reference used to pass a player's choice selection back from the UI
    /// to the ConvoCore runner after PresentChoices completes.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ChoiceResult.html")]
    public class ChoiceResult
    {
        /// <summary>The index into the ChoiceOption list that the player selected. -1 means unresolved.</summary>
        public int SelectedIndex = -1;

        /// <summary>True once the player has made a selection.</summary>
        public bool IsResolved => SelectedIndex >= 0;
    }
}