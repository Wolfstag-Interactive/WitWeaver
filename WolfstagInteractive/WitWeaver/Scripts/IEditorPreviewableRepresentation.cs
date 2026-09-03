// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

#if UNITY_EDITOR
using UnityEngine;
namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Opt-in editor interface for character representations that want a hover preview in the
    /// dialogue line inspector. Implement it on a <see cref="CharacterRepresentationBase"/>
    /// subclass inside an <c>#if UNITY_EDITOR</c> block (the interface only exists in the editor).
    /// Representations that do not implement it simply have no preview — the inspector skips the
    /// preview tooltip entirely.
    /// </summary>
    public interface IEditorPreviewableRepresentation
    {
        /// <summary>
        /// Draws the preview for the given expression mapping into <paramref name="position"/>.
        /// </summary>
        /// <param name="expressionMapping">The mapping returned by
        /// <see cref="CharacterRepresentationBase.GetExpressionMappingByGuid"/> for the line's
        /// selected expression — may be null (no expression selected, or the lookup missed);
        /// implementations must tolerate null.</param>
        /// <param name="position">The rect bounds for drawing.</param>
        void DrawInlineEditorPreview(object expressionMapping, Rect position);

        /// <summary>
        /// Height in pixels the preview needs. Return 0 (or less) to indicate "nothing to preview
        /// right now" — the inspector then skips the preview without drawing anything. Positive
        /// values are clamped to the inspector's preview size range.
        /// </summary>
        float GetPreviewHeight();
    }
}
#endif