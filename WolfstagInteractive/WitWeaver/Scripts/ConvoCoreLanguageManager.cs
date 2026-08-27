using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreLanguageManager.html")]
    public class ConvoCoreLanguageManager
    {
        private static ConvoCoreLanguageManager _instance;

        public static ConvoCoreLanguageManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ConvoCoreLanguageManager();
                    _instance.Initialize();
                }
                return _instance;
            }
        }

        private ConvoCoreSettings _convoCoreSettings;

        public string CurrentLanguage
        {
            get
            {
                if (_convoCoreSettings != null)
                    return _convoCoreSettings.CurrentLanguage;
                
                return "EN"; // fallback
            }
        }

        public static Action<string> OnLanguageChanged { get; set; }

        private ConvoCoreLanguageManager() { }

        private void Initialize()
        {
            // Load settings - try Resources first, then look in project
            _convoCoreSettings = ConvoCoreYamlLoader.Settings;
            
            if (_convoCoreSettings == null)
            {
                // Try to load from Resources as fallback
                _convoCoreSettings = Resources.Load<ConvoCoreSettings>("ConvoCoreSettings");
            }

            if (_convoCoreSettings == null)
            {
#if UNITY_EDITOR
                // In editor, try to find it in the project
                var guids = UnityEditor.AssetDatabase.FindAssets("t:ConvoCoreSettings");
                if (guids.Length > 0)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _convoCoreSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<ConvoCoreSettings>(path);
                    
                    // Auto-assign to the loader for next time
                    if (_convoCoreSettings != null)
                    {
                        ConvoCoreYamlLoader.Settings = _convoCoreSettings;
                    }
                }
#endif
            }

            if (_convoCoreSettings == null)
            {
                Debug.LogError("ConvoCoreSettings not found! Please create one via Tools > ConvoCore > Open Settings (or Create if Missing)");
                return;
            }

            if (_convoCoreSettings.SupportedLanguages == null || _convoCoreSettings.SupportedLanguages.Count == 0)
            {
                Debug.LogWarning("ConvoCoreSettings has no supported languages. Adding default 'EN'.");
                _convoCoreSettings.SupportedLanguages = new List<string> { "EN" };
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(_convoCoreSettings);
#endif
            }

            if (string.IsNullOrEmpty(_convoCoreSettings.CurrentLanguage))
            {
                _convoCoreSettings.CurrentLanguage = _convoCoreSettings.SupportedLanguages[0];
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(_convoCoreSettings);
#endif
            }

            bool verboseLogs = _convoCoreSettings.VerboseLogs;
            if (verboseLogs)
                Debug.Log($"LanguageManager initialized with language: {_convoCoreSettings.CurrentLanguage}");
        }

        public List<string> GetSupportedLanguages()
        {
            if (_convoCoreSettings != null &&
                _convoCoreSettings.SupportedLanguages != null &&
                _convoCoreSettings.SupportedLanguages.Count > 0)
            {
                return _convoCoreSettings.SupportedLanguages;
            }

            return new List<string> { "EN" };
        }

        public void SetLanguage(string newLanguage)
        {
            var supportedLanguages = GetSupportedLanguages();

            if (supportedLanguages == null || supportedLanguages.Count == 0)
            {
                Debug.LogWarning("Language settings are not loaded.");
                return;
            }

            // case-insensitive match, but keep the project's canonical casing
            var match = supportedLanguages
                .Find(l => string.Equals(l?.Trim(), newLanguage?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(match))
            {
                if (_convoCoreSettings != null)
                {
                    _convoCoreSettings.CurrentLanguage = match;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(_convoCoreSettings);
#endif
                }

                bool verboseLogs = _convoCoreSettings?.VerboseLogs ?? false;
                if (verboseLogs)
                    Debug.Log($"Language set to: {match}");

                OnLanguageChanged?.Invoke(match);
            }
            else
            {
                Debug.LogWarning($"'{newLanguage}' is not a supported language.");
            }
        }
    }
}