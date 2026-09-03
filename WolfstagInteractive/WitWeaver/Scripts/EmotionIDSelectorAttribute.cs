// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Marks a string field to be rendered as a popup of expression IDs coming from a sibling
    /// SerializedProperty that references a CharacterRepresentationBase asset.
    /// Stores the GUID, shows DisplayName.
    /// </summary>
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1ExpressionIDSelectorAttribute.html")]
[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false)]
    public class ExpressionIDSelectorAttribute : PropertyAttribute
    {
        public string RepresentationPropertyName { get; }
        public ExpressionIDSelectorAttribute(string representationPropertyName)
        {
            RepresentationPropertyName = representationPropertyName;
        }
    }
}