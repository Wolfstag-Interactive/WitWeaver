using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Prefab display component that maps expression display names to Animator parameters.
    /// Expression display names are matched against <see cref="PrefabExpressionMapping.DisplayName"/>
    /// on the bound <see cref="PrefabCharacterRepresentationData"/> asset at bind time, building
    /// a runtime GUID-to-parameter lookup. Warns by display name when a mapping is missing.
    ///
    /// Supports Bool, Int, Float, and Trigger parameter types.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreAnimatorDisplay.html")]
    public class ConvoCoreAnimatorDisplay : ConvoCoreCharacterDisplayBase
    {
        [Header("Animator")]
        [Tooltip("Animator to drive. Auto-resolved from this GameObject or its children if left empty.")]
        [SerializeField] private Animator _animator;

        [Header("Expression Mappings")]
        [Tooltip("Maps expression display names (matching the representation asset) to Animator parameters.")]
        [SerializeField] private List<AnimatorExpressionMapping> _expressionMappings = new();

        // Runtime lookup: expression GUID -> AnimatorExpressionMapping
        private readonly Dictionary<string, AnimatorExpressionMapping> _runtimeLookup = new();
        private CharacterRepresentationBase _lastBoundAsset;
        private PrefabCharacterRepresentationData _lastBoundRep;

        protected override void Awake()
        {
            base.Awake();
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
        }

        public override void BindRepresentation(CharacterRepresentationBase representationAsset)
        {
            if (representationAsset == _lastBoundAsset) return;
            _lastBoundAsset = representationAsset;
            _lastBoundRep = representationAsset as PrefabCharacterRepresentationData;

            _runtimeLookup.Clear();

            if (_lastBoundRep == null)
            {
                Debug.LogWarning($"[ConvoCoreAnimatorDisplay] Expected PrefabCharacterRepresentationData " +
                                 $"but received '{representationAsset?.GetType().Name}'.");
                return;
            }

            // Build GUID -> AnimatorExpressionMapping using display name as the join key.
            foreach (var exprMapping in _lastBoundRep.SharedExpressionMappings)
            {
                var animMapping = _expressionMappings.Find(m => m.ExpressionDisplayName == exprMapping.DisplayName);
                if (animMapping != null)
                    _runtimeLookup[exprMapping.ExpressionID] = animMapping;
                else
                    Debug.LogWarning($"[ConvoCoreAnimatorDisplay] No animator mapping found for expression " +
                                     $"'{exprMapping.DisplayName}' on '{gameObject.name}'. Add an entry to the Expression Mappings list.");
            }
        }

        public override void ApplyExpression(string expressionId)
        {
            if (_animator == null)
            {
                Debug.LogWarning("[ConvoCoreAnimatorDisplay] No Animator found.");
                return;
            }

            if (!_runtimeLookup.TryGetValue(expressionId, out var mapping))
            {
                Debug.LogWarning($"[ConvoCoreAnimatorDisplay] Expression GUID '{expressionId}' not found in runtime lookup. " +
                                 $"Was BindRepresentation called?");
                return;
            }

            switch (mapping.ParameterType)
            {
                case AnimatorParameterType.Bool:
                    _animator.SetBool(mapping.ParameterName, mapping.BoolValue);
                    break;
                case AnimatorParameterType.Int:
                    _animator.SetInteger(mapping.ParameterName, mapping.IntValue);
                    break;
                case AnimatorParameterType.Float:
                    _animator.SetFloat(mapping.ParameterName, mapping.FloatValue);
                    break;
                case AnimatorParameterType.Trigger:
                    _animator.SetTrigger(mapping.ParameterName);
                    break;
            }
        }
    }

    public enum AnimatorParameterType
    {
        Bool,
        Int,
        Float,
        Trigger
    }

    [System.Serializable]
    public class AnimatorExpressionMapping
    {
        [Tooltip("Must match the Display Name on the corresponding PrefabExpressionMapping in the representation asset.")]
        public string ExpressionDisplayName;

        [Tooltip("The Animator parameter to set when this expression is applied.")]
        public string ParameterName;

        public AnimatorParameterType ParameterType;

        [Tooltip("Value for Bool parameters.")]
        public bool BoolValue;

        [Tooltip("Value for Int parameters.")]
        public int IntValue;

        [Tooltip("Value for Float parameters.")]
        public float FloatValue;
    }
}
