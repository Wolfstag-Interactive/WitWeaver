using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    /// <summary>
    /// Draws a [LineIDSelector] string field as a dropdown of the dialogue lines in the
    /// inspected <see cref="WitWeaverConversationData"/> asset. Entries are labeled
    /// "{index}: {characterID} — {text preview}". A "(None)" entry clears the field, and a
    /// value that no longer matches any line is surfaced as a "Missing: ..." entry so the
    /// stale id is visible instead of silently discarded.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1LineIDSelectorDrawer.html")]
[CustomPropertyDrawer(typeof(LineIDSelectorAttribute))]
    public class LineIDSelectorDrawer : PropertyDrawer
    {
        private const int PreviewLength = 30;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var conversation = property.serializedObject.targetObject as WitWeaverConversationData;
            if (conversation?.DialogueLines == null || conversation.DialogueLines.Count == 0)
            {
                // Not inspecting a conversation asset (or it has no lines) — plain text field.
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var lines = conversation.DialogueLines;
            string currentId = property.stringValue;

            var options = new List<GUIContent>(lines.Count + 2) { new("(None)") };
            var ids = new List<string>(lines.Count + 2) { "" };

            int selected = string.IsNullOrEmpty(currentId) ? 0 : -1;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null || string.IsNullOrEmpty(line.LineID)) continue;

                options.Add(new GUIContent($"{i}: {line.characterID} — {GetPreviewText(line)}"));
                ids.Add(line.LineID);
                if (selected < 0 && line.LineID == currentId)
                    selected = ids.Count - 1;
            }

            if (selected < 0)
            {
                // Stale id (line deleted or YAML changed) — keep it visible and selected.
                options.Add(new GUIContent($"Missing: {currentId}"));
                ids.Add(currentId);
                selected = ids.Count - 1;
            }

            EditorGUI.BeginProperty(position, label, property);
            int newSelected = EditorGUI.Popup(position, label, selected, options.ToArray());
            if (newSelected != selected)
                property.stringValue = ids[newSelected];
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        private static string GetPreviewText(WitWeaverConversationData.DialogueLineInfo line)
        {
            if (line.LocalizedDialogues != null)
            {
                for (int i = 0; i < line.LocalizedDialogues.Count; i++)
                {
                    var text = line.LocalizedDialogues[i].Text;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    text = text.Replace('\n', ' ').Trim();
                    return text.Length > PreviewLength ? text.Substring(0, PreviewLength - 3) + "..." : text;
                }
            }
            return line.LineID;
        }
    }
}
