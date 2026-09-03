// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    /// <summary>
    /// Draws a <see cref="RepresentationPair"/> as name + representation asset + read-only stable
    /// ID, mirroring how expression mappings present their non-editable GUIDs.
    /// </summary>
    [CustomPropertyDrawer(typeof(RepresentationPair))]
    public class RepresentationPairDrawer : PropertyDrawer
    {
        private const float k_Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 3f + k_Spacing * 3f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var nameProp = property.FindPropertyRelative("CharacterRepresentationName");
            var typeProp = property.FindPropertyRelative("CharacterRepresentationType");
            var idProp = property.FindPropertyRelative("representationID");

            EditorGUI.BeginProperty(position, label, property);

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, nameProp, new GUIContent("Name",
                "Human-readable name shown in dropdowns. Display-only; renaming never breaks references."));

            line.y += EditorGUIUtility.singleLineHeight + k_Spacing;
            EditorGUI.PropertyField(line, typeProp, new GUIContent("Representation"));

            line.y += EditorGUIUtility.singleLineHeight + k_Spacing;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(line, idProp, new GUIContent("Representation ID",
                    "Stable unique ID (GUID). Non-editable; dialogue lines reference this."));
            }

            EditorGUI.EndProperty();
        }
    }
}
