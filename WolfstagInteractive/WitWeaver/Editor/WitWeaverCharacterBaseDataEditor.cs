// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverCharacterProfileBaseDataEditor.html")]
[CustomEditor(typeof(WitWeaverCharacterProfileBaseData))]
    public class WitWeaverCharacterProfileBaseDataEditor : UnityEditor.Editor
    {
        SerializedProperty isPlayerProp;
        SerializedProperty characterNameProp;
        SerializedProperty playerPlaceholderProp;
        SerializedProperty characterDescriptionProp;

        private void OnEnable()
        {
            // Cache references to the serialized properties.
            isPlayerProp = serializedObject.FindProperty("IsPlayerCharacter");
            characterNameProp = serializedObject.FindProperty("CharacterName");
            playerPlaceholderProp = serializedObject.FindProperty("PlayerPlaceholder");
            characterDescriptionProp = serializedObject.FindProperty("CharacterDescription");
        }

        public override void OnInspectorGUI()
        {
            // Update the serialized object
            serializedObject.Update();

            // Display the script reference (read-only)
            GUI.enabled = false;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            GUI.enabled = true;

            EditorGUILayout.PropertyField(isPlayerProp);
            if (isPlayerProp.boolValue)
            {
                EditorGUILayout.PropertyField(playerPlaceholderProp, new GUIContent("Player Placeholder Phrase"));
            }
            EditorGUILayout.PropertyField(characterNameProp);
            DrawCharacterDescription();

            // Draw the rest of the properties excluding script and the ones already shown
            EditorGUILayout.Space();
            DrawPropertiesExcluding(serializedObject, "m_Script", "IsPlayerCharacter", "CharacterName", "PlayerPlaceholder", "CharacterDescription");

            // Apply changes to the serialized object
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCharacterDescription()
        {
            // Draw CharacterDescription as editable multiline text with tooltip
            var content = new GUIContent("Character Description", "An optional field used to store character information such as a character description or biography.");
            var r = EditorGUILayout.GetControlRect(true, Mathf.Max(60, EditorGUIUtility.singleLineHeight));
            r = EditorGUI.IndentedRect(r);
            var labelWidth = EditorGUIUtility.labelWidth;
            var labelRect = new Rect(r.x, r.y, labelWidth, EditorGUIUtility.singleLineHeight);
            var fieldRect = new Rect(r.x + labelWidth, r.y, r.width - labelWidth, r.height);
            EditorGUI.PrefixLabel(labelRect, content); // shows tooltip on hover
            characterDescriptionProp.stringValue = EditorGUI.TextArea(fieldRect, characterDescriptionProp.stringValue);
        }

    }
}