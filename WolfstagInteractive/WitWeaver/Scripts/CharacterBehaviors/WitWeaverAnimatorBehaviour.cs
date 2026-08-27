using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Character behaviour type for scene-resident characters that are driven by an <see cref="Animator"/>.
    ///
    /// Resolves the scene-resident character via <see cref="ConvoCoreSceneCharacterRegistry"/>,
    /// identical to <see cref="ExternalBehaviour"/>. The character's expression application is
    /// handled by a <see cref="ConvoCoreAnimatorDisplay"/> component on the scene object.
    ///
    /// This behaviour is a named variant of <see cref="ExternalBehaviour"/> provided as a
    /// convenience create-asset-menu entry to make the intended usage pattern explicit.
    /// Pair it with scene characters that have a <see cref="ConvoCoreAnimatorDisplay"/> component.
    ///
    /// Use case: fully animated scene-resident characters driven by an existing Animator controller.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimatorBehaviour", menuName = "ConvoCore/Character Behaviour/Animator Behaviour")]
    public class ConvoCoreAnimatorBehaviour : ExternalBehaviour
    {
        // Inherits ExternalBehaviour resolution (scene-resident registry lookup).
        // Expression application is performed by ConvoCoreAnimatorDisplay on the character.
    }
}
