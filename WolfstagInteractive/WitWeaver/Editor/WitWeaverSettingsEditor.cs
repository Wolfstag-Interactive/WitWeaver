// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverSettingsEditor.html")]
    [CustomEditor(typeof(WitWeaverSettings))]
    public class WitWeaverSettingsEditor : UnityEditor.Editor
    {
        private const string TAB_PREF_KEY = "WitWeaver.SettingsEditor.ActiveTab";
        private static readonly string[] TAB_LABELS = { "General", "Language", "Save System", "Dialogue History Renderers", "Spreadsheet" };

        // Serialized properties
        private SerializedProperty _sourceOrderProp;
        private SerializedProperty _supportedLanguagesProp;
        private SerializedProperty _currentLanguageProp;
        private SerializedProperty _goBackLabelProp;
        private SerializedProperty _verboseLogsProp;
        private SerializedProperty _saveKeyPrefixProp;
        private SerializedProperty _enableSaveSystemProp;
        private SerializedProperty _enableVariableStoreProp;
        private SerializedProperty _enableLanguageSystemProp;
        private SerializedProperty _historyRendererProfilesProp;

        // Spreadsheet properties
        private SerializedProperty _spreadsheetCharacterIDHeaderProp;
        private SerializedProperty _spreadsheetLineIDHeaderProp;
        private SerializedProperty _spreadsheetSkipSheetPrefixProp;
        private SerializedProperty _spreadsheetHeaderRowIndexProp;
        private SerializedProperty _spreadsheetSkipEmptyRowsProp;
        private SerializedProperty _spreadsheetWarnOnUnrecognizedColumnsProp;
        private SerializedProperty _spreadsheetFormulaCellBehaviorProp;

        private ReorderableList _rendererList;
        private int _activeTab;

        private void OnEnable()
        {
            _activeTab = EditorPrefs.GetInt(TAB_PREF_KEY, 0);

            _sourceOrderProp                    = serializedObject.FindProperty("SourceOrder");
            _supportedLanguagesProp             = serializedObject.FindProperty("SupportedLanguages");
            _currentLanguageProp                = serializedObject.FindProperty("CurrentLanguage");
            _goBackLabelProp                    = serializedObject.FindProperty("GoBackLabel");
            _verboseLogsProp                    = serializedObject.FindProperty("VerboseLogs");
            _saveKeyPrefixProp                  = serializedObject.FindProperty("SaveKeyPrefix");
            _enableSaveSystemProp               = serializedObject.FindProperty("EnableSaveSystem");
            _enableVariableStoreProp            = serializedObject.FindProperty("EnableVariableStore");
            _enableLanguageSystemProp           = serializedObject.FindProperty("EnableLanguageSystem");
            _historyRendererProfilesProp        = serializedObject.FindProperty("historyRendererProfiles");

            _spreadsheetCharacterIDHeaderProp         = serializedObject.FindProperty("SpreadsheetCharacterIDHeader");
            _spreadsheetLineIDHeaderProp              = serializedObject.FindProperty("SpreadsheetLineIDHeader");
            _spreadsheetSkipSheetPrefixProp           = serializedObject.FindProperty("SpreadsheetSkipSheetPrefix");
            _spreadsheetHeaderRowIndexProp            = serializedObject.FindProperty("SpreadsheetHeaderRowIndex");
            _spreadsheetSkipEmptyRowsProp             = serializedObject.FindProperty("SpreadsheetSkipEmptyRows");
            _spreadsheetWarnOnUnrecognizedColumnsProp = serializedObject.FindProperty("SpreadsheetWarnOnUnrecognizedColumns");
            _spreadsheetFormulaCellBehaviorProp       = serializedObject.FindProperty("SpreadsheetFormulaCellBehavior");

            BuildRendererList();
        }

        private void BuildRendererList()
        {
            if (_historyRendererProfilesProp == null) return;

            _rendererList = new ReorderableList(serializedObject, _historyRendererProfilesProp, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Renderer Profiles"),
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var element = _historyRendererProfilesProp.GetArrayElementAtIndex(index);
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;

                    var profile = element.objectReferenceValue as WitWeaverHistoryRendererProfile;
                    var label = profile != null ? profile.RendererName : "(unnamed)";

                    EditorGUI.PropertyField(rect, element, new GUIContent(label));
                },
                elementHeight = EditorGUIUtility.singleLineHeight + 4
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var settings = (WitWeaverSettings)target;

            // Tab bar
            EditorGUI.BeginChangeCheck();
            var newTab = GUILayout.Toolbar(_activeTab, TAB_LABELS);
            if (EditorGUI.EndChangeCheck())
            {
                _activeTab = newTab;
                EditorPrefs.SetInt(TAB_PREF_KEY, _activeTab);
            }

            EditorGUILayout.Space(4);

            // Draw active tab content
            EditorGUI.BeginChangeCheck();

            switch (_activeTab)
            {
                case 0: DrawGeneralTab(settings); break;
                case 1: DrawLanguageTab(settings); break;
                case 2: DrawSaveSystemTab(); break;
                case 3: DrawHistoryRenderersTab(settings); break;
                case 4: DrawSpreadsheetTab(settings); break;
            }

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(target);

            // Always-visible Open About Window button
            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Open About Window"))
                WitWeaverAboutWindow.Open();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGeneralTab(WitWeaverSettings settings)
        {
            EditorGUILayout.LabelField("YAML Source Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_sourceOrderProp, true);
            EditorGUILayout.HelpBox(
                "Sources are checked in order from top to bottom; the first source that returns content wins. " +
                "AssignedTextAsset reads the YAML embedded in the Conversation Data asset (no runtime I/O). " +
                "Persistent reads an optional device-side override from persistentDataPath/WitWeaver/Dialogue/.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_verboseLogsProp);
        }

        private void DrawLanguageTab(WitWeaverSettings settings)
        {
            EditorGUILayout.LabelField("Language Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_supportedLanguagesProp, new GUIContent("Supported Languages"), true);

            EditorGUILayout.Space();

            if (settings.SupportedLanguages != null && settings.SupportedLanguages.Count > 0)
            {
                int currentIndex = settings.SupportedLanguages.IndexOf(settings.CurrentLanguage);
                if (currentIndex < 0) currentIndex = 0;

                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUILayout.Popup("Current Language", currentIndex, settings.SupportedLanguages.ToArray());
                if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < settings.SupportedLanguages.Count)
                {
                    _currentLanguageProp.stringValue = settings.SupportedLanguages[newIndex];
                    WitWeaverLanguageManager.Instance?.SetLanguage(settings.SupportedLanguages[newIndex]);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Add at least one supported language!", MessageType.Warning);
            }

            EditorGUILayout.Space();

            // System strings: framework-owned button text, one row per supported language.
            EditorGUILayout.LabelField("System Strings", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(new GUIContent("Go Back Option",
                "Shown as the last option on player choice lines that have Allow Go Back enabled."));

            if (_goBackLabelProp != null)
                WitWeaverChoiceLabelsDrawer.DrawLayout(_goBackLabelProp, settings.SupportedLanguages,
                    drawHeader: false);
        }

        private void DrawSaveSystemTab()
        {
            EditorGUILayout.LabelField("Save System", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_saveKeyPrefixProp);
            EditorGUILayout.PropertyField(_enableSaveSystemProp);
            EditorGUILayout.PropertyField(_enableVariableStoreProp);
            EditorGUILayout.PropertyField(_enableLanguageSystemProp);
        }

        private void DrawHistoryRenderersTab(WitWeaverSettings settings)
        {
            EditorGUILayout.LabelField("Dialogue History Renderers", EditorStyles.boldLabel);

            if (_rendererList != null)
                _rendererList.DoLayoutList();

            if (GUILayout.Button("Clean Null Entries"))
            {
                settings.CleanRendererProfiles();
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Auto-Populate Renderer Profiles"))
                PopulateRendererProfiles(settings);

            EditorGUILayout.HelpBox(
                "Renderer Profiles define which dialogue history renderers are available. " +
                "Click 'Auto-Populate' to discover and generate profiles for all IWitWeaverHistoryRenderer " +
                "implementations in your project.",
                MessageType.Info);
        }

        private void DrawSpreadsheetTab(WitWeaverSettings settings)
        {
            EditorGUILayout.HelpBox(
                "These settings apply to all spreadsheet imports project-wide. " +
                "Column header names are case-insensitive. Changes take effect on the next import.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_spreadsheetCharacterIDHeaderProp, new GUIContent("Character ID Header"));
            EditorGUILayout.PropertyField(_spreadsheetLineIDHeaderProp, new GUIContent("Line ID Header"));
            EditorGUILayout.PropertyField(_spreadsheetSkipSheetPrefixProp, new GUIContent("Skip Sheet Prefix"));
            EditorGUILayout.PropertyField(_spreadsheetHeaderRowIndexProp, new GUIContent("Header Row Index"));
            EditorGUILayout.PropertyField(_spreadsheetSkipEmptyRowsProp, new GUIContent("Skip Empty Rows"));
            EditorGUILayout.PropertyField(_spreadsheetWarnOnUnrecognizedColumnsProp, new GUIContent("Warn On Unrecognized Columns"));
            EditorGUILayout.PropertyField(_spreadsheetFormulaCellBehaviorProp, new GUIContent("Formula Cell Behavior"));

            // Warn if required headers are empty
            var charHeader = _spreadsheetCharacterIDHeaderProp?.stringValue;
            var lineHeader = _spreadsheetLineIDHeaderProp?.stringValue;
            if (string.IsNullOrEmpty(charHeader) || string.IsNullOrEmpty(lineHeader))
            {
                EditorGUILayout.HelpBox(
                    "CharacterID Header and LineID Header must not be empty. " +
                    "All spreadsheet imports will fail until these are set.",
                    MessageType.Warning);
            }
        }

        private static void Separator(float thickness = 1f, float margin = 4f, float alpha = 0.2f)
        {
            GUILayout.Space(margin);
            var rect = EditorGUILayout.GetControlRect(false, thickness);
            rect.height = thickness;
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, alpha));
            GUILayout.Space(margin);
        }

        private void PopulateRendererProfiles(WitWeaverSettings settings)
        {
            WitWeaverHistoryRendererRegistry.DiscoverRenderers();
            var names = WitWeaverHistoryRendererRegistry.GetRendererNames();

            if (names == null || names.Length == 0)
            {
                Debug.LogWarning("[WitWeaverSettings] No IWitWeaverHistoryRenderer implementations found.");
                return;
            }

            string folder = "Assets/WitWeaver/Generated/RendererProfiles";
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            int created = 0;
            foreach (var name in names)
            {
                if (settings.HistoryRendererProfiles != null)
                {
                    bool exists = false;
                    foreach (var p in settings.HistoryRendererProfiles)
                    {
                        if (p != null && p.RendererName == name) { exists = true; break; }
                    }
                    if (exists) continue;
                }

                var profile = ScriptableObject.CreateInstance<WitWeaverHistoryRendererProfile>();
                profile.UpdateFromDiscovered(name);

                string assetPath = Path.Combine(folder, $"{name}RendererProfile.asset");
                AssetDatabase.CreateAsset(profile, assetPath);
                settings.AddRendererProfile(profile);
                created++;
            }

            settings.CleanRendererProfiles();
            BuildRendererList();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log($"[WitWeaverSettings] Added {created} new renderer profile(s) to settings.");
        }
    }
}
#endif