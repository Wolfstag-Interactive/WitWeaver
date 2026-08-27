#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WorldPointBehaviourEditor.html")]
    [CustomEditor(typeof(WorldPointBehaviour))]
    public class WorldPointBehaviourEditor : UnityEditor.Editor
    {
        private ReorderableList _list;

        private void OnEnable()
        {
            var worldPointsProp = serializedObject.FindProperty("_worldPoints");
            if (worldPointsProp == null) return;

            _list = new ReorderableList(serializedObject, worldPointsProp, true, true, true, true);

            _list.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "World Points");

            _list.elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 4f;

            _list.drawElementCallback = (rect, index, active, focused) =>
            {
                var element = worldPointsProp.GetArrayElementAtIndex(index);
                var idProp  = element.FindPropertyRelative("SpawnPointId");
                if (idProp == null) return;

                rect.y      += 2f;
                rect.height  = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(rect, idProp, new GUIContent($"Slot {index}"));
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_list == null)
            {
                DrawDefaultInspector();
                return;
            }

            _list.DoLayoutList();

            // Validation: collect IDs that have no matching WitWeaverSpawnPoint in the scene.
            var worldPointsProp = serializedObject.FindProperty("_worldPoints");
            if (worldPointsProp != null && worldPointsProp.isArray)
            {
                var missing = new List<string>();
                var registry = FindAnyObjectByType<WitWeaverSpawnPointRegistry>();

                for (int i = 0; i < worldPointsProp.arraySize; i++)
                {
                    var id = worldPointsProp.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("SpawnPointId")?.stringValue;

                    if (string.IsNullOrEmpty(id)) continue;
                    if (registry == null || !registry.Contains(id))
                        missing.Add(id);
                }

                if (missing.Count > 0)
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.HelpBox(
                        "The following Spawn Point ID(s) have no matching WitWeaverSpawnPoint in the scene:\n" +
                        string.Join("\n", missing) +
                        "\n\nAdd WitWeaverSpawnPoint components to scene GameObjects and set their Spawn Point ID to match.",
                        MessageType.Warning);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
