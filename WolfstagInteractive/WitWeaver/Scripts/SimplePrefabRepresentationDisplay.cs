using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SimplePrefabRepresentationDisplay.html")]
    [System.Obsolete("SimplePrefabRepresentationDisplay is deprecated. " +
                     "Use ConvoCoreActionOnlyDisplay instead, which provides the same " +
                     "functionality with a corrected BindRepresentation signature.")]
    public class SimplePrefabRepresentationDisplay : ConvoCoreCharacterDisplayBase
    {
        private CharacterRepresentationBase _representation;
        private PrefabCharacterRepresentationData _prefabRep;

        public override void BindRepresentation(CharacterRepresentationBase representationAsset)
        {
            _representation = representationAsset;
            _prefabRep = representationAsset as PrefabCharacterRepresentationData;

            if (_prefabRep == null)
                Debug.LogWarning($"[SimplePrefabRepresentationDisplay] Expected PrefabCharacterRepresentationData " +
                                 $"but received '{representationAsset?.GetType().Name}'. Expression application will be skipped.");
        }

        public override void ApplyExpression(string expressionId)
        {
            if (_prefabRep == null)
            {
                Debug.LogWarning("[SimplePrefabDisplay] Catalog not bound. Call BindRepresentation first.");
                return;
            }

            if (!_prefabRep.TryResolveById(expressionId, out var mapping))
            {
                Debug.LogWarning($"[SimplePrefabDisplay] ExpressionId '{expressionId}' not found in '{_prefabRep.name}'.");
                return;
            }

            /*if (_animator && mapping.AnimatorController)
                _animator.runtimeAnimatorController = mapping.AnimatorController;

            if (_renderer && mapping.OverrideMaterial)
                _renderer.sharedMaterial = mapping.OverrideMaterial;*/

            // Add more payload effects as needed (SFX, particle, blend shapes...)
        }
        // Intentionally standalone -- does not call base.ApplyDisplayOptions.
        // Positioning is handled by the parent transform assigned in ConvoCorePrefabRepresentationSpawner.
        // Scale and flip are applied directly to this transform.
        public override void ApplyDisplayOptions(DialogueLineDisplayOptions options)
        {
            var scale = Vector3.Scale(Vector3.one, options.FullBodyScale);
            transform.localScale = new Vector3(
                options.FlipFullBodyX ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x),
                options.FlipFullBodyY ? -Mathf.Abs(scale.y) : Mathf.Abs(scale.y),
                scale.z);
        }
    }
}