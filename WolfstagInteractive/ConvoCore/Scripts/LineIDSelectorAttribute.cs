using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Renders a string field holding a dialogue <c>LineID</c> as a dropdown of the lines in the
    /// owning <see cref="ConvoCoreConversationData"/> asset instead of a raw text field.
    /// Falls back to a plain text field when the inspected object is not a conversation asset.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1LineIDSelectorAttribute.html")]
    public class LineIDSelectorAttribute : PropertyAttribute
    {
    }
}
