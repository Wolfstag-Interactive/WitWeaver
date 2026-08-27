using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Minimal prefab display component. All expression logic is delegated to
    /// <see cref="BaseExpressionAction"/> ScriptableObjects defined on the
    /// <see cref="PrefabCharacterRepresentationData"/> asset. No built-in visual
    /// change behaviour is applied by this component itself.
    ///
    /// Use this when you want full control over expression results via ScriptableObject
    /// actions and do not need built-in Animator, blend-shape, or sprite handling.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreActionOnlyDisplay.html")]
    public class ConvoCoreActionOnlyDisplay : ConvoCoreCharacterDisplayBase
    {
        private PrefabCharacterRepresentationData _prefabRep;

        public override void BindRepresentation(CharacterRepresentationBase representationAsset)
        {
            _prefabRep = representationAsset as PrefabCharacterRepresentationData;

            if (_prefabRep == null)
                Debug.LogWarning($"[ConvoCoreActionOnlyDisplay] Expected PrefabCharacterRepresentationData " +
                                 $"but received '{representationAsset?.GetType().Name}'. Expression actions will be skipped.");
        }

        public override void ApplyExpression(string expressionId)
        {
            if (_prefabRep == null)
            {
                Debug.LogWarning("[ConvoCoreActionOnlyDisplay] No representation bound. Call BindRepresentation first.");
                return;
            }

            if (!_prefabRep.TryResolveById(expressionId, out var mapping))
            {
                Debug.LogWarning($"[ConvoCoreActionOnlyDisplay] ExpressionId '{expressionId}' not found in '{_prefabRep.name}'.");
                return;
            }

            // This method body is intentionally empty. ConvoCoreActionOnlyDisplay has two jobs:
            //
            // 1. Provide a valid IConvoCoreCharacterDisplay on the prefab so ConvoCore can call
            //    BindRepresentation and ApplyExpression without null-checking for a display component.
            //
            // 2. Let PrefabCharacterRepresentationData.ApplyExpression (called separately by the
            //    ConvoCore runner) execute any BaseExpressionAction ScriptableObjects attached to
            //    the expression mapping. Those actions ARE the visual response -- this component
            //    intentionally adds none of its own.
            //
            // If you need built-in Animator or blend shape driving, use ConvoCoreAnimatorDisplay
            // or ConvoCoreBlendShapeDisplay instead.
        }
    }
}
