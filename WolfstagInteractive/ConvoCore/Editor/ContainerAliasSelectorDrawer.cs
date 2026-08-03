using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.Editor
{
    /// <summary>
    /// Draws a [ContainerAliasSelector] string field as a dropdown of the entries in the sibling
    /// <see cref="ConversationContainer"/> field. Entries are labeled "alias — conversation";
    /// "(Let container decide)" clears the field, and a value that matches no entry is surfaced
    /// as a "Missing: ..." entry instead of being silently discarded. Falls back to a plain text
    /// field when no container is assigned.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ContainerAliasSelectorDrawer.html")]
[CustomPropertyDrawer(typeof(ContainerAliasSelectorAttribute))]
    public class ContainerAliasSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var container = ResolveSiblingContainer(property);
            if (container?.Conversations == null || container.Conversations.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var options = new List<GUIContent> { new("(Let container decide)") };
            var values = new List<string> { "" };

            string current = property.stringValue;
            int selected = string.IsNullOrEmpty(current) ? 0 : -1;

            foreach (var entry in container.Conversations)
            {
                if (entry?.ConversationData == null) continue;

                bool hasAlias = !string.IsNullOrEmpty(entry.Alias);
                // ResolveForBranch matches alias first, then conversation name — store whichever exists.
                var value = hasAlias ? entry.Alias : entry.ConversationData.name;
                var display = hasAlias
                    ? $"{entry.Alias} — {entry.ConversationData.name}"
                    : entry.ConversationData.name;

                options.Add(new GUIContent(display));
                values.Add(value);
                if (selected < 0 && string.Equals(value, current, StringComparison.OrdinalIgnoreCase))
                    selected = values.Count - 1;
            }

            if (selected < 0)
            {
                options.Add(new GUIContent($"Missing: {current}"));
                values.Add(current);
                selected = values.Count - 1;
            }

            EditorGUI.BeginProperty(position, label, property);
            int newSelected = EditorGUI.Popup(position, label, selected, options.ToArray());
            if (newSelected != selected)
                property.stringValue = values[newSelected];
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        /// <summary>Finds the ConversationContainer on the sibling field named by the attribute.</summary>
        private ConversationContainer ResolveSiblingContainer(SerializedProperty property)
        {
            var attr = (ContainerAliasSelectorAttribute)attribute;
            var path = property.propertyPath;
            int lastDot = path.LastIndexOf('.');
            var siblingPath = lastDot >= 0
                ? path.Substring(0, lastDot + 1) + attr.ContainerFieldName
                : attr.ContainerFieldName;

            var siblingProp = property.serializedObject.FindProperty(siblingPath);
            return siblingProp?.objectReferenceValue as ConversationContainer;
        }
    }
}
