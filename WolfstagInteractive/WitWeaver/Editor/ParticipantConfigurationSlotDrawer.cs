#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    /// <summary>
    /// Property drawer for <see cref="ParticipantConfigurationSlot"/>.
    ///
    /// Shows a CharacterID text field and a dropdown for DefaultConfigurationEntryName.
    /// The dropdown is populated from the <see cref="PrefabCharacterRepresentationData"/> assets
    /// assigned to the matching participant profile on the parent <see cref="WitWeaverConversationData"/>.
    /// When no PrefabCharacterRepresentationData is found for the participant, the dropdown falls
    /// back to a plain text field.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1ParticipantConfigurationSlotDrawer.html")]
[CustomPropertyDrawer(typeof(ParticipantConfigurationSlot))]
    public class ParticipantConfigurationSlotDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            return line + Spacing + line + Spacing + line + Spacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var charIdProp    = property.FindPropertyRelative("CharacterID");
            var entryProp     = property.FindPropertyRelative("DefaultConfigurationEntryName");
            var timingProp    = property.FindPropertyRelative("SpawnTiming");

            float line  = EditorGUIUtility.singleLineHeight;
            float y     = position.y;

            // Row 1 — CharacterID (read-only; auto-managed by WitWeaverConversationDataEditor)
            var charIdRect = new Rect(position.x, y, position.width, line);
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.PropertyField(charIdRect, charIdProp, new GUIContent("Character ID"));
            y += line + Spacing;

            // Row 2 — DefaultConfigurationEntryName (dropdown or fallback text field)
            var entryRect = new Rect(position.x, y, position.width, line);
            var entryNames = GetEntryNames(property, charIdProp.stringValue);

            if (entryNames != null && entryNames.Length > 0)
            {
                DrawEntryDropdown(entryRect, entryProp, entryNames);
            }
            else
            {
                EditorGUI.PropertyField(entryRect, entryProp, new GUIContent("Default Entry Name"));
            }
            y += line + Spacing;

            // Row 3 — SpawnTiming
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, line), timingProp, new GUIContent("Spawn Timing"));
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static void DrawEntryDropdown(Rect rect, SerializedProperty entryProp, string[] options)
        {
            // Find current selection index.
            int current = 0;
            if (!string.IsNullOrEmpty(entryProp.stringValue))
            {
                for (int i = 0; i < options.Length; i++)
                {
                    if (options[i] == entryProp.stringValue)
                    {
                        current = i;
                        break;
                    }
                }
            }

            EditorGUI.BeginProperty(rect, GUIContent.none, entryProp);
            int selected = EditorGUI.Popup(rect, new GUIContent("Default Entry"), current, ToGUIContents(options));
            EditorGUI.EndProperty();

            entryProp.stringValue = options[selected];
        }

        /// <summary>
        /// Walks up from the slot property to find the parent WitWeaverConversationData,
        /// then collects all entry names from any representation that opts in via
        /// <see cref="CharacterRepresentationBase.GetConfigurationEntryNames"/>.
        /// </summary>
        private static string[] GetEntryNames(SerializedProperty slotProperty, string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;

            if (!(slotProperty.serializedObject.targetObject is WitWeaverConversationData conversationData))
                return null;

            var names = new List<string>();
            var seen  = new HashSet<string>();

            foreach (var profile in conversationData.ConversationParticipantProfiles)
            {
                if (profile == null || profile.CharacterID != characterId) continue;

                foreach (var pair in profile.Representations)
                {
                    var entryNames = pair?.CharacterRepresentationType?.GetConfigurationEntryNames();
                    if (entryNames == null) continue;
                    foreach (var n in entryNames)
                        if (!string.IsNullOrEmpty(n) && seen.Add(n))
                            names.Add(n);
                }
            }

            return names.Count > 0 ? names.ToArray() : null;
        }

        private static GUIContent[] ToGUIContents(string[] labels)
        {
            var result = new GUIContent[labels.Length];
            for (int i = 0; i < labels.Length; i++)
                result[i] = new GUIContent(labels[i]);
            return result;
        }
    }
}
#endif
