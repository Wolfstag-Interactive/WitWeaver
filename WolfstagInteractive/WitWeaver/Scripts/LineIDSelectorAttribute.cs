using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Renders a string field holding a dialogue <c>LineID</c> as a dropdown of the lines in the
    /// owning <see cref="WitWeaverConversationData"/> asset instead of a raw text field.
    /// Falls back to a plain text field when the inspected object is not a conversation asset.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1LineIDSelectorAttribute.html")]
    public class LineIDSelectorAttribute : PropertyAttribute
    {
    }
}
