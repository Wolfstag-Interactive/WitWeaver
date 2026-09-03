// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;
using System.Collections.Generic;

namespace WolfstagInteractive.WitWeaver
{
    public enum TextSourceKind
    {
        AssignedTextAsset,
        Persistent
    }

    /// <summary>
    /// Controls how WitWeaver handles formula cells encountered during spreadsheet import.
    /// </summary>
    public enum SpreadsheetFormulaCellBehavior
    {
        /// <summary>
        /// Use the cached result value stored in the cell. This is the default and works
        /// for most cases where the file was saved with calculations up to date.
        /// </summary>
        UseCachedValue,

        /// <summary>
        /// Treat any formula cell as a parse error and abort import of that sheet.
        /// </summary>
        TreatAsError,

        /// <summary>
        /// Skip rows that contain any formula cell silently without aborting the import.
        /// </summary>
        SkipRow
    }

    /// <summary>
    /// Global runtime settings ScriptableObject for WitWeaver. Defines the list of supported
    /// language codes, the active language, and the YAML text source load order.
    /// Create one per project via Assets > Create > WitWeaver > Settings.
    /// </summary>
    public sealed class WitWeaverSettings : ScriptableObject
    {
        [Header("Order the sources to try (first hit wins)")]
        public TextSourceKind[] SourceOrder = new[]
        {
            TextSourceKind.AssignedTextAsset,
            TextSourceKind.Persistent
        };

        [Tooltip("List of supported language codes (e.g., 'en', 'fr', 'es')")]
        public List<string> SupportedLanguages = new List<string> { "EN" };
        [Tooltip("Currently active language code")]
        public string CurrentLanguage = "EN";

        [Tooltip("Localized label for the Go Back option appended to player choices when a line " +
                 "has Allow Go Back enabled. One entry per supported language.")]
        public List<WitWeaverConversationData.LocalizedDialogue> GoBackLabel =
            new List<WitWeaverConversationData.LocalizedDialogue>
            {
                new WitWeaverConversationData.LocalizedDialogue { Language = "EN", Text = "← Go Back" }
            };
        public bool VerboseLogs = false;

        [Tooltip("Prefix for all save system keys. Must not be empty.")]
        public string SaveKeyPrefix = "witweaver.";
        [Tooltip("Enable the save system for persisting game state.")]
        public bool EnableSaveSystem = true;
        [Tooltip("Enable the variable store for tracking runtime variables.")]
        public bool EnableVariableStore = true;
        [Tooltip("Enable the language/localization system.")]
        public bool EnableLanguageSystem = true;

        private static WitWeaverSettings _instance;

        public static WitWeaverSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = LoadInstance();
                }
                return _instance;
            }
        }
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorInitialize()
        {
            _instance = LoadInstance();
        }
#endif
        private static WitWeaverSettings LoadInstance()
        {
            // 1. Try already loaded asset
            if (_instance != null)
                return _instance;

            // 2. Try from Resources folder (recommended for builds)
            var loaded = Resources.Load<WitWeaverSettings>("WitWeaverSettings");
            if (loaded != null)
            {
                _instance = loaded;
                return _instance;
            }

#if UNITY_EDITOR
            // 3. Try find it anywhere in the project (Editor only)
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:WitWeaverSettings");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<WitWeaverSettings>(path);
                if (loaded != null)
                {
                    _instance = loaded;
                    return _instance;
                }
            }

            // 4. Create one automatically if none exists
            _instance = CreateInstance<WitWeaverSettings>();
            string assetPath = "Assets/Resources/WitWeaverSettings.asset";
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
            UnityEditor.AssetDatabase.CreateAsset(_instance, assetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            Debug.LogWarning($"Created default WitWeaverSettings at {assetPath}");
            return _instance;
#else
        Debug.LogError("WitWeaverSettings not found in Resources! Please create one in the Editor.");
        _instance = ScriptableObject.CreateInstance<WitWeaverSettings>();
        return _instance;
#endif
        }
        /// <summary>
        /// Validates that CurrentLanguage is in the SupportedLanguages list
        /// </summary>
        private void OnValidate()
        {
            SanitizeSourceOrder();

            // Ensure we have at least one language
            if (SupportedLanguages == null || SupportedLanguages.Count == 0)
            {
                SupportedLanguages = new List<string> { "EN" };
            }

            // If current language is not in supported languages, reset to first
            if (string.IsNullOrEmpty(CurrentLanguage) || 
                !SupportedLanguages.Exists(lang => string.Equals(lang, CurrentLanguage, System.StringComparison.OrdinalIgnoreCase)))
            {
                CurrentLanguage = SupportedLanguages[0];
            }
            // Validate SaveKeyPrefix
            if (string.IsNullOrEmpty(SaveKeyPrefix))
            {
                Debug.LogWarning("[WitWeaverSettings] SaveKeyPrefix is empty. Defaulting to 'witweaver.'.");
                SaveKeyPrefix = "witweaver.";
            }
            else
            {
                for (int i = 0; i < SaveKeyPrefix.Length; i++)
                {
                    char c = SaveKeyPrefix[i];
                    if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')
                    {
                        Debug.LogWarning($"[WitWeaverSettings] SaveKeyPrefix contains invalid character '{c}'. Only letters, digits, '.', '_', and '-' are allowed.");
                        break;
                    }
                }
            }

            CleanRendererProfiles();

            if (string.IsNullOrEmpty(SpreadsheetCharacterIDHeader))
                SpreadsheetCharacterIDHeader = "CharacterID";

            if (string.IsNullOrEmpty(SpreadsheetLineIDHeader))
                SpreadsheetLineIDHeader = "LineID";

            if (SpreadsheetHeaderRowIndex < 0)
                SpreadsheetHeaderRowIndex = 0;
        }

        /// <summary>
        /// Removes duplicate and out-of-range SourceOrder entries. Assets serialized before the
        /// Addressables/Resources source kinds were removed may still contain their old raw values;
        /// those deserialize as undefined enum values and are pruned here.
        /// </summary>
        private void SanitizeSourceOrder()
        {
            if (SourceOrder == null || SourceOrder.Length == 0)
            {
                SourceOrder = new[] { TextSourceKind.AssignedTextAsset, TextSourceKind.Persistent };
                return;
            }

            var cleaned = new List<TextSourceKind>(SourceOrder.Length);
            foreach (var src in SourceOrder)
            {
                if ((src == TextSourceKind.AssignedTextAsset || src == TextSourceKind.Persistent) &&
                    !cleaned.Contains(src))
                {
                    cleaned.Add(src);
                }
            }

            if (cleaned.Count == 0)
                cleaned.Add(TextSourceKind.AssignedTextAsset);

            if (cleaned.Count != SourceOrder.Length)
                SourceOrder = cleaned.ToArray();
        }
        // ------------------------------
        // Spreadsheet Import
        // ------------------------------

        [Tooltip("The column header used to identify the character ID column in a spreadsheet. Case-insensitive.")]
        public string SpreadsheetCharacterIDHeader = "CharacterID";

        [Tooltip("The column header used to identify the line ID column. This column is auto-populated by WitWeaver on import. Case-insensitive.")]
        public string SpreadsheetLineIDHeader = "LineID";

        [Tooltip("Sheet tabs whose names begin with this prefix are skipped during import. Use this for note sheets or scratch tabs.")]
        public string SpreadsheetSkipSheetPrefix = "_";

        [Tooltip("Zero-based row index of the header row. Default is 0 (first row). Increase this if your spreadsheet has title rows above the column headers.")]
        public int SpreadsheetHeaderRowIndex = 0;

        [Tooltip("If true, rows where all cells are empty or whitespace are silently skipped during import.")]
        public bool SpreadsheetSkipEmptyRows = true;

        [Tooltip("If true, column headers that are not the CharacterID column, the LineID column, or a recognized language code will produce a warning in the console.")]
        public bool SpreadsheetWarnOnUnrecognizedColumns = false;

        [Tooltip("Controls how cells containing spreadsheet formulas are handled during import.")]
        public SpreadsheetFormulaCellBehavior SpreadsheetFormulaCellBehavior = SpreadsheetFormulaCellBehavior.UseCachedValue;

        // ------------------------------
        // Dialogue History Renderers
        // ------------------------------
        [Tooltip("List of available renderer profiles for dialogue history UI.")]
        [SerializeField] private List<WitWeaverHistoryRendererProfile> historyRendererProfiles = new();

        public IReadOnlyList<WitWeaverHistoryRendererProfile> HistoryRendererProfiles => historyRendererProfiles;

        /// <summary>
        /// Returns a renderer profile by its display name.
        /// </summary>
        public WitWeaverHistoryRendererProfile GetRendererProfile(string rendererName)
        {
            foreach (var p in historyRendererProfiles)
                if (p != null && p.RendererName == rendererName)
                    return p;
            return null;
        }

        /// <summary>
        /// Adds a new profile if it doesn't already exist in the list.
        /// </summary>
        public void AddRendererProfile(WitWeaverHistoryRendererProfile profile)
        {
            if (profile != null && !historyRendererProfiles.Contains(profile))
                historyRendererProfiles.Add(profile);
        }

        /// <summary>
        /// Removes null or missing profile references.
        /// </summary>
        public void CleanRendererProfiles()
        {
            historyRendererProfiles.RemoveAll(p => p == null);
        }
    }
}