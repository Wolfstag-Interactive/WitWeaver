// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver
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
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverActionOnlyDisplay.html")]
    public class WitWeaverActionOnlyDisplay : WitWeaverCharacterDisplayBase
    {
        private PrefabCharacterRepresentationData _prefabRep;

        public override void BindRepresentation(CharacterRepresentationBase representationAsset)
        {
            _prefabRep = representationAsset as PrefabCharacterRepresentationData;

            if (_prefabRep == null)
                Debug.LogWarning($"[WitWeaverActionOnlyDisplay] Expected PrefabCharacterRepresentationData " +
                                 $"but received '{representationAsset?.GetType().Name}'. Expression actions will be skipped.");
        }

        public override void ApplyExpression(string expressionId)
        {
            if (_prefabRep == null)
            {
                Debug.LogWarning("[WitWeaverActionOnlyDisplay] No representation bound. Call BindRepresentation first.");
                return;
            }

            if (!_prefabRep.TryResolveById(expressionId, out var mapping))
            {
                Debug.LogWarning($"[WitWeaverActionOnlyDisplay] ExpressionId '{expressionId}' not found in '{_prefabRep.name}'.");
                return;
            }

            // This method body is intentionally empty. WitWeaverActionOnlyDisplay has two jobs:
            //
            // 1. Provide a valid IWitWeaverCharacterDisplay on the prefab so WitWeaver can call
            //    BindRepresentation and ApplyExpression without null-checking for a display component.
            //
            // 2. Let PrefabCharacterRepresentationData.ApplyExpression (invoked separately by the
            //    UI foundation's expression-action pass) execute any BaseExpressionAction
            //    ScriptableObjects attached to the expression mapping. Those actions ARE the
            //    visual response -- this component intentionally adds none of its own.
            //
            // If you need built-in Animator or blend shape driving, use WitWeaverAnimatorDisplay
            // or WitWeaverBlendShapeDisplay instead.
        }
    }
}
