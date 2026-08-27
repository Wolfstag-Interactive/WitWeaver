using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Marks a string field to be rendered as a popup of expression IDs coming from a sibling
    /// SerializedProperty that references a CharacterRepresentationBase asset.
    /// Stores the GUID, shows DisplayName.
    /// </summary>
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ExpressionIDSelectorAttribute.html")]
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