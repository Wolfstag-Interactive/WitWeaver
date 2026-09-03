// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverEditor.html")]
    [CustomEditor(typeof(WitWeaver))]
    public class WitWeaverEditor : UnityEditor.Editor
    {
        private WitWeaverLanguageManager _witWeaverLanguageManager;
        private int _selectedLanguageIndex;
        private bool _indexInitialized;

        public override void OnInspectorGUI()
        {
            // Get the target object
            WitWeaver witWeaver = (WitWeaver)target;
            // Access the LanguageManager Singleton instance
            _witWeaverLanguageManager = WitWeaverLanguageManager.Instance;

            // Check if the LanguageManager is initialized
            if (_witWeaverLanguageManager == null || _witWeaverLanguageManager.GetSupportedLanguages() == null)
            {
                EditorGUILayout.HelpBox(
                    "WitWeaverSettings not found or not configured properly. Please create and configure it using the menu.",
                    MessageType.Error);
                EditorGUILayout.Space();

                if (GUILayout.Button("Open Settings (or Create if Missing)"))
                {
                    WitWeaverMenuItems.OpenSettings();
                }

                return;
            }

            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Language Control", EditorStyles.boldLabel);

            // Info box about global language settings
            EditorGUILayout.HelpBox(
                "Language is controlled globally in WitWeaverSettings. Changes here affect all conversations.",
                MessageType.Info);

            // Display the current language
            EditorGUILayout.LabelField("Current Language:", _witWeaverLanguageManager.CurrentLanguage);

            // Display dropdown to select a language
            var supportedLanguages = _witWeaverLanguageManager.GetSupportedLanguages();
            if (supportedLanguages is { Count: > 0 })
            {
                // Initialize the dropdown index only once or when language changes externally
                if (!_indexInitialized || !IsValidIndex(supportedLanguages))
                {
                    _selectedLanguageIndex = Mathf.Max(0, supportedLanguages.IndexOf(_witWeaverLanguageManager.CurrentLanguage));
                    _indexInitialized = true;
                }

                // Render the dropdown and track changes
                EditorGUI.BeginChangeCheck();
                int newSelectedIndex = EditorGUILayout.Popup("Select a Language:", _selectedLanguageIndex,
                    supportedLanguages.ToArray());
                
                if (EditorGUI.EndChangeCheck())
                {
                    _selectedLanguageIndex = newSelectedIndex;
                }

                // Add a button to apply the selected language
                EditorGUI.BeginDisabledGroup(supportedLanguages[_selectedLanguageIndex] == _witWeaverLanguageManager.CurrentLanguage);
                if (GUILayout.Button("Apply Language"))
                {
                    var selectedLanguage = supportedLanguages[_selectedLanguageIndex];
                    _witWeaverLanguageManager.SetLanguage(selectedLanguage);
                    Debug.Log($"Language applied globally: {selectedLanguage}");
                    witWeaver.UpdateUIForLanguage(selectedLanguage);
                }
                EditorGUI.EndDisabledGroup();

                // Button to open settings
                if (GUILayout.Button("Open WitWeaverSettings"))
                {
                    WitWeaverMenuItems.OpenSettings();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No supported languages found! Please configure WitWeaverSettings.",
                    MessageType.Warning);

                if (GUILayout.Button("Open Settings"))
                {
                    WitWeaverMenuItems.OpenSettings();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Conversation Control", EditorStyles.boldLabel);

            // Add a button to start the conversation
            if (GUILayout.Button("Start Conversation"))
            {
                witWeaver.PlayConversation();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current Conversation State:", witWeaver.CurrentDialogueState.ToString());
        }

        private bool IsValidIndex(List<string> supportedLanguages)
        {
            return _selectedLanguageIndex >= 0 && 
                   _selectedLanguageIndex < supportedLanguages.Count && 
                   supportedLanguages[_selectedLanguageIndex] != null;
        }

        private void OnEnable()
        {
            // Reset initialization when editor is enabled
            _indexInitialized = false;
        }
    }
}