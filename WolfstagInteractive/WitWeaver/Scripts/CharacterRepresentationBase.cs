using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Abstract ScriptableObject base class for all character visual representations.
    /// Extend this class to map expression IDs to sprites, prefabs, or any other display asset.
    /// Attach a concrete implementation to a <see cref="WitWeaverCharacterProfileBaseData"/> asset
    /// so the runner can resolve visuals and trigger expression actions on each line.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1CharacterRepresentationBase.html")]

    public abstract class CharacterRepresentationBase : ScriptableObject 
        #if UNITY_EDITOR
         ,IEditorPreviewableRepresentation
        #endif
    {
        /// <summary>
        /// Resolves an expression ID to a representation-defined payload for UI consumption.
        ///
        /// The return shape is defined by each concrete representation — there is no common
        /// payload type. Consumers must type-test the result (e.g. <c>is SpriteExpressionMapping</c>)
        /// and must not assume any particular shape for representation types they do not know.
        /// Built-in contracts:
        /// <list type="bullet">
        /// <item><see cref="SpriteCharacterRepresentationData"/> returns a <see cref="SpriteExpressionMapping"/>
        /// (or null when it has no mappings).</item>
        /// <item><see cref="AnimatedCharacterRepresentationData"/> returns an <see cref="AnimatedExpressionMapping"/>
        /// (or null when it has no mappings).</item>
        /// <item><see cref="PrefabCharacterRepresentationData"/> returns <paramref name="expressionID"/>
        /// unchanged: prefab visuals are bound by the spawned display
        /// (<see cref="IWitWeaverCharacterDisplay.ApplyExpression"/>), not by this method.
        /// Representations whose visuals live on a spawned instance may follow the same pattern.</item>
        /// </list>
        /// </summary>
        /// <param name="expressionID">Stable expression ID (GUID) to resolve. Null/empty selects the
        /// representation's default, where one exists.</param>
        /// <returns>A representation-defined payload, the unchanged ID for display-bound
        /// representations, or null.</returns>
        public abstract object ProcessExpression(string expressionID);
        /// <summary>
        /// Apply an expression for this representation.
        /// Implementations are expected to run any attached BaseExpressionAction.
        /// </summary>
        public abstract void ApplyExpression(
            string expressionId,
            WitWeaver runtime,
            WitWeaverConversationData conversation,
            int lineIndex,
            IWitWeaverCharacterDisplay display);
        /// <summary>
        /// Retrieves the expression mapping object by its GUID. Exact lookup: no fallback, no
        /// logging — a miss (or a null/empty GUID) returns null.
        /// Used by the editor to feed <c>DrawInlineEditorPreview</c>; the mapping type is
        /// representation-defined (see <see cref="ProcessExpression"/> for the built-in shapes).
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
        /// <see cref="WitWeaverConversationData"/>. The inspector will show one dropdown slot
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
    public interface IWitWeaverRepresentationInitializable
    {
        void Initialize();
    }
}