using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Renders a string field holding a <see cref="ConversationContainer"/> entry alias (or
    /// conversation name) as a dropdown of the entries in the container referenced by a sibling
    /// field, instead of a raw text field. Falls back to a plain text field when no container is
    /// assigned.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ContainerAliasSelectorAttribute.html")]
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
