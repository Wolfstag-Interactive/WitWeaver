using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Character behaviour type for scene-resident characters that move to an authored offset position
    /// when a conversation begins and return to their original position when it ends.
    ///
    /// Characters are resolved via <see cref="ConvoCoreSceneCharacterRegistry"/>.
    /// Each slot entry specifies a world-space target position and optionally a duration.
    /// When duration is zero the move is instant; otherwise a <see cref="ConvoCoreTransformLerp"/>
    /// MonoBehaviour is added to the character's GameObject to run a coroutine.
    ///
    /// Use case: NPC turns to face the player, walks to a conversation spot, etc.
    /// </summary>
    [CreateAssetMenu(fileName = "TransformLerpBehaviour", menuName = "ConvoCore/Character Behaviour/Transform Lerp Behaviour")]
    public class TransformLerpBehaviour : ConvoCoreCharacterBehaviour
    {
        [System.Serializable]
        public class LerpSlotEntry
        {
            [Tooltip("Scene character ID to move (registered via ConvoCoreSceneCharacterRegistrant).")]
            public string SceneCharacterId;

            [Tooltip("Target world position for conversation start.")]
            public Vector3 TargetPosition;

            [Tooltip("Target world rotation (Euler) for conversation start.")]
            public Vector3 TargetEulerRotation;

            [Tooltip("Move duration in seconds. 0 = instant.")]
            public float Duration = 0.4f;

            [Header("Animator (optional)")]
            [Tooltip("Animator parameter to set at the start of movement. Leave empty to skip.")]
            public string AnimatorParameterName;

            [Tooltip("Type of the animator parameter.")]
            public AnimatorParameterType ParameterType = AnimatorParameterType.Bool;

            [Tooltip("Value used when ParameterType is Float.")]
            public float FloatValue = 1f;

            [Tooltip("Value used when ParameterType is Int.")]
            public int IntValue = 1;

            [Tooltip("Value used when ParameterType is Bool.")]
            public bool BoolValue = true;

            [Tooltip("Animator trigger to fire when movement completes. Leave empty to skip.")]
            public string CompletionTriggerName;
        }

        [SerializeField] private List<LerpSlotEntry> _slots = new();

        // Runtime state
        [System.NonSerialized] private List<(Transform t, Vector3 origPos, Quaternion origRot)> _originals = new();
        [System.NonSerialized] private Dictionary<string, IConvoCoreCharacterDisplay> _cachedDisplays = new();

        public override void OnConversationBegin()
        {
            _originals.Clear();
            _cachedDisplays.Clear();
        }

        public override IConvoCoreCharacterDisplay ResolvePresence(
            PrefabCharacterRepresentationData representation,
            CharacterBehaviourContext context,
            ConvoCorePrefabRepresentationSpawner spawner)
        {
            // Use CharacterId as cache key when available; fall back to representation name.
            var cacheKey = !string.IsNullOrEmpty(context.CharacterId) ? context.CharacterId : representation.name;

            if (_cachedDisplays.TryGetValue(cacheKey, out var cached))
                return cached;

            // Find the matching slot: try by CharacterId first, then fall back to index.
            LerpSlotEntry slot = null;
            if (!string.IsNullOrEmpty(context.CharacterId))
                slot = _slots.Find(s => s.SceneCharacterId == context.CharacterId);

            if (slot == null && context.CharacterIndex < _slots.Count)
                slot = _slots[context.CharacterIndex];

            if (slot == null)
            {
                Debug.LogWarning($"[TransformLerpBehaviour] No slot found for character '{context.CharacterId}' (index {context.CharacterIndex}).");
                return null;
            }

            if (!spawner.TryGetSceneResident(slot.SceneCharacterId, out var display))
            {
                Debug.LogWarning($"[TransformLerpBehaviour] Scene character '{slot.SceneCharacterId}' not found in registry.");
                return null;
            }

            var mono = display as MonoBehaviour;
            if (mono == null)
            {
                Debug.LogWarning($"[TransformLerpBehaviour] Display for '{slot.SceneCharacterId}' is not a MonoBehaviour.");
                return null;
            }

            var t = mono.transform;
            _originals.Add((t, t.position, t.rotation));

            // Apply optional animator parameter at the start of movement.
            ApplyAnimatorParameter(mono.gameObject, slot);

            if (slot.Duration <= 0f)
            {
                t.position = slot.TargetPosition;
                t.rotation = Quaternion.Euler(slot.TargetEulerRotation);
                FireAnimatorTrigger(mono.gameObject, slot.CompletionTriggerName);
            }
            else
            {
                var lerp = mono.gameObject.AddComponent<ConvoCoreTransformLerp>();
                lerp.MoveTo(slot.TargetPosition, Quaternion.Euler(slot.TargetEulerRotation), slot.Duration,
                    mono.gameObject, slot.CompletionTriggerName);
            }

            _cachedDisplays[cacheKey] = display;
            return display;
        }

        public override void OnConversationEnd()
        {
            foreach (var (t, origPos, origRot) in _originals)
            {
                if (t == null) continue;

                var lerp = t.GetComponent<ConvoCoreTransformLerp>();
                if (lerp != null)
                {
                    lerp.MoveTo(origPos, origRot, lerp.Duration, null, null);
                }
                else
                {
                    t.position = origPos;
                    t.rotation = origRot;
                }
            }

            _originals.Clear();
            _cachedDisplays.Clear();
        }

        private static void ApplyAnimatorParameter(GameObject go, LerpSlotEntry slot)
        {
            if (string.IsNullOrEmpty(slot.AnimatorParameterName)) return;
            var animator = go.GetComponentInChildren<Animator>();
            if (animator == null) return;
            switch (slot.ParameterType)
            {
                case AnimatorParameterType.Bool:    animator.SetBool(slot.AnimatorParameterName, slot.BoolValue); break;
                case AnimatorParameterType.Int:     animator.SetInteger(slot.AnimatorParameterName, slot.IntValue); break;
                case AnimatorParameterType.Float:   animator.SetFloat(slot.AnimatorParameterName, slot.FloatValue); break;
                case AnimatorParameterType.Trigger: animator.SetTrigger(slot.AnimatorParameterName); break;
            }
        }

        private static void FireAnimatorTrigger(GameObject go, string triggerName)
        {
            if (string.IsNullOrEmpty(triggerName)) return;
            var animator = go.GetComponentInChildren<Animator>();
            animator?.SetTrigger(triggerName);
        }
    }

    /// <summary>
    /// Added at runtime by <see cref="TransformLerpBehaviour"/> to smoothly move a character's
    /// Transform to a target position and rotation over a given duration.
    /// Self-destructs when the move completes.
    /// </summary>
    public class ConvoCoreTransformLerp : MonoBehaviour
    {
        public float Duration { get; private set; }

        private Coroutine _routine;

        public void MoveTo(Vector3 targetPos, Quaternion targetRot, float duration,
            GameObject animatorTarget, string completionTrigger)
        {
            Duration = duration;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(LerpRoutine(targetPos, targetRot, duration, animatorTarget, completionTrigger));
        }

        private IEnumerator LerpRoutine(Vector3 targetPos, Quaternion targetRot, float duration,
            GameObject animatorTarget, string completionTrigger)
        {
            var startPos = transform.position;
            var startRot = transform.rotation;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            transform.position = targetPos;
            transform.rotation = targetRot;

            if (!string.IsNullOrEmpty(completionTrigger) && animatorTarget != null)
            {
                var animator = animatorTarget.GetComponentInChildren<Animator>();
                animator?.SetTrigger(completionTrigger);
            }

            _routine = null;
            Destroy(this);
        }
    }
}
