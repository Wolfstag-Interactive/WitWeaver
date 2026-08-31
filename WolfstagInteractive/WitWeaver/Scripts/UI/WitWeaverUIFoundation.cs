using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Used as a basis of any UI that can be assigned in the inspector that includes all the base functions needed
    /// to interoperate with the dialogue state machine.
    /// Representation-agnostic: subclasses are responsible for obtaining and using any
    /// <see cref="WitWeaverPrefabRepresentationSpawner"/> they need.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverUIFoundation.html")]
    public class WitWeaverUIFoundation : MonoBehaviour, IUIFoundation
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

        protected WitWeaver WitWeaverInstance;
        protected WitWeaverDialogueHistoryUI WitWeaverDialogueHistoryUI;

        public event Action RequestAdvance;
        public event Action RequestReverse;

        protected void RaiseAdvance() => RequestAdvance?.Invoke();
        protected void RaiseReverse() => RequestReverse?.Invoke();

        public virtual void InitializeUI(WitWeaver witWeaverInstance)
        {
            WitWeaverInstance = witWeaverInstance;

            WitWeaverDialogueHistoryUI = !TryGetComponent(out WitWeaverDialogueHistoryUI historyUI)
                ? gameObject.AddComponent<WitWeaverDialogueHistoryUI>()
                : historyUI;
        }

        /// <summary>
        /// Presents a dialogue line: renders it via <see cref="ApplyDialogueLine"/>, then runs any
        /// expression actions the rendering pass did not already run (see
        /// <see cref="RunExpressionActions"/>). Orchestration is fixed — subclasses override
        /// <see cref="ApplyDialogueLine"/>, not this method, so expression actions are guaranteed
        /// to run for every line regardless of which UI is active.
        /// </summary>
        public void UpdateDialogueUI(WitWeaverConversationData.DialogueLineInfo dialogueLineInfo,
            string localizedText, string speakingCharacterName,
            CharacterRepresentationBase expressionMappingData, WitWeaverCharacterProfileBaseData primaryProfile)
        {
            _currentLine = dialogueLineInfo;
            ApplyDialogueLine(dialogueLineInfo, localizedText, speakingCharacterName,
                expressionMappingData, primaryProfile);
            RunPendingExpressionActions(dialogueLineInfo);
            _currentLine = null;
        }

        /// <summary>
        /// Override point for rendering a dialogue line: set the text and speaker name, place and
        /// update character visuals. Called once per line presentation (including when the player
        /// navigates back to a previous line).
        ///
        /// Expression actions run automatically after this method returns, with a null
        /// <c>Display</c> in their context. Call <see cref="RunExpressionActions"/> yourself
        /// during rendering only when you can provide more — the resolved
        /// <see cref="IWitWeaverCharacterDisplay"/> on the prefab path, or exact timing relative
        /// to your visuals; anything you ran is not run again.
        /// </summary>
        protected virtual void ApplyDialogueLine(WitWeaverConversationData.DialogueLineInfo dialogueLineInfo,
            string localizedText, string speakingCharacterName,
            CharacterRepresentationBase expressionMappingData, WitWeaverCharacterProfileBaseData primaryProfile)
        {
        }

        /// <summary>
        /// Updates the UI when the language changes, primarily to replace the current dialogue text.
        /// </summary>
        public virtual void UpdateForLanguageChange(string localizedDialogueText, string newLanguageCode)
        {
        }

        // Expression actions already run during the current line presentation, keyed by the
        // character SLOT rather than the representation instance — so a UI that resolves a
        // different representation than the default path still suppresses the automatic pass for
        // that slot. RunExpressionActions records here; RunPendingExpressionActions skips
        // recorded slots and clears the set at the end of each presentation.
        private readonly HashSet<(int slotIndex, string expressionId, int lineIndex)>
            _ranExpressionActions = new();

        // Line currently being presented (set for the duration of UpdateDialogueUI); lets the
        // slot-compatibility path in RecordExpressionActionsRan attribute legacy calls.
        private WitWeaverConversationData.DialogueLineInfo _currentLine;

        /// <summary>
        /// Runs the <see cref="BaseExpressionAction"/> ScriptableObjects attached to the given
        /// expression on the representation. Calling this yourself is optional: the foundation
        /// runs any expression actions you did not run after <see cref="ApplyDialogueLine"/>
        /// returns (with a null display in the context). Call it during rendering when you can
        /// provide the resolved <see cref="IWitWeaverCharacterDisplay"/> for the prefab path, or
        /// need the actions to fire at an exact point relative to your visuals — whatever you run
        /// here is not run again by the automatic pass.
        ///
        /// Safe to call with a null <paramref name="display"/> (e.g. sprite representations) or a
        /// null/empty <paramref name="expressionId"/> — both are no-ops.
        /// </summary>
        /// <param name="representation">The representation whose expression actions should run.</param>
        /// <param name="expressionId">The expression being applied on this line.</param>
        /// <param name="lineIndex">The conversation line index, passed to the action context.</param>
        /// <param name="display">The resolved display for this character, or null if none.</param>
        /// <param name="slotIndex">Index of the character slot in the line's
        /// CharacterRepresentations list (0 = speaker). Pass it whenever it is in scope — it is
        /// what tells the automatic pass "this slot is handled". When omitted, the run is
        /// attributed to the first matching slot of the line currently being presented.</param>
        protected void RunExpressionActions(
            CharacterRepresentationBase representation,
            string expressionId,
            int lineIndex,
            IWitWeaverCharacterDisplay display,
            int slotIndex = -1)
        {
            if (representation == null || string.IsNullOrEmpty(expressionId))
                return;

            RecordExpressionActionsRan(expressionId, lineIndex, slotIndex);

            representation.ApplyExpression(
                expressionId,
                WitWeaverInstance,
                WitWeaverInstance != null ? WitWeaverInstance.GetCurrentConversationData() : null,
                lineIndex,
                display);
        }

        private void RecordExpressionActionsRan(string expressionId, int lineIndex, int slotIndex)
        {
            if (slotIndex >= 0)
            {
                _ranExpressionActions.Add((slotIndex, expressionId, lineIndex));
                return;
            }

            // Compatibility for callers that predate the slotIndex parameter: attribute the run
            // to the first not-yet-recorded slot on the current line using this expression.
            var line = _currentLine;
            if (line?.CharacterRepresentations == null)
                return;

            for (int i = 0; i < line.CharacterRepresentations.Count; i++)
            {
                if (line.CharacterRepresentations[i].SelectedExpressionId != expressionId)
                    continue;
                if (_ranExpressionActions.Add((i, expressionId, lineIndex)))
                    return;
            }
        }

        /// <summary>
        /// Runs expression actions for every visible character slot on the line whose actions
        /// were not already run during <see cref="ApplyDialogueLine"/>. This is what guarantees
        /// expression actions execute even in UI subclasses that never call
        /// <see cref="RunExpressionActions"/>; the fallback context carries a null <c>Display</c>.
        /// Slots are matched by index, so a UI that resolved a different representation for a
        /// slot than the default path still counts as having handled it.
        /// </summary>
        private void RunPendingExpressionActions(WitWeaverConversationData.DialogueLineInfo line)
        {
            var conversation = WitWeaverInstance != null ? WitWeaverInstance.GetCurrentConversationData() : null;
            if (line?.CharacterRepresentations == null || conversation == null)
            {
                _ranExpressionActions.Clear();
                return;
            }

            for (int i = 0; i < line.CharacterRepresentations.Count; i++)
            {
                var data = line.CharacterRepresentations[i];
                if (string.IsNullOrEmpty(data.SelectedExpressionId))
                    continue;

                int lineIndex = line.ConversationLineIndex;

                // Handled slots are skipped before any resolution work (or logging) happens.
                if (_ranExpressionActions.Contains((i, data.SelectedExpressionId, lineIndex)))
                    continue;

                var representation = conversation.ResolveRepresentation(
                    in data,
                    i == 0 ? line.characterID : null,
                    i == 0 ? RepresentationRole.Speaker : RepresentationRole.Visible);
                if (representation == null)
                    continue;

                RunExpressionActions(representation, data.SelectedExpressionId, lineIndex,
                    display: null, slotIndex: i);
            }

            _ranExpressionActions.Clear();
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
            List<WitWeaverConversationData.ChoiceOption> options,
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