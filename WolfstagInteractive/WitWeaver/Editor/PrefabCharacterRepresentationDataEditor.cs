#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.Editor
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1PrefabCharacterRepresentationDataEditor.html")]
    [CustomEditor(typeof(PrefabCharacterRepresentationData))]
    public class PrefabCharacterRepresentationDataEditor : UnityEditor.Editor
    {
        private ReorderableList _sharedList;
        private ReorderableList _entriesList;

        private void OnEnable()
        {
            BuildSharedList();
            BuildEntriesList();
        }

        /// <summary>
        /// Renders and manages the custom Inspector GUI for the
        /// PrefabCharacterRepresentationData asset. This method handles
        /// the layout and display of configuration entries, shared expression
        /// mappings, and associated warnings or tooltips.
        /// </summary>
        /// <remarks>
        /// Includes logic to rebuild reorderable lists (if cleared due to domain
        /// reload), ensure unique GUIDs for shared expressions, and display warnings
        /// for duplicate shared expression names. Custom GUI components, such as
        /// reorderable lists and informational labels, are utilized to organize
        /// the properties in the Inspector.
        /// </remarks>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Safety rebuild if lists were cleared by domain reload
            if (_sharedList == null) BuildSharedList();
            if (_entriesList == null) BuildEntriesList();

            // ── Configuration Entries ──────────────────────────────────────
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Configuration Entries", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each entry defines a prefab (spawned when the character is not in the scene registry), " +
                "character behaviours that controls 3D placement or other logic, and optional expression overrides.",
                MessageType.None);

            _entriesList.DoLayoutList();

            EditorGUILayout.Space(8f);

            // ── Shared Expression Pool ─────────────────────────────────────
            EditorGUILayout.LabelField("Shared Expression Pool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Expressions available to all entries. An entry's own ExpressionOverrides take priority.",
                MessageType.None);

            var sharedProp = serializedObject.FindProperty("SharedExpressionMappings");
            EnsureGuids(sharedProp);

            var sharedDuplicates = GetDuplicateDisplayNames(sharedProp);
            if (sharedDuplicates.Count > 0)
                EditorGUILayout.HelpBox(
                    $"Duplicate shared expression names: {string.Join(", ", sharedDuplicates)}",
                    MessageType.Warning);

            _sharedList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
            Repaint();
        }

        /// <summary>
        /// Configures and initializes the reorderable list used to manage
        /// shared expression mappings within the PrefabCharacterRepresentationData editor.
        /// The shared expression pool is available globally across all entries,
        /// where each mapping associates an identifier with specific expression data.
        /// </summary>
        private void BuildSharedList()
        {
            var listProp = serializedObject.FindProperty("SharedExpressionMappings");
            if (listProp == null) return;

            _sharedList = new ReorderableList(serializedObject, listProp, true, true, true, true);
            _sharedList.drawHeaderCallback  = rect => EditorGUI.LabelField(rect, "Shared Expressions");
            _sharedList.elementHeightCallback = index => ExpressionMappingHeight(listProp, index);
            _sharedList.drawElementCallback   = (rect, index, active, focused) =>
                DrawExpressionMappingElement(rect, listProp, index, null);
        }

        /// <summary>
        /// Initializes and configures the reorderable list for managing
        /// configuration entries within the PrefabCharacterRepresentationData editor.
        /// Each entry specifies details such as the prefab, character behavior,
        /// and optional visual expression overrides.
        /// </summary>
        private void BuildEntriesList()
        {
            var entriesProp = serializedObject.FindProperty("ConfigurationEntries");
            if (entriesProp == null) return;

            _entriesList = new ReorderableList(serializedObject, entriesProp, true, true, true, true);
            _entriesList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Configuration Entries");

            _entriesList.elementHeightCallback = index =>
            {
                if (!entriesProp.isArray || index >= entriesProp.arraySize) return EditorGUIUtility.singleLineHeight;

                var el          = entriesProp.GetArrayElementAtIndex(index);
                float lineH     = EditorGUIUtility.singleLineHeight;
                float pad       = 8f;
                float spacing   = 3f;
                // EntryName + CharacterPrefab = 2 fixed rows
                float rows      = 2f * (lineH + spacing);
                // CharacterBehaviours (variable height list)
                var charBehavioursProp = el.FindPropertyRelative("CharacterBehaviours");
                float behavioursH = charBehavioursProp != null
                    ? EditorGUI.GetPropertyHeight(charBehavioursProp, true) + spacing
                    : 0f;
                // Preview
                float preview   = 68f;
                // ExpressionOverrides
                var overridesProp = el.FindPropertyRelative("ExpressionOverrides");
                float overridesH = overridesProp != null
                    ? EditorGUI.GetPropertyHeight(overridesProp, true) + spacing
                    : 0f;

                return pad + rows + behavioursH + preview + spacing + overridesH + pad;
            };

            _entriesList.drawElementCallback = (rect, index, active, focused) =>
            {
                if (!entriesProp.isArray || index >= entriesProp.arraySize) return;

                var el          = entriesProp.GetArrayElementAtIndex(index);
                float lineH     = EditorGUIUtility.singleLineHeight;
                float pad       = 8f;
                float spacing   = 3f;

                rect = new Rect(rect.x + 4f, rect.y + pad, rect.width - 8f, rect.height - pad * 2f);

                var nameProp           = el.FindPropertyRelative("EntryName");
                var prefabProp         = el.FindPropertyRelative("CharacterPrefab");
                var charBehavioursProp = el.FindPropertyRelative("CharacterBehaviours");
                var overridesProp      = el.FindPropertyRelative("ExpressionOverrides");

                float y = rect.y;

                // Row: EntryName
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), nameProp, new GUIContent("Entry Name"));
                y += lineH + spacing;

                // Row: CharacterPrefab
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineH), prefabProp);
                y += lineH + spacing;

                // CharacterBehaviours (variable height list)
                if (charBehavioursProp != null)
                {
                    float behavioursH = EditorGUI.GetPropertyHeight(charBehavioursProp, true);
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, behavioursH),
                        charBehavioursProp, new GUIContent("Character Behaviours"), true);
                    y += behavioursH + spacing;
                }

                // Prefab preview
                var previewRect = new Rect(rect.x, y, rect.width, 68f);
                DrawPrefabPreview(previewRect, prefabProp);
                y += 68f + spacing;

                // ExpressionOverrides
                if (overridesProp != null)
                {
                    EnsureGuids(overridesProp);
                    float overridesH = EditorGUI.GetPropertyHeight(overridesProp, true);
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, overridesH),
                        overridesProp, new GUIContent("Expression Overrides"), true);
                }
            };
        }

        /// <summary>
        /// Calculates the height of an expression mapping element in a reorderable list.
        /// This height accounts for each element's header, padding, and any associated properties,
        /// dynamically adjusting based on the presence and expanded state of nested properties.
        /// </summary>
        /// <param name="listProp">The serialized property representing the list of expression mappings.</param>
        /// <param name="index">The index of the expression mapping within the list.</param>
        /// <returns>The calculated height in pixels required to render the specified element.</returns>
        private static float ExpressionMappingHeight(SerializedProperty listProp, int index)
        {
            const float pad = 6f;
            float header     = EditorGUIUtility.singleLineHeight;
            float height     = pad + header + pad;

            if (listProp.isArray && index >= 0 && index < listProp.arraySize)
            {
                var actionsProp = listProp.GetArrayElementAtIndex(index).FindPropertyRelative("ExpressionActions");
                if (actionsProp != null)
                    height += 4f + EditorGUI.GetPropertyHeight(actionsProp, true);
            }

            return height;
        }

        private static void DrawExpressionMappingElement(Rect rect, SerializedProperty listProp,
            int index, SerializedProperty prefabPropOverride)
        {
            var el         = listProp.GetArrayElementAtIndex(index);
            var nameProp   = el.FindPropertyRelative("DisplayName") ?? el.FindPropertyRelative("Name");
            var guidProp   = el.FindPropertyRelative("expressionID");
            var actionsProp = el.FindPropertyRelative("ExpressionActions");

            bool isDuplicate = !string.IsNullOrWhiteSpace(nameProp?.stringValue) &&
                               CountName(listProp, index, nameProp.stringValue) > 0;

            const float pad = 6f;
            rect = new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, rect.height - pad * 2f);

            var headerRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);

            float warnW  = isDuplicate ? 20f : 0f;
            float nameW  = (headerRect.width - warnW) * 0.55f;

            if (isDuplicate)
            {
                var warnRect = new Rect(headerRect.x, headerRect.y, warnW, headerRect.height);
                EditorGUI.LabelField(warnRect, new GUIContent(
                    EditorGUIUtility.IconContent("console.warnicon.sml")) { tooltip = "Duplicate name." });
            }

            var nameRect  = new Rect(headerRect.x + warnW, headerRect.y, nameW, headerRect.height);
            var guidLabel = new Rect(nameRect.xMax + 8f, headerRect.y, 70f, headerRect.height);
            var guidRect  = new Rect(guidLabel.xMax + 4f, headerRect.y,
                headerRect.xMax - (guidLabel.xMax + 4f), headerRect.height);

            EditorGUI.BeginProperty(nameRect, GUIContent.none, nameProp);
            if (isDuplicate)
            {
                var old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.8f, 0.6f);
                nameProp.stringValue = EditorGUI.TextField(nameRect, nameProp.displayName, nameProp.stringValue);
                GUI.backgroundColor = old;
            }
            else
            {
                nameProp.stringValue = EditorGUI.TextField(nameRect, nameProp.displayName, nameProp.stringValue);
            }
            EditorGUI.EndProperty();

            EditorGUI.LabelField(guidLabel, "GUID");
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.SelectableLabel(guidRect, guidProp != null ? guidProp.stringValue : "(missing)");

            if (actionsProp != null)
            {
                float actH    = EditorGUI.GetPropertyHeight(actionsProp, true);
                var actRect   = new Rect(rect.x, headerRect.yMax + 4f, rect.width, actH);
                EditorGUI.PropertyField(actRect, actionsProp, new GUIContent("Expression Actions"), true);
            }
        }

        private static int CountName(SerializedProperty listProp, int excludeIndex, string name)
        {
            int count = 0;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                if (i == excludeIndex) continue;
                var n = listProp.GetArrayElementAtIndex(i).FindPropertyRelative("DisplayName")
                        ?? listProp.GetArrayElementAtIndex(i).FindPropertyRelative("Name");
                if (n != null && n.stringValue == name) count++;
            }
            return count;
        }

        private static void EnsureGuids(SerializedProperty listProp)
        {
            if (listProp == null || !listProp.isArray) return;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var guidProp = listProp.GetArrayElementAtIndex(i).FindPropertyRelative("expressionID");
                if (guidProp != null && string.IsNullOrEmpty(guidProp.stringValue))
                    guidProp.stringValue = System.Guid.NewGuid().ToString("N");
            }
        }

        private static HashSet<string> GetDuplicateDisplayNames(SerializedProperty listProp)
        {
            var duplicates = new HashSet<string>();
            if (listProp == null || !listProp.isArray) return duplicates;

            var seen = new Dictionary<string, int>();
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var n = listProp.GetArrayElementAtIndex(i).FindPropertyRelative("DisplayName")
                        ?? listProp.GetArrayElementAtIndex(i).FindPropertyRelative("Name");
                if (n == null || string.IsNullOrWhiteSpace(n.stringValue)) continue;
                if (seen.ContainsKey(n.stringValue)) duplicates.Add(n.stringValue);
                else seen[n.stringValue] = i;
            }
            return duplicates;
        }

        private static void DrawPrefabPreview(Rect rect, SerializedProperty prefabProp)
        {
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.05f));
            if (prefabProp == null || !(prefabProp.objectReferenceValue is GameObject obj))
            {
                GUI.Label(rect, "(No prefab)", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            const float pad = 6f;
            var inner = new Rect(rect.x + pad, rect.y + pad, rect.height - pad * 2f, rect.height - pad * 2f);
            var tex   = AssetPreview.GetAssetPreview(obj) ?? AssetPreview.GetMiniThumbnail(obj);
            if (tex != null)
                GUI.DrawTexture(inner, tex, ScaleMode.ScaleToFit, true);
        }
    }
}
#endif