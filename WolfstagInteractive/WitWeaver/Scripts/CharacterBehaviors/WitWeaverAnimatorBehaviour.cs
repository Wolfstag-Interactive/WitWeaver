// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Character behaviour type for scene-resident characters that are driven by an <see cref="Animator"/>.
    ///
    /// Resolves the scene-resident character via <see cref="WitWeaverSceneCharacterRegistry"/>,
    /// identical to <see cref="ExternalBehaviour"/>. The character's expression application is
    /// handled by a <see cref="WitWeaverAnimatorDisplay"/> component on the scene object.
    ///
    /// This behaviour is a named variant of <see cref="ExternalBehaviour"/> provided as a
    /// convenience create-asset-menu entry to make the intended usage pattern explicit.
    /// Pair it with scene characters that have a <see cref="WitWeaverAnimatorDisplay"/> component.
    ///
    /// Use case: fully animated scene-resident characters driven by an existing Animator controller.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimatorBehaviour", menuName = "WitWeaver/Character Behaviour/Animator Behaviour")]
    public class WitWeaverAnimatorBehaviour : ExternalBehaviour
    {
        // Inherits ExternalBehaviour resolution (scene-resident registry lookup).
        // Expression application is performed by WitWeaverAnimatorDisplay on the character.
    }
}
