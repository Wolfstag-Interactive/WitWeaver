using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Abstract ScriptableObject base class for all character visual representations.
    /// Extend this class to map expression IDs to sprites, prefabs, or any other display asset.
    /// Attach a concrete implementation to a <see cref="ConvoCoreCharacterProfileBaseData"/> asset
    /// so the runner can resolve visuals and trigger expression actions on each line.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1CharacterRepresentationBase.html")]

    public abstract class CharacterRepresentationBase : ScriptableObject 
        #if UNITY_EDITOR
         ,IEditorPreviewableRepresentation
        #endif
    {
        /// <summary>
        /// Processes the given expression and returns UI-relevant data (e.g., a sprite or GameObject).
        /// Allows each character representation to define its own output.
        /// </summary>
        /// <param name="expressionID">The expression to process.</param>
        /// <returns>Object related to the current representation, e.g., Sprite, GameObject, etc.</returns>
        public abstract object ProcessExpression(string expressionID);
        /// <summary>
        /// Apply an expression for this representation.
        /// Implementations are expected to run any attached BaseExpressionAction.
        /// </summary>
        public abstract void ApplyExpression(
            string expressionId,
            ConvoCore runtime,
            ConvoCoreConversationData conversation,
            int lineIndex,
            IConvoCoreCharacterDisplay display);
        /// <summary>
        /// Retrieves the expression mapping object by its GUID.
        /// Used by the editor to display the correct expression in previews.
        /// </summary>
        /// <param name="expressionGuid">The GUID of the expression to retrieve.</param>
        /// <returns>The expression mapping object, or null if not found.</returns>
        public abstract object GetExpressionMappingByGuid(string expressionGuid);
        #if UNITY_EDITOR
        public abstract void DrawInlineEditorPreview(object expressionMapping, Rect position);
        public abstract float GetPreviewHeight();
        #endif
        /// <summary>
        /// Returns the named configuration entry options exposed by this representation.
        /// Override to opt in to the <c>Participant Configuration Defaults</c> system on
        /// <see cref="ConvoCoreConversationData"/>. The inspector will show one dropdown slot
        /// per participant whose profile contains a representation that returns non-null here.
        ///
        /// Return <c>null</c> (default) to opt out entirely — no slot will be generated for
        /// participants whose representations all return <c>null</c>.
        /// </summary>
        public virtual IReadOnlyList<string> GetConfigurationEntryNames() => null;
    }
    
    /// <summary>
    /// Optional interface for character representations that require manual initialization.
    /// </summary>
    public interface IConvoCoreRepresentationInitializable
    {
        void Initialize();
    }
}