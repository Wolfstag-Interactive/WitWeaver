using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverLanguageManager.html")]
    public class WitWeaverLanguageManager
    {
        private static WitWeaverLanguageManager _instance;

        public static WitWeaverLanguageManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new WitWeaverLanguageManager();
                    _instance.Initialize();
                }
                return _instance;
            }
        }

        private WitWeaverSettings _witWeaverSettings;

        public string CurrentLanguage
        {
            get
            {
                if (_witWeaverSettings != null)
                    return _witWeaverSettings.CurrentLanguage;
                
                return "EN"; // fallback
            }
        }

        public static Action<string> OnLanguageChanged { get; set; }

        private WitWeaverLanguageManager() { }

        private void Initialize()
        {
            // Load settings - try Resources first, then look in project
            _witWeaverSettings = WitWeaverYamlLoader.Settings;
            
            if (_witWeaverSettings == null)
            {
                // Try to load from Resources as fallback
                _witWeaverSettings = Resources.Load<WitWeaverSettings>("WitWeaverSettings");
            }

            if (_witWeaverSettings == null)
            {
#if UNITY_EDITOR
                // In editor, try to find it in the project
                var guids = UnityEditor.AssetDatabase.FindAssets("t:WitWeaverSettings");
                if (guids.Length > 0)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _witWeaverSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<WitWeaverSettings>(path);
                    
                    // Auto-assign to the loader for next time
                    if (_witWeaverSettings != null)
                    {
                        WitWeaverYamlLoader.Settings = _witWeaverSettings;
                    }
                }
#endif
            }

            if (_witWeaverSettings == null)
            {
                Debug.LogError("WitWeaverSettings not found! Please create one via Tools > WitWeaver > Open Settings (or Create if Missing)");
                return;
            }

            if (_witWeaverSettings.SupportedLanguages == null || _witWeaverSettings.SupportedLanguages.Count == 0)
            {
                Debug.LogWarning("WitWeaverSettings has no supported languages. Adding default 'EN'.");
                _witWeaverSettings.SupportedLanguages = new List<string> { "EN" };
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(_witWeaverSettings);
#endif
            }

            if (string.IsNullOrEmpty(_witWeaverSettings.CurrentLanguage))
            {
                _witWeaverSettings.CurrentLanguage = _witWeaverSettings.SupportedLanguages[0];
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(_witWeaverSettings);
#endif
            }

            bool verboseLogs = _witWeaverSettings.VerboseLogs;
            if (verboseLogs)
                Debug.Log($"LanguageManager initialized with language: {_witWeaverSettings.CurrentLanguage}");
        }

        public List<string> GetSupportedLanguages()
        {
            if (_witWeaverSettings != null &&
                _witWeaverSettings.SupportedLanguages != null &&
                _witWeaverSettings.SupportedLanguages.Count > 0)
            {
                return _witWeaverSettings.SupportedLanguages;
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
                if (_witWeaverSettings != null)
                {
                    _witWeaverSettings.CurrentLanguage = match;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(_witWeaverSettings);
#endif
                }

                bool verboseLogs = _witWeaverSettings?.VerboseLogs ?? false;
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