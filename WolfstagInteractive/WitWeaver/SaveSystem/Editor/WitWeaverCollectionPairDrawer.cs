using UnityEngine;
using UnityEditor;
using WolfstagInteractive.WitWeaver.SaveSystem;

namespace WolfstagInteractive.WitWeaver.SaveSystem.Editor
{
    /// <summary>
    /// Draws a Collection sub-entry as a single row (sub-key field + typed value field).
    /// Rows with an empty sub-key, or a sub-key that occurs more than once within the same
    /// Collection, are tinted with the error color. Duplicates block neither entry — the
    /// first occurrence wins when the runtime dictionary is rebuilt.
    /// </summary>
    [CustomPropertyDrawer(typeof(CollectionIntPair))]
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1SaveSystem_1_1Editor_1_1WitWeaverCollectionPairDrawer.html")]
[CustomPropertyDrawer(typeof(CollectionStringPair))]
    public class WitWeaverCollectionPairDrawer : PropertyDrawer
    {
        private static readonly Color k_ErrorTint = new Color(1f, 0.25f, 0.25f, 0.18f);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var subKeyProp = property.FindPropertyRelative("SubKey");
            var valueProp  = property.FindPropertyRelative("Value");
            if (subKeyProp == null || valueProp == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            if (IsInvalidSubKey(property, subKeyProp.stringValue))
                EditorGUI.DrawRect(position, k_ErrorTint);

            float keyW = position.width * 0.45f;
            EditorGUI.PropertyField(
                new Rect(position.x, position.y, keyW - 4f, EditorGUIUtility.singleLineHeight),
                subKeyProp,
                new GUIContent(string.Empty, "Sub-key. Must be unique within this Collection and non-empty."));
            EditorGUI.PropertyField(
                new Rect(position.x + keyW, position.y, position.width - keyW, EditorGUIUtility.singleLineHeight),
                valueProp, GUIContent.none);
        }

        private static bool IsInvalidSubKey(SerializedProperty pairProp, string subKey)
        {
            if (string.IsNullOrEmpty(subKey))
                return true;

            // "...CollectionIntPairs.Array.data[3]" -> "...CollectionIntPairs"
            string path = pairProp.propertyPath;
            int arraySuffix = path.LastIndexOf(".Array.data[", System.StringComparison.Ordinal);
            if (arraySuffix < 0) return false;

            var arrayProp = pairProp.serializedObject.FindProperty(path.Substring(0, arraySuffix));
            if (arrayProp == null || !arrayProp.isArray) return false;

            int occurrences = 0;
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var sibling = arrayProp.GetArrayElementAtIndex(i).FindPropertyRelative("SubKey");
                if (sibling != null && sibling.stringValue == subKey && ++occurrences > 1)
                    return true;
            }
            return false;
        }
    }
}
