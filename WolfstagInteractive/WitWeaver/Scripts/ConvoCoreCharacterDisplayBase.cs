using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Generic base class for all prefab-based character displays.
    /// Applies scale, flip, and side offsets using ConvoCore's side-based layout model.
    /// </summary>
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreCharacterDisplayBase.html")]
    public abstract class ConvoCoreCharacterDisplayBase : MonoBehaviour, IConvoCoreCharacterDisplay
    {
        [Header("Display Root Settings")]
        [Tooltip("The root object to scale/flip. Defaults to this GameObject.")]
        [SerializeField] protected Transform visualRoot;

        [Tooltip("Should the root object be flipped horizontally for side-facing characters?")]
        [SerializeField] protected bool supportFlipX = true;

        [Tooltip("Should the root object be flipped vertically?")]
        [SerializeField] protected bool supportFlipY = false;
        
        protected virtual void Awake()
        {
            if (visualRoot == null)
                visualRoot = transform;

        }

        public virtual void ApplyDisplayOptions(DialogueLineDisplayOptions options)
        {
            // Scale
            var scale = options.FullBodyScale;

            // Flip X/Y -- applied as sign on scale axes
            if (supportFlipX && options.FlipFullBodyX)
                scale.x *= -1f;
            if (supportFlipY && options.FlipFullBodyY)
                scale.y *= -1f;

            visualRoot.localScale = scale;

            // Positioning is intentionally not handled here.
            // Characters are positioned by the parent transform assigned in ConvoCorePrefabRepresentationSpawner.
            // Override ApplyDisplayOptions in a subclass if the character needs to reposition itself
            // relative to its parent based on display options.
        }

        public abstract void BindRepresentation(CharacterRepresentationBase representationAsset);
      

        public abstract void ApplyExpression(string expressionId);
    }
}