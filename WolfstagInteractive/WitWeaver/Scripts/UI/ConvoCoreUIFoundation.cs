using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Used as a basis of any UI that can be assigned in the inspector that includes all the base functions needed
    /// to interoperate with the dialogue state machine.
    /// Representation-agnostic: subclasses are responsible for obtaining and using any
    /// <see cref="ConvoCorePrefabRepresentationSpawner"/> they need.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreUIFoundation.html")]
    public class ConvoCoreUIFoundation : MonoBehaviour, IUIFoundation
    {
        /// <summary>
        /// A named slot entry that maps a display name to a scene object reference.
        /// Populated on the UI Foundation prefab to define all slots available for character placement.
        /// </summary>
        [Serializable]
        public class DisplaySlotDefinition
        {
            [Tooltip("The name shown in the inspector dropdown for this slot.")]
            public string SlotName;
            [Tooltip("The GameObject in the canvas hierarchy that represents this slot.")]
            public GameObject SlotObject;
        }

        [Tooltip("Named display slots available for character placement. " +
                 "These populate the Display Slot dropdown in the dialogue line inspector.")]
        [SerializeField] private List<DisplaySlotDefinition> _displaySlots = new();
        public IReadOnlyList<DisplaySlotDefinition> DisplaySlots => _displaySlots;

        protected ConvoCore ConvoCoreInstance;
        protected ConvoCoreDialogueHistoryUI ConvoCoreDialogueHistoryUI;

        public event Action RequestAdvance;
        public event Action RequestReverse;

        protected void RaiseAdvance() => RequestAdvance?.Invoke();
        protected void RaiseReverse() => RequestReverse?.Invoke();

        public virtual void InitializeUI(ConvoCore convoCoreInstance)
        {
            ConvoCoreInstance = convoCoreInstance;

            ConvoCoreDialogueHistoryUI = !TryGetComponent(out ConvoCoreDialogueHistoryUI historyUI)
                ? gameObject.AddComponent<ConvoCoreDialogueHistoryUI>()
                : historyUI;
        }

        public virtual void UpdateDialogueUI(ConvoCoreConversationData.DialogueLineInfo dialogueLineInfo,
            string localizedText, string speakingCharacterName,
            CharacterRepresentationBase expressionMappingData, ConvoCoreCharacterProfileBaseData primaryProfile)
        {
        }

        /// <summary>
        /// Updates the UI when the language changes, primarily to replace the current dialogue text.
        /// </summary>
        public virtual void UpdateForLanguageChange(string localizedDialogueText, string newLanguageCode)
        {
        }

        /// <summary>
        /// Runs the <see cref="BaseExpressionAction"/> ScriptableObjects attached to the given
        /// expression on the representation. Call this from <see cref="UpdateDialogueUI"/> once a
        /// character's display has been resolved, after applying the built-in visual expression.
        ///
        /// The display side (<see cref="IConvoCoreCharacterDisplay.ApplyExpression"/>) only handles
        /// built-in visuals; the representation owns the action list, so custom UI subclasses must
        /// call this to give those actions a chance to run. Safe to call with a null
        /// <paramref name="display"/> (e.g. sprite representations) or a null/empty
        /// <paramref name="expressionId"/> — both are no-ops.
        /// </summary>
        /// <param name="representation">The representation whose expression actions should run.</param>
        /// <param name="expressionId">The expression being applied on this line.</param>
        /// <param name="lineIndex">The conversation line index, passed to the action context.</param>
        /// <param name="display">The resolved display for this character, or null if none.</param>
        protected void RunExpressionActions(
            CharacterRepresentationBase representation,
            string expressionId,
            int lineIndex,
            IConvoCoreCharacterDisplay display)
        {
            if (representation == null || string.IsNullOrEmpty(expressionId))
                return;

            representation.ApplyExpression(
                expressionId,
                ConvoCoreInstance,
                ConvoCoreInstance != null ? ConvoCoreInstance.GetCurrentConversationData() : null,
                lineIndex,
                display);
        }

        public virtual IEnumerator WaitForUserInput()
        {
            yield return null;
        }

        /// <summary>
        /// Present a set of player choices and wait for the player to select one.
        /// The base implementation auto-selects index 0 so conversations never hang
        /// if no choice UI has been implemented.
        /// </summary>
        public virtual IEnumerator PresentChoices(
            List<ConvoCoreConversationData.ChoiceOption> options,
            List<string> localizedLabels,
            ChoiceResult result)
        {
            result.SelectedIndex = 0;
            yield return null;
        }

        public virtual void Dispose()
        {
        }

        public virtual void HideDialogue()
        {
        }

        public virtual void DisplayDialogue(string text)
        {
        }
    }
}