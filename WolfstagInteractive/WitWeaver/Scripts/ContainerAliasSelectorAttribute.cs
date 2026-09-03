// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Renders a string field holding a <see cref="ConversationContainer"/> entry alias (or
    /// conversation name) as a dropdown of the entries in the container referenced by a sibling
    /// field, instead of a raw text field. Falls back to a plain text field when no container is
    /// assigned.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1ContainerAliasSelectorAttribute.html")]
    public class ContainerAliasSelectorAttribute : PropertyAttribute
    {
        /// <summary>Name of the sibling field (same struct/class) holding the ConversationContainer reference.</summary>
        public readonly string ContainerFieldName;

        public ContainerAliasSelectorAttribute(string containerFieldName)
        {
            ContainerFieldName = containerFieldName;
        }
    }
}
