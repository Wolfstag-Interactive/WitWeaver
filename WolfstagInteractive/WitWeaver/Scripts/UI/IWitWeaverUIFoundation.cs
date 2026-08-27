using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Contract between the ConvoCore runner and a dialogue UI implementation.
    /// Implement (or extend <see cref="ConvoCoreUIFoundation"/>) to display dialogue lines,
    /// present player choices, and signal advance/reverse input back to the runner.
    /// </summary>
    public interface IUIFoundation
    {
        /// <summary>
        /// Initializes the UI builder and sets up required bindings.
        /// </summary>
        /// <param name="convoCoreInstance">The ConvoCore instance.</param>
        void InitializeUI(ConvoCore convoCoreInstance);

        /// <summary>
        /// Updates the displayed UI elements based on the ConvoCore instances current state.
        /// </summary>
        /// <param name="dialogueLineInfo">Details of the current dialogue line.</param>
        /// <param name="localizedText">The current localized text to be displayed</param>
        /// <param name="speakingCharacterName">The name of the speaking character</param>
        /// <param name="expressionMappingData">The speaking characters portrait</param>
        /// <param name="primaryProfile">The primary profile data</param>
        public void UpdateDialogueUI(ConvoCoreConversationData.DialogueLineInfo dialogueLineInfo, string localizedText,
            string speakingCharacterName, CharacterRepresentationBase expressionMappingData, ConvoCoreCharacterProfileBaseData primaryProfile);

        /// <summary>
        /// Updates the UI when language changes, primarily to replace the current dialogue text
        /// </summary>
        /// <param name="localizedDialogueText">The new localized dialogue text to display</param>
        /// <param name="newLanguageCode">The new language code that was applied</param>
        public virtual void UpdateForLanguageChange(string localizedDialogueText, string newLanguageCode)
        {
        }


        /// <summary>
        /// Wait until the UI signals that the user has provided input.
        /// The actual waiting is delegated to the UI implementation.
        /// </summary>
        /// <returns></returns>
        public IEnumerator WaitForUserInput();

        /// <summary>
        /// Present a set of player choices and wait for the player to select one.
        /// Implementations must set result.SelectedIndex before the coroutine completes.
        /// The base implementation auto-selects index 0 so the runner never hangs
        /// if no choice UI has been wired up.
        /// </summary>
        /// <param name="options">The choice options defined on the dialogue line.</param>
        /// <param name="localizedLabels">Pre-resolved display strings, one per option.</param>
        /// <param name="result">Shared result object; set SelectedIndex to signal completion.</param>
        public virtual IEnumerator PresentChoices(
            List<ConvoCoreConversationData.ChoiceOption> options,
            List<string> localizedLabels,
            ChoiceResult result)
        {
            result.SelectedIndex = 0;
            yield return null;
        }

        /// <summary>
        /// Handles cleanup when the UI builder is no longer needed.
        /// </summary>
        void Dispose();

    }
}