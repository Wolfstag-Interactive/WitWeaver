#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace WolfstagInteractive.ConvoCoreEditor
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCoreEditor_1_1RepresentationMappingListEditor.html")]
    public static class RepresentationMappingListEditor
    {
        public static ReorderableList Build(
            SerializedObject so,
            SerializedProperty listProp,
            System.Func<SerializedProperty, string> headerLabel,
            System.Action<Rect, SerializedProperty> drawBody)
        {
            var rl = new ReorderableList(so, listProp, true, true, true, true);

            rl.drawHeaderCallback = rect => EditorGUI.LabelField(rect, listProp.displayName);

            rl.elementHeightCallback = index =>
            {
                var element = listProp.GetArrayElementAtIndex(index);
                // ~ line height * N + padding. Adjust if you add more fields.
                return EditorGUIUtility.singleLineHeight * 5f + 10f;
            };

            rl.drawElementCallback = (rect, index, active, focused) =>
            {
                var element = listProp.GetArrayElementAtIndex(index);
                rect.height = EditorGUIUtility.singleLineHeight;

                // Header row: DisplayName as label
                var nameProp = element.FindPropertyRelative("DisplayName");
                string header = headerLabel != null ? headerLabel(element) :
                                (nameProp != null ? nameProp.stringValue : $"Element {index}");
                EditorGUI.LabelField(rect, header, EditorStyles.boldLabel);

                rect.y += EditorGUIUtility.singleLineHeight + 2;

                // Read-only GUID
                var guidProp = element.FindPropertyRelative("_expressionId");
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.PropertyField(rect, guidProp, new GUIContent("Expression GUID"));
                }

                rect.y += EditorGUIUtility.singleLineHeight + 2;

                // Let caller draw the rest (e.g., sprites/animator/material/options)
                drawBody?.Invoke(rect, element);
            };

            return rl;
        }
    }
}
#endif