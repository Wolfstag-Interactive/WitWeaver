using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Character behaviour type that spawns characters and keeps them following a scene Transform
    /// during the conversation.
    ///
    /// The follow target is resolved via <see cref="ConvoCoreSceneCharacterRegistry"/> by ID.
    /// A <see cref="ConvoCoreFollowTarget"/> MonoBehaviour is attached to each spawned
    /// character instance to drive the per-frame follow logic.
    ///
    /// On <see cref="OnConversationEnd"/>: releases all spawned instances via the spawner.
    ///
    /// Use case: companion NPC that walks beside the player during a conversation.
    /// </summary>
    [CreateAssetMenu(fileName = "FollowTargetBehaviour", menuName = "ConvoCore/Character Behaviour/Follow Target Behaviour")]
    public class FollowTargetBehaviour : ConvoCoreCharacterBehaviour
    {
        [System.Serializable]
        public class FollowSlotEntry
        {
            [Tooltip("Scene character ID (registered via ConvoCoreSceneCharacterRegistrant) of the target to follow.")]
            public string TargetSceneId;

            [Tooltip("World-space offset applied relative to the target's position.")]
            public Vector3 Offset;

            [Header("Animator (optional)")]
            [Tooltip("Animator parameter to set when the follow begins. Leave empty to skip.")]
            public string AnimatorParameterName;

            [Tooltip("Type of the animator parameter.")]
            public AnimatorParameterType ParameterType = AnimatorParameterType.Bool;

            [Tooltip("Value used when ParameterType is Float.")]
            public float FloatValue = 1f;

            [Tooltip("Value used when ParameterType is Int.")]
            public int IntValue = 1;

            [Tooltip("Value used when ParameterType is Bool.")]
            public bool BoolValue = true;

            [Tooltip("Animator trigger to fire when the conversation ends. Leave empty to skip.")]
            public string CompletionTriggerName;
        }

        [SerializeField] private List<FollowSlotEntry> _slots = new();

        [System.NonSerialized] private Dictionary<string, IConvoCoreCharacterDisplay> _cachedDisplays = new();
        [System.NonSerialized] private List<(GameObject go, string triggerName)> _animatorResets = new();
        [System.NonSerialized] private ConvoCorePrefabRepresentationSpawner _spawner;

        public override IConvoCoreCharacterDisplay ResolvePresence(
            PrefabCharacterRepresentationData representation,
            CharacterBehaviourContext context,
            ConvoCorePrefabRepresentationSpawner spawner)
        {
            _spawner = spawner;

            var cacheKey = !string.IsNullOrEmpty(context.CharacterId) ? context.CharacterId : representation.name;
            if (_cachedDisplays.TryGetValue(cacheKey, out var cached))
                return cached;

            if (context.CharacterIndex >= _slots.Count)
            {
                Debug.LogWarning($"[FollowTargetBehaviour] No slot defined for character index {context.CharacterIndex}.");
                return null;
            }

            var slot = _slots[context.CharacterIndex];

            // Resolve the follow target via registry.
            if (!spawner.TryGetSceneResident(slot.TargetSceneId, out var targetDisplay))
            {
                Debug.LogWarning($"[FollowTargetBehaviour] Follow target '{slot.TargetSceneId}' not found in registry.");
                return null;
            }

            // The target MonoBehaviour gives us its Transform.
            var targetMono = targetDisplay as MonoBehaviour;
            if (targetMono == null)
            {
                Debug.LogWarning($"[FollowTargetBehaviour] Target '{slot.TargetSceneId}' does not implement MonoBehaviour. Cannot resolve follow Transform.");
                return null;
            }

            // Spawn the character with no initial parent (follow component will position it).
            var display = spawner.SpawnAndBind(representation, context.ConfigurationEntryName, context.CharacterId, null);
            if (display == null) return null;

            var displayMono = display as MonoBehaviour;
            if (displayMono != null)
            {
                var follow = displayMono.gameObject.AddComponent<ConvoCoreFollowTarget>();
                follow.Initialize(targetMono.transform, slot.Offset);

                // Apply optional animator parameter at the start of following.
                if (!string.IsNullOrEmpty(slot.AnimatorParameterName))
                {
                    var animator = displayMono.GetComponentInChildren<Animator>();
                    if (animator != null)
                    {
                        switch (slot.ParameterType)
                        {
                            case AnimatorParameterType.Bool:    animator.SetBool(slot.AnimatorParameterName, slot.BoolValue); break;
                            case AnimatorParameterType.Int:     animator.SetInteger(slot.AnimatorParameterName, slot.IntValue); break;
                            case AnimatorParameterType.Float:   animator.SetFloat(slot.AnimatorParameterName, slot.FloatValue); break;
                            case AnimatorParameterType.Trigger: animator.SetTrigger(slot.AnimatorParameterName); break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(slot.CompletionTriggerName))
                    _animatorResets.Add((displayMono.gameObject, slot.CompletionTriggerName));
            }

            _cachedDisplays[cacheKey] = display;
            return display;
        }

        public override void OnConversationEnd()
        {
            // Fire completion triggers before releasing spawned instances.
            foreach (var (go, triggerName) in _animatorResets)
            {
                if (go == null) continue;
                var animator = go.GetComponentInChildren<Animator>();
                animator?.SetTrigger(triggerName);
            }
            _animatorResets.Clear();

            _spawner?.ReleaseAll();
            _cachedDisplays.Clear();
            _spawner = null;
        }
    }

    /// <summary>
    /// Added at runtime to spawned characters by <see cref="FollowTargetBehaviour"/>.
    /// Follows a target Transform with a fixed world-space offset each frame.
    /// </summary>
    public class ConvoCoreFollowTarget : MonoBehaviour
    {
        private Transform _target;
        private Vector3 _offset;

        public void Initialize(Transform target, Vector3 offset)
        {
            _target = target;
            _offset = offset;
        }

        private void LateUpdate()
        {
            if (_target != null)
                transform.position = _target.position + _offset;
        }
    }
}
