#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace WolfstagInteractive.ConvoCore.Editor
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1GameObjectReferenceDrawer.html")]
[CustomPropertyDrawer(typeof(GameObjectReference))]
    public class GameObjectReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var findMethodProp = property.FindPropertyRelative("findMethod");
            var directRefProp = property.FindPropertyRelative("directReference");
            var objectNameProp = property.FindPropertyRelative("objectName");
            var tagNameProp = property.FindPropertyRelative("tagName");

            var labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, label);

            var enumRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(enumRect, findMethodProp, new GUIContent("Find Method"));

            var valueRect = new Rect(position.x, position.y + (EditorGUIUtility.singleLineHeight + 2) * 2, position.width, EditorGUIUtility.singleLineHeight);

            GameObjectReference.FindMethod method = (GameObjectReference.FindMethod)findMethodProp.enumValueIndex;
            
            switch (method)
            {
                case GameObjectReference.FindMethod.DirectReference:
                    EditorGUI.PropertyField(valueRect, directRefProp, new GUIContent("GameObject"));
                    break;
                    
                case GameObjectReference.FindMethod.ByName:
                case GameObjectReference.FindMethod.ByNameInChildren:
                    EditorGUI.PropertyField(valueRect, objectNameProp, new GUIContent("Object Name"));
                    break;
                    
                case GameObjectReference.FindMethod.ByTag:
                case GameObjectReference.FindMethod.ByTagInChildren:
                    objectNameProp.stringValue = EditorGUI.TagField(valueRect, "Tag", tagNameProp.stringValue);
                    break;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight + 2) * 3;
        }
    }
}
#endif