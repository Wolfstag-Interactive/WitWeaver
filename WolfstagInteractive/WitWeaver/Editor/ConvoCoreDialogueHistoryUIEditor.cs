#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace WolfstagInteractive.ConvoCore.Editor
{
    [CanEditMultipleObjects]
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreDialogueHistoryUIEditor.html")]
[CustomEditor(typeof(ConvoCoreDialogueHistoryUI))]
    public class ConvoCoreDialogueHistoryUIEditor : UnityEditor.Editor
    {
        private SerializedProperty _settingsProp;
        private SerializedProperty _selectedProfileNameProp;
        private SerializedProperty _maxEntriesProp;
        private SerializedProperty _uiReferenceProp;
        private SerializedProperty _rootTransformProp;

        // Cached sub-editor for embedded profile editing
        private UnityEditor.Editor _profileEditor;

        private void OnEnable()
        {
            _settingsProp            = serializedObject.FindProperty("convoCoreSettings");
            _selectedProfileNameProp = serializedObject.FindProperty("selectedProfileName");
            _maxEntriesProp          = serializedObject.FindProperty("maxEntries");
            _uiReferenceProp         = serializedObject.FindProperty("uiReference");
            _rootTransformProp       = serializedObject.FindProperty("rootTransform");

            // Auto-link the settings asset if missing
            if (_settingsProp.objectReferenceValue == null)
            {
                var guid = AssetDatabase.FindAssets("t:ConvoCoreSettings").FirstOrDefault();
                if (!string.IsNullOrEmpty(guid))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var settings = AssetDatabase.LoadAssetAtPath<ConvoCoreSettings>(path);
                    if (settings != null)
                    {
                        _settingsProp.objectReferenceValue = settings;
                        serializedObject.ApplyModifiedPropertiesWithoutUndo();
                        Debug.Log($"[ConvoCore] Auto-linked settings: {path}");
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (_profileEditor != null)
            {
                DestroyImmediate(_profileEditor);
                _profileEditor = null;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Dialogue History UI", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Settings field (read-only)
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(_settingsProp, new GUIContent("ConvoCore Settings"));

            var settings = _settingsProp.objectReferenceValue as ConvoCoreSettings;
            ConvoCoreHistoryRendererProfile selectedProfile = null;

            // ------------------------------------------------------------------
            // Renderer Profile Dropdown
            // ------------------------------------------------------------------
            if (settings != null && settings.HistoryRendererProfiles != null && settings.HistoryRendererProfiles.Count > 0)
            {
                var profiles     = settings.HistoryRendererProfiles.Where(p => p != null).ToList();
                var profileNames = profiles.Select(p => p.RendererName).ToArray();

                string current = _selectedProfileNameProp.hasMultipleDifferentValues
                    ? string.Empty
                    : _selectedProfileNameProp.stringValue;

                int currentIndex = Mathf.Max(0, System.Array.IndexOf(profileNames, current));
                if (currentIndex < 0) currentIndex = 0;

                EditorGUI.showMixedValue = _selectedProfileNameProp.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUILayout.Popup("Renderer Profile", currentIndex, profileNames);
                EditorGUI.showMixedValue = false;

                if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < profileNames.Length)
                {
                    _selectedProfileNameProp.stringValue = profileNames[newIndex];
                    // Reset sub-editor when profile changes
                    if (_profileEditor != null)
                    {
                        DestroyImmediate(_profileEditor);
                        _profileEditor = null;
                    }
                }

                selectedProfile = profiles.ElementAtOrDefault(currentIndex);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No renderer profiles found. Open your ConvoCoreSettings asset and click 'Auto-Populate Renderer Profiles'.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(8);

            // ------------------------------------------------------------------
            // Inline Profile Editor
            // ------------------------------------------------------------------
            if (selectedProfile != null)
            {
                DrawInlineProfileEditor(selectedProfile);
                EditorGUILayout.Space(10);
            }

            // ------------------------------------------------------------------
            // Other serialized fields
            // ------------------------------------------------------------------
            DrawSafeProperty(_maxEntriesProp, "Max Entries");
            // DrawSafeProperty(_rootTransformProp, "Root Transform");
            // DrawSafeProperty(_uiReferenceProp, "UI Reference");

            serializedObject.ApplyModifiedProperties();
        }

        // ------------------------------------------------------------------
        // Draw inline inspector for selected profile
        // ------------------------------------------------------------------
        private void DrawInlineProfileEditor(ConvoCoreHistoryRendererProfile profile)
        {
            if (_profileEditor == null || _profileEditor.target != profile)
            {
                DestroyImmediate(_profileEditor);
                _profileEditor = CreateEditor(profile);
            }

            if (_profileEditor != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Renderer Profile Editor", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                // Render all fields except name first
                var so = new SerializedObject(profile);
                so.Update();
                var nameProp = so.FindProperty("rendererName");
                DrawPropertiesExcluding(so, "m_Script", "rendererName");
                // Draw name as non-editable
                using (new EditorGUI.DisabledScope(true))
                {
                    if (nameProp != null)
                        EditorGUILayout.PropertyField(nameProp, new GUIContent("Renderer Name"));
                }
                so.ApplyModifiedProperties();

                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("Failed to create profile editor.", MessageType.Warning);
            }
        }

        // ------------------------------------------------------------------
        // Helper to avoid null errors
        // ------------------------------------------------------------------
        private void DrawSafeProperty(SerializedProperty prop, string label)
        {
            if (prop == null)
            {
                EditorGUILayout.HelpBox($"Missing serialized field: {label} (check field name in script).", MessageType.Warning);
                return;
            }
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
        }
    }
}
#endif