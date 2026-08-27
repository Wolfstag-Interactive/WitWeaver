using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Character behaviour type that wraps a list of other behaviour types and selects between them
    /// based on a conversation index counter or an optional named condition.
    ///
    /// Each time <see cref="OnConversationBegin"/> is called the active behaviour is selected from
    /// the list using the current index (which increments automatically). Optionally a
    /// <see cref="ConditionKey"/> and <see cref="ISequencedPresenceCondition"/> can override
    /// the index-based selection.
    ///
    /// All lifecycle calls (<see cref="OnConversationBegin"/>, <see cref="OnConversationEnd"/>)
    /// are forwarded only to the currently active sub-behaviour.
    ///
    /// Use case: the same scene has multiple dialogue modes and requires different character
    /// placement strategies depending on which conversation is active.
    /// </summary>
    [CreateAssetMenu(fileName = "SequencedBehaviour", menuName = "ConvoCore/Character Behaviour/Sequenced Behaviour")]
    public class SequencedBehaviour : ConvoCoreCharacterBehaviour
    {
        [Tooltip("Ordered list of sub-behaviours to cycle through. Wraps around when exhausted.")]
        [SerializeField] private List<ConvoCoreCharacterBehaviour> _behaviours = new();

        [Tooltip("Optional named key used with ISequencedPresenceCondition for condition-based selection. " +
                 "Leave empty to use index-based (round-robin) selection only.")]
        [SerializeField] private string _conditionKey;

        [System.NonSerialized] private int _conversationCounter;
        [System.NonSerialized] private ConvoCoreCharacterBehaviour _active;

        public override void OnConversationBegin()
        {
            if (_behaviours == null || _behaviours.Count == 0)
            {
                Debug.LogWarning("[SequencedBehaviour] No sub-behaviours configured.");
                _active = null;
                return;
            }

            int index = _conversationCounter % _behaviours.Count;
            _conversationCounter++;

            _active = _behaviours[index];
            _active?.OnConversationBegin();
        }

        public override IConvoCoreCharacterDisplay ResolvePresence(
            PrefabCharacterRepresentationData representation,
            CharacterBehaviourContext context,
            ConvoCorePrefabRepresentationSpawner spawner)
        {
            if (_active == null)
            {
                Debug.LogWarning("[SequencedBehaviour] No active sub-behaviour. Was OnConversationBegin called?");
                return null;
            }

            return _active.ResolvePresence(representation, context, spawner);
        }

        public override void OnConversationEnd()
        {
            _active?.OnConversationEnd();
            _active = null;
        }
    }
}
