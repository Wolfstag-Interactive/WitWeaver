#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.Editor
{
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreSceneCharacterRegistrantEditor.html")]
[CustomEditor(typeof(ConvoCoreSceneCharacterRegistrant))]
    public class ConvoCoreSceneCharacterRegistrantEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var registrant = (ConvoCoreSceneCharacterRegistrant)target;

            // Check whether a registry exists in the scene.
            bool hasRegistry = FindAnyObjectByType<ConvoCoreSceneCharacterRegistry>() != null;

            if (!hasRegistry)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(
                    "No ConvoCoreSceneCharacterRegistry found in the scene. " +
                    "Characters will not be registered at runtime. Add a registry or assign one directly.",
                    MessageType.Warning);

                if (GUILayout.Button("Add Registry to Scene"))
                {
                    var go = new GameObject("ConvoCoreSceneCharacterRegistry");
                    go.AddComponent<ConvoCoreSceneCharacterRegistry>();
                    Undo.RegisterCreatedObjectUndo(go, "Add ConvoCoreSceneCharacterRegistry");
                    Selection.activeGameObject = go;
                }
            }
        }
    }
}
#endif
