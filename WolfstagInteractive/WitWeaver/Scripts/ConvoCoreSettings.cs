using UnityEngine;
using System.Collections.Generic;

namespace WolfstagInteractive.ConvoCore
{
    public enum TextSourceKind
    {
        AssignedTextAsset,
        Persistent,
        Addressables,
        Resources
    }

    /// <summary>
    /// Controls how ConvoCore handles formula cells encountered during Excel spreadsheet import.
    /// </summary>
    public enum ExcelFormulaCellBehavior
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
    /// Global runtime settings ScriptableObject for ConvoCore. Defines the list of supported
    /// language codes, the active language, and the YAML text source load order.
    /// Create one per project via Assets > Create > ConvoCore > Settings.
    /// </summary>
    public sealed class ConvoCoreSettings : ScriptableObject
    {
        [Header("Order the sources to try (first hit wins)")]
        public TextSourceKind[] SourceOrder = new[]
        {
            TextSourceKind.AssignedTextAsset,
            TextSourceKind.Persistent,
            TextSourceKind.Addressables,
            TextSourceKind.Resources
        };

        public string resourcesRoot = "ConvoCore/Dialogue"; // only used if FilePath given
        [Header("Language Settings")]
        [Tooltip("List of supported language codes (e.g., 'en', 'fr', 'es')")]
        public List<string> SupportedLanguages = new List<string> { "EN" };
        [Tooltip("Currently active language code")]
        public string CurrentLanguage = "EN";

        [Tooltip("Localized label for the Go Back option appended to player choices when a line " +
                 "has Allow Go Back enabled. One entry per supported language.")]
        public List<ConvoCoreConversationData.LocalizedDialogue> GoBackLabel =
            new List<ConvoCoreConversationData.LocalizedDialogue>
            {
                new ConvoCoreConversationData.LocalizedDialogue { Language = "EN", Text = "← Go Back" }
            };
        public bool AddressablesEnabled = false; // flip on when project uses it
        public string AddressablesKeyTemplate = "{filePath}.yml"; // maps FilePath -> key
        public bool VerboseLogs = false;

        [Header("Save System")]
        [Tooltip("Prefix for all save system keys. Must not be empty.")]
        public string SaveKeyPrefix = "convocore.";
        [Tooltip("Enable the save system for persisting game state.")]
        public bool EnableSaveSystem = true;
        [Tooltip("Enable the variable store for tracking runtime variables.")]
        public bool EnableVariableStore = true;
        [Tooltip("Enable the language/localization system.")]
        public bool EnableLanguageSystem = true;

        private static ConvoCoreSettings _instance;

        public static ConvoCoreSettings Instance
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
        private static ConvoCoreSettings LoadInstance()
        {
            // 1. Try already loaded asset
            if (_instance != null)
                return _instance;

            // 2. Try from Resources folder (recommended for builds)
            var loaded = Resources.Load<ConvoCoreSettings>("ConvoCoreSettings");
            if (loaded != null)
            {
                _instance = loaded;
                return _instance;
            }

#if UNITY_EDITOR
            // 3. Try find it anywhere in the project (Editor only)
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ConvoCoreSettings");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<ConvoCoreSettings>(path);
                if (loaded != null)
                {
                    _instance = loaded;
                    return _instance;
                }
            }

            // 4. Create one automatically if none exists
            _instance = CreateInstance<ConvoCoreSettings>();
            string assetPath = "Assets/Resources/ConvoCoreSettings.asset";
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
            UnityEditor.AssetDatabase.CreateAsset(_instance, assetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            Debug.LogWarning($"Created default ConvoCoreSettings at {assetPath}");
            return _instance;
#else
        Debug.LogError("ConvoCoreSettings not found in Resources! Please create one in the Editor.");
        return ScriptableObject.CreateInstance<ConvoCoreSettings>();
#endif
        }
        /// <summary>
        /// Validates that CurrentLanguage is in the SupportedLanguages list
        /// </summary>
        private void OnValidate()
        {
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
                Debug.LogWarning("[ConvoCoreSettings] SaveKeyPrefix is empty. Defaulting to 'convocore.'.");
                SaveKeyPrefix = "convocore.";
            }
            else
            {
                for (int i = 0; i < SaveKeyPrefix.Length; i++)
                {
                    char c = SaveKeyPrefix[i];
                    if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')
                    {
                        Debug.LogWarning($"[ConvoCoreSettings] SaveKeyPrefix contains invalid character '{c}'. Only letters, digits, '.', '_', and '-' are allowed.");
                        break;
                    }
                }
            }

            CleanRendererProfiles();

            if (string.IsNullOrEmpty(ExcelCharacterIDHeader))
                ExcelCharacterIDHeader = "CharacterID";

            if (string.IsNullOrEmpty(ExcelLineIDHeader))
                ExcelLineIDHeader = "LineID";

            if (ExcelHeaderRowIndex < 0)
                ExcelHeaderRowIndex = 0;
        }
        // ------------------------------
        // Spreadsheet Import
        // ------------------------------

        [Tooltip("The column header used to identify the character ID column in an Excel spreadsheet. Case-insensitive.")]
        public string ExcelCharacterIDHeader = "CharacterID";

        [Tooltip("The column header used to identify the line ID column. This column is auto-populated by ConvoCore on import. Case-insensitive.")]
        public string ExcelLineIDHeader = "LineID";

        [Tooltip("Sheet tabs whose names begin with this prefix are skipped during import. Use this for note sheets or scratch tabs.")]
        public string ExcelSkipSheetPrefix = "_";

        [Tooltip("Zero-based row index of the header row. Default is 0 (first row). Increase this if your spreadsheet has title rows above the column headers.")]
        public int ExcelHeaderRowIndex = 0;

        [Tooltip("If true, rows where all cells are empty or whitespace are silently skipped during import.")]
        public bool ExcelSkipEmptyRows = true;

        [Tooltip("If true, column headers that are not the CharacterID column, the LineID column, or a recognized language code will produce a warning in the console.")]
        public bool ExcelWarnOnUnrecognizedColumns = false;

        [Tooltip("Controls how cells containing Excel formulas are handled during import.")]
        public ExcelFormulaCellBehavior ExcelFormulaCellBehavior = ExcelFormulaCellBehavior.UseCachedValue;

        // ------------------------------
        // Dialogue History Renderers
        // ------------------------------
        [Tooltip("List of available renderer profiles for dialogue history UI.")]
        [SerializeField] private List<ConvoCoreHistoryRendererProfile> historyRendererProfiles = new();

        public IReadOnlyList<ConvoCoreHistoryRendererProfile> HistoryRendererProfiles => historyRendererProfiles;

        /// <summary>
        /// Returns a renderer profile by its display name.
        /// </summary>
        public ConvoCoreHistoryRendererProfile GetRendererProfile(string rendererName)
        {
            foreach (var p in historyRendererProfiles)
                if (p != null && p.RendererName == rendererName)
                    return p;
            return null;
        }

        /// <summary>
        /// Adds a new profile if it doesn't already exist in the list.
        /// </summary>
        public void AddRendererProfile(ConvoCoreHistoryRendererProfile profile)
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