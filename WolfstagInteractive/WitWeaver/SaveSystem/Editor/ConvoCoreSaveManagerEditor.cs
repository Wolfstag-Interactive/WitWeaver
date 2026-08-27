#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using WolfstagInteractive.ConvoCore.SaveSystem;
using YamlDotNet.Serialization;

namespace WolfstagInteractive.ConvoCore.SaveSystem.Editor
{
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SaveSystem_1_1Editor_1_1ConvoCoreSaveManagerEditor.html")]
    [CustomEditor(typeof(ConvoCoreSaveManager))]
    public class ConvoCoreSaveManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _variableStoreProp;
        private SerializedProperty _settingsStateProp;
        private SerializedProperty _useYamlProp;
        private SerializedProperty _defaultSlotProp;

        private void OnEnable()
        {
            _variableStoreProp = serializedObject.FindProperty("VariableStore");
            _settingsStateProp = serializedObject.FindProperty("SettingsState");
            _useYamlProp = serializedObject.FindProperty("_useYaml");
            _defaultSlotProp = serializedObject.FindProperty("_defaultSlot");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var manager = (ConvoCoreSaveManager)target;

            // References
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_variableStoreProp);
            EditorGUILayout.PropertyField(_settingsStateProp);

            // Validation warnings
            if (_variableStoreProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Variable Store is not assigned. Variables will not be saved or restored.", MessageType.Warning);
            }

            if (_settingsStateProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Settings State is not assigned. Settings will not be saved or restored.", MessageType.Warning);
            }

            EditorGUILayout.Space();

            // Provider selector
            EditorGUILayout.LabelField("Provider", EditorStyles.boldLabel);

            int providerChoice = _useYamlProp.boolValue ? 0 : 1;
            int newChoice = GUILayout.SelectionGrid(providerChoice, new[] { "YAML", "JSON" }, 2);
            if (newChoice != providerChoice)
            {
                _useYamlProp.boolValue = newChoice == 0;
            }

            EditorGUILayout.Space();

            // Default slot
            EditorGUILayout.PropertyField(_defaultSlotProp, new GUIContent("Default Save Slot"));

            // Play Mode section
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Play Mode State", EditorStyles.boldLabel);

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Toggle("Is Initialized", manager.IsInitialized);
                EditorGUILayout.TextField("Provider Type", manager.Provider != null
                    ? manager.Provider.GetType().Name
                    : "(none)");
                EditorGUILayout.IntField("Registered Snapshots", manager.ConversationSnapshotCount);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();

                if (GUILayout.Button("Save to Default Slot"))
                {
                    manager.SaveToDefaultSlot();
                }

                if (GUILayout.Button("Load from Default Slot"))
                {
                    manager.LoadFromDefaultSlot();
                }

                if (GUILayout.Button("Preview Full Snapshot"))
                {
                    var snapshot = manager.GetGameSnapshot();
                    var serializer = new SerializerBuilder().Build();
                    var yaml = serializer.Serialize(snapshot);
                    Debug.Log($"[ConvoCoreSaveManager] Full Snapshot Preview:\n{yaml}");
                }

                EditorGUILayout.Space();

                var deleteStyle = new GUIStyle(GUI.skin.button);
                deleteStyle.normal.textColor = Color.red;
                if (GUILayout.Button("Clear All Saves", deleteStyle))
                {
                    if (EditorUtility.DisplayDialog("Clear All Saves",
                        "Are you sure you want to delete all save files? This cannot be undone.",
                        "Delete All", "Cancel"))
                    {
                        var savePath = System.IO.Path.Combine(Application.persistentDataPath, "ConvoCoreSaves");
                        if (System.IO.Directory.Exists(savePath))
                        {
                            System.IO.Directory.Delete(savePath, true);
                            Debug.Log("[ConvoCoreSaveManager] All save files deleted.");
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif