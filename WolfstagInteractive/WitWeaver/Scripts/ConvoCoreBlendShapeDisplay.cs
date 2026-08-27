using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Prefab display component that maps expression display names to SkinnedMeshRenderer
    /// blend shape indices and target weights. A single expression can drive multiple blend
    /// shapes simultaneously. Supports optional smooth blending over time.
    ///
    /// Expression display names are matched against <see cref="PrefabExpressionMapping.DisplayName"/>
    /// on the bound <see cref="PrefabCharacterRepresentationData"/> asset at bind time, building
    /// a runtime GUID-to-blend-shape lookup.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreBlendShapeDisplay.html")]
    public class ConvoCoreBlendShapeDisplay : ConvoCoreCharacterDisplayBase
    {
        [Header("Renderer")]
        [Tooltip("SkinnedMeshRenderer whose blend shapes are driven by expressions. Auto-resolved if left empty.")]
        [SerializeField] private SkinnedMeshRenderer _renderer;

        [Header("Blend Settings")]
        [Tooltip("Time in seconds to transition between blend shape weights. 0 = instant.")]
        [SerializeField] private float _transitionDuration = 0.2f;

        [Header("Neutral Reset")]
        [Tooltip("Blend shape indices that are reset to zero before each expression is applied. " +
                 "Use 'Populate Neutral Reset Indices From Mappings' from the context menu to fill this automatically.")]
        [SerializeField] private List<int> _neutralResetIndices = new();

        [Header("Expression Mappings")]
        [Tooltip("Maps expression display names (matching the representation asset) to one or more blend shape targets.")]
        [SerializeField] private List<BlendShapeExpressionMapping> _expressionMappings = new();

        private readonly Dictionary<string, BlendShapeExpressionMapping> _runtimeLookup = new();
        private CharacterRepresentationBase _lastBoundAsset;
        private PrefabCharacterRepresentationData _lastBoundRep;
        private Coroutine _blendCoroutine;

        protected override void Awake()
        {
            base.Awake();
            if (_renderer == null)
                _renderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        public override void BindRepresentation(CharacterRepresentationBase representationAsset)
        {
            if (representationAsset == _lastBoundAsset) return;
            _lastBoundAsset = representationAsset;
            _lastBoundRep = representationAsset as PrefabCharacterRepresentationData;

            _runtimeLookup.Clear();

            if (_lastBoundRep == null)
            {
                Debug.LogWarning($"[ConvoCoreBlendShapeDisplay] Expected PrefabCharacterRepresentationData " +
                                 $"but received '{representationAsset?.GetType().Name}'.");
                return;
            }

            foreach (var exprMapping in _lastBoundRep.SharedExpressionMappings)
            {
                var bsMapping = _expressionMappings.Find(m => m.ExpressionDisplayName == exprMapping.DisplayName);
                if (bsMapping != null)
                    _runtimeLookup[exprMapping.ExpressionID] = bsMapping;
                else
                    Debug.LogWarning($"[ConvoCoreBlendShapeDisplay] No blend shape mapping found for expression " +
                                     $"'{exprMapping.DisplayName}' on '{gameObject.name}'.");
            }
        }

        public override void ApplyExpression(string expressionId)
        {
            if (_renderer == null)
            {
                Debug.LogWarning("[ConvoCoreBlendShapeDisplay] No SkinnedMeshRenderer found.");
                return;
            }

            if (!_runtimeLookup.TryGetValue(expressionId, out var mapping))
            {
                Debug.LogWarning($"[ConvoCoreBlendShapeDisplay] Expression GUID '{expressionId}' not found in runtime lookup.");
                return;
            }

            if (_blendCoroutine != null)
                StopCoroutine(_blendCoroutine);

            if (_transitionDuration <= 0f)
            {
                // Instant: reset neutrals then apply targets.
                foreach (var index in _neutralResetIndices)
                    _renderer.SetBlendShapeWeight(index, 0f);

                if (mapping.Targets != null)
                    foreach (var target in mapping.Targets)
                        _renderer.SetBlendShapeWeight(target.BlendShapeIndex, target.TargetWeight);
            }
            else
            {
                _blendCoroutine = StartCoroutine(BlendTo(mapping));
            }
        }

        private IEnumerator BlendTo(BlendShapeExpressionMapping mapping)
        {
            // Capture start weights for neutral reset indices.
            var neutralStartWeights = new float[_neutralResetIndices.Count];
            for (int i = 0; i < _neutralResetIndices.Count; i++)
                neutralStartWeights[i] = _renderer.GetBlendShapeWeight(_neutralResetIndices[i]);

            // Capture start weights for incoming targets.
            var targets = mapping.Targets ?? new List<BlendShapeTarget>();
            var targetStartWeights = new float[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                targetStartWeights[i] = _renderer.GetBlendShapeWeight(targets[i].BlendShapeIndex);

            float elapsed = 0f;

            while (elapsed < _transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _transitionDuration);

                // Reset neutral indices toward zero simultaneously.
                for (int i = 0; i < _neutralResetIndices.Count; i++)
                    _renderer.SetBlendShapeWeight(_neutralResetIndices[i], Mathf.Lerp(neutralStartWeights[i], 0f, t));

                // Drive incoming targets toward their goal simultaneously.
                for (int i = 0; i < targets.Count; i++)
                    _renderer.SetBlendShapeWeight(targets[i].BlendShapeIndex, Mathf.Lerp(targetStartWeights[i], targets[i].TargetWeight, t));

                yield return null;
            }

            // Snap to final values.
            foreach (var index in _neutralResetIndices)
                _renderer.SetBlendShapeWeight(index, 0f);

            foreach (var target in targets)
                _renderer.SetBlendShapeWeight(target.BlendShapeIndex, target.TargetWeight);

            _blendCoroutine = null;
        }

        /// <summary>
        /// Populates Neutral Reset Indices from all blend shape indices present across
        /// all expression mappings. Run this from the inspector context menu after
        /// configuring your expression mappings to keep the reset list in sync.
        /// </summary>
        [ContextMenu("Populate Neutral Reset Indices From Mappings")]
        private void PopulateNeutralResetIndices()
        {
            var collected = new System.Collections.Generic.HashSet<int>();

            foreach (var mapping in _expressionMappings)
            {
                if (mapping?.Targets == null) continue;
                foreach (var target in mapping.Targets)
                    collected.Add(target.BlendShapeIndex);
            }

            _neutralResetIndices = new System.Collections.Generic.List<int>(collected);
            _neutralResetIndices.Sort();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif

            Debug.Log($"[ConvoCoreBlendShapeDisplay] Neutral Reset Indices populated with " +
                      $"{_neutralResetIndices.Count} entries: " +
                      $"{string.Join(", ", _neutralResetIndices)}");
        }
    }

    [System.Serializable]
    public class BlendShapeExpressionMapping
    {
        [Tooltip("Must match the Display Name on the corresponding PrefabExpressionMapping in the representation asset.")]
        public string ExpressionDisplayName;

        [Tooltip("One or more blend shapes to drive when this expression is applied.")]
        public List<BlendShapeTarget> Targets = new();
    }

    [System.Serializable]
    public class BlendShapeTarget
    {
        [Tooltip("Index of the blend shape on the SkinnedMeshRenderer.")]
        public int BlendShapeIndex;

        [Tooltip("Target blend shape weight (0-100).")]
        [Range(0f, 100f)]
        public float TargetWeight;
    }
}
