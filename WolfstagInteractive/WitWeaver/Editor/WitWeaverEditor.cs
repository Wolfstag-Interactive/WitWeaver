using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.Editor
{
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreEditor.html")]
    [CustomEditor(typeof(ConvoCore))]
    public class ConvoCoreEditor : UnityEditor.Editor
    {
        private ConvoCoreLanguageManager _convoCoreLanguageManager;
        private int _selectedLanguageIndex;
        private bool _indexInitialized;

        public override void OnInspectorGUI()
        {
            // Get the target object
            ConvoCore convoCore = (ConvoCore)target;
            // Access the LanguageManager Singleton instance
            _convoCoreLanguageManager = ConvoCoreLanguageManager.Instance;

            // Check if the LanguageManager is initialized
            if (_convoCoreLanguageManager == null || _convoCoreLanguageManager.GetSupportedLanguages() == null)
            {
                EditorGUILayout.HelpBox(
                    "ConvoCoreSettings not found or not configured properly. Please create and configure it using the menu.",
                    MessageType.Error);
                EditorGUILayout.Space();

                if (GUILayout.Button("Open Settings (or Create if Missing)"))
                {
                    ConvoCoreMenuItems.OpenSettings();
                }

                return;
            }

            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Language Control", EditorStyles.boldLabel);

            // Info box about global language settings
            EditorGUILayout.HelpBox(
                "Language is controlled globally in ConvoCoreSettings. Changes here affect all conversations.",
                MessageType.Info);

            // Display the current language
            EditorGUILayout.LabelField("Current Language:", _convoCoreLanguageManager.CurrentLanguage);

            // Display dropdown to select a language
            var supportedLanguages = _convoCoreLanguageManager.GetSupportedLanguages();
            if (supportedLanguages is { Count: > 0 })
            {
                // Initialize the dropdown index only once or when language changes externally
                if (!_indexInitialized || !IsValidIndex(supportedLanguages))
                {
                    _selectedLanguageIndex = Mathf.Max(0, supportedLanguages.IndexOf(_convoCoreLanguageManager.CurrentLanguage));
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
                EditorGUI.BeginDisabledGroup(supportedLanguages[_selectedLanguageIndex] == _convoCoreLanguageManager.CurrentLanguage);
                if (GUILayout.Button("Apply Language"))
                {
                    var selectedLanguage = supportedLanguages[_selectedLanguageIndex];
                    _convoCoreLanguageManager.SetLanguage(selectedLanguage);
                    Debug.Log($"Language applied globally: {selectedLanguage}");
                    convoCore.UpdateUIForLanguage(selectedLanguage);
                }
                EditorGUI.EndDisabledGroup();

                // Button to open settings
                if (GUILayout.Button("Open ConvoCoreSettings"))
                {
                    ConvoCoreMenuItems.OpenSettings();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No supported languages found! Please configure ConvoCoreSettings.",
                    MessageType.Warning);

                if (GUILayout.Button("Open Settings"))
                {
                    ConvoCoreMenuItems.OpenSettings();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Conversation Control", EditorStyles.boldLabel);

            // Add a button to start the conversation
            if (GUILayout.Button("Start Conversation"))
            {
                convoCore.PlayConversation();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current Conversation State:", convoCore.CurrentDialogueState.ToString());
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