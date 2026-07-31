using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.IO;
using Object = UnityEngine.Object;
using System.Linq;
using System.Collections.Generic;
namespace WolfstagInteractive.ConvoCore.Editor
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1CharacterConversationObjectEditor.html")]
[CustomEditor(typeof(ConvoCoreConversationData))]
    public class CharacterConversationObjectEditor : UnityEditor.Editor
    {
        private SerializedProperty _conversationKey;
        private SerializedProperty _filePath;

        private SerializedProperty _conversationYaml;
        private SerializedProperty _sourceYaml;
        private SerializedProperty _sourceYamlAssetPath;
        private SerializedProperty _sourceExcelAsset;
        private SerializedProperty _sourceExcelAssetPath;
        private string _lastExcelImportMessage;
        private bool _lastExcelImportSuccess;
        private bool _excelFoldout;

        private string[] _cachedSupportedLanguages;
        private string[] _cachedDisplayLanguages;
        private List<string> _cachedYamlLocales;
        private double _lastYamlCheckTime;
        private const double YAML_CHECK_INTERVAL = 1.0;

        private ReorderableList _participantConfigList;
        private void OnEnable()
        {
            _excelFoldout = EditorPrefs.GetBool("ConvoCore.ExcelSourceFoldout." + target.GetEntityId(), false);
            CacheLanguageSettings();
        }
        private void CacheLanguageSettings()
        {
            var loader = new ConvoCoreLanguageSettingsLoader();
            var settings = loader.LoadLanguageSettings();

            var supported = settings?.SupportedLanguages;
            if (supported == null || supported.Count == 0)
                supported = new List<string> { "en" };

            // Clean & deduplicate without LINQ
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cleaned = new List<string>();
            foreach (var s in supported)
            {
                if (string.IsNullOrWhiteSpace(s)) continue;
                var trimmed = s.Trim();
                if (seen.Add(trimmed))
                    cleaned.Add(trimmed);
            }
            _cachedSupportedLanguages = cleaned.ToArray();
            _cachedDisplayLanguages = new string[_cachedSupportedLanguages.Length];
            for (int i = 0; i < _cachedSupportedLanguages.Length; i++)
                _cachedDisplayLanguages[i] = _cachedSupportedLanguages[i].ToUpperInvariant();
        }
        public override void OnInspectorGUI()
        {
            // Update the serialized object
            serializedObject.Update();

            // Initialize required serialized properties
            _filePath                 = serializedObject.FindProperty("FilePath");
            _conversationKey          = serializedObject.FindProperty("ConversationKey");
            _conversationYaml         = serializedObject.FindProperty("ConversationYaml");
            _sourceYaml               = serializedObject.FindProperty("SourceYaml");
            _sourceYamlAssetPath      = serializedObject.FindProperty("SourceYamlAssetPath");
            _sourceExcelAsset         = serializedObject.FindProperty("SourceExcelAsset");
            _sourceExcelAssetPath     = serializedObject.FindProperty("SourceExcelAssetPath");

            // Auto-sync ParticipantConfigurationDefaults before drawing.
            SyncParticipantConfigurationDefaults();

            // Track if any changes are made for validation
            EditorGUI.BeginChangeCheck();

            // Draw properties using custom iteration to handle overrides
            SerializedProperty property = serializedObject.GetIterator();
            property.NextVisible(true);

            do
            {
                // Skip custom-handled and internal properties
                if (property.name == "m_Script" ||
                    property.name == "FilePath" ||
                    property.name == "ConversationKey" ||
                    property.name == "ConversationYaml" ||
                    property.name == "SourceYaml" ||
                    property.name == "SourceYamlAssetPath" ||
                    property.name == "ParticipantConfigurationDefaults")
                    continue;

                if (property.name == "ConversationParticipantProfiles")
                {
                    // Draw participants normally, then inject configuration section directly after.
                    EditorGUILayout.PropertyField(property, true);
                    DrawParticipantConfigurationSection();
                    continue;
                }

                if (property.name == "DialogueLines" || property.name == "dialogueLines")
                {
                    // Custom paged drawing for dialogue lines
                    PagedListUtility.DrawPagedList(property, 20);
                }
                else
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }
            while (property.NextVisible(false));

            // If any changes were made, validate the data
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                var conversationData = (ConvoCoreConversationData)target;
                conversationData.ValidateAndFixDialogueLines();
                EditorUtility.SetDirty(target);
            }
            DrawLanguagePreviewSection();

            // Add validation tools section
            DrawValidationToolsSection();

            // YAML Linking (hidden intermediary workflow)
            DrawYamlLinkingSection();
            // Excel Source (alternative to YAML for spreadsheet-driven authoring)
            DrawExcelSourceSection();
            // Draw 'FilePath' field with the browse button
            DrawFilePathField();

            DrawAddDeleteSection();
            // Apply any modified properties
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAddDeleteSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Line Data Controls", EditorStyles.boldLabel);
            DrawConversationKeyField();
            Separator();
            DrawDeletePopupButton();
            EditorGUILayout.EndVertical();
        }
        private static void Separator(float thickness = 2f, float margin = 6f, float alpha = 0.2f)
        {
            GUILayout.Space(margin);
            var rect = EditorGUILayout.GetControlRect(false, thickness);
            rect.height = thickness;
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, alpha));
            GUILayout.Space(margin);
        }
        private void DrawDeletePopupButton()
        {
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Delete ALL Dialogue Lines…", GUILayout.Height(28)))
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "Delete All Dialogue Lines",
                    "This will permanently remove ALL dialogue lines from this conversation.\n\nAre you sure?",
                    "Delete ALL Lines",
                    "Cancel"
                );
                if (confirm)
                {
                    var conversationData = (ConvoCoreConversationData)target;
                    try
                    {
                        // Clear commonly used field names for lines
                        var so = serializedObject;
                        var linesProp = so.FindProperty("DialogueLines") ?? so.FindProperty("dialogueLines");
                        if (linesProp != null && linesProp.isArray)
                        {
                            linesProp.ClearArray();
                            so.ApplyModifiedProperties();
                        }

                        // Best-effort cleanup/validation afterward
                        conversationData.ValidateAndFixDialogueLines();
                        EditorUtility.SetDirty(conversationData);
                        AssetDatabase.SaveAssets();
                        Debug.Log($"All dialogue lines deleted for '{conversationData.name}'.");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to delete dialogue lines: {ex.Message}");
                    }
                }
            }
            GUI.backgroundColor = prevColor;
        }

        // ------------------------------------------------------------------
        // Participant Configuration Defaults
        // ------------------------------------------------------------------

        /// <summary>
        /// Returns the CharacterIDs of all participants that have at least one representation
        /// returning non-null from <see cref="CharacterRepresentationBase.GetConfigurationEntryNames"/>,
        /// in profile-list order.
        /// </summary>
        private List<string> GetConfigurableParticipantIds()
        {
            var convoData = (ConvoCoreConversationData)target;
            var ids = new List<string>();
            foreach (var profile in convoData.ConversationParticipantProfiles)
            {
                if (profile == null || string.IsNullOrEmpty(profile.CharacterID)) continue;
                bool hasEntries = profile.Representations.Any(p =>
                    p?.CharacterRepresentationType?.GetConfigurationEntryNames() != null);
                if (hasEntries && !ids.Contains(profile.CharacterID))
                    ids.Add(profile.CharacterID);
            }
            return ids;
        }

        /// <summary>
        /// Ensures <c>ParticipantConfigurationDefaults</c> has exactly one slot per configurable
        /// participant, in participant-list order, with existing entry-name values preserved.
        /// Uses serialized-property operations so changes integrate with Unity's undo system.
        /// </summary>
        private void SyncParticipantConfigurationDefaults()
        {
            var targetIds = GetConfigurableParticipantIds();
            var arrayProp = serializedObject.FindProperty("ParticipantConfigurationDefaults");

            // Build current state.
            var currentIds = new List<string>(arrayProp.arraySize);
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var idProp = arrayProp.GetArrayElementAtIndex(i).FindPropertyRelative("CharacterID");
                currentIds.Add(idProp?.stringValue ?? "");
            }

            // Early-out when nothing changed.
            if (currentIds.Count == targetIds.Count && currentIds.SequenceEqual(targetIds))
                return;

            // Cache existing entry-name values so they survive a rewrite.
            var cache = new Dictionary<string, string>();
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var elem   = arrayProp.GetArrayElementAtIndex(i);
                var idStr  = elem.FindPropertyRelative("CharacterID")?.stringValue ?? "";
                var entryStr = elem.FindPropertyRelative("DefaultConfigurationEntryName")?.stringValue ?? "";
                if (!string.IsNullOrEmpty(idStr))
                    cache[idStr] = entryStr;
            }

            // Rewrite the array in one pass.
            arrayProp.arraySize = targetIds.Count;
            for (int i = 0; i < targetIds.Count; i++)
            {
                var id   = targetIds[i];
                var elem = arrayProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("CharacterID").stringValue = id;
                elem.FindPropertyRelative("DefaultConfigurationEntryName").stringValue =
                    cache.TryGetValue(id, out var saved) ? saved : "";
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            // Rebuild list so element heights are recalculated.
            _participantConfigList = null;
        }

        private void EnsureParticipantConfigList()
        {
            var prop = serializedObject.FindProperty("ParticipantConfigurationDefaults");
            _participantConfigList = new ReorderableList(serializedObject, prop,
                draggable: false, displayHeader: true,
                displayAddButton: false, displayRemoveButton: false);

            _participantConfigList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Participant Configuration Defaults");

            _participantConfigList.elementHeightCallback = index =>
                EditorGUI.GetPropertyHeight(prop.GetArrayElementAtIndex(index), true) + 4f;

            _participantConfigList.drawElementCallback = (rect, index, active, focused) =>
            {
                rect.y      += 2f;
                rect.height -= 4f;
                EditorGUI.PropertyField(rect, prop.GetArrayElementAtIndex(index), GUIContent.none, true);
            };
        }

        private void DrawParticipantConfigurationSection()
        {
            var ids = GetConfigurableParticipantIds();
            if (ids.Count == 0) return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Participant Configuration Defaults", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "One entry per participant whose representation supports named configuration entries. " +
                "CharacterID is managed automatically. Use the dropdown to set a conversation-level default entry.",
                MessageType.None);

            if (_participantConfigList == null) EnsureParticipantConfigList();
            _participantConfigList.DoLayoutList();
        }

        // ------------------------------------------------------------------

        private void DrawLanguagePreviewSection()
{
    EditorGUILayout.Space();
    EditorGUILayout.BeginVertical("box");
    EditorGUILayout.LabelField("Language (Preview)", EditorStyles.boldLabel);

    // 1) Source of truth – from Settings
    var supported = LocaleSettingsCache.Get();
    if (supported.Length == 0)
    {
        EditorGUILayout.HelpBox("No supported locales configured in ConvoCore Settings.", MessageType.Warning);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        return;
    }

    // Populate display names once (or reuse your _cachedDisplayLanguages)
    if (_cachedSupportedLanguages == null || _cachedSupportedLanguages.Length != supported.Length)
    {
        _cachedSupportedLanguages = supported;
        _cachedDisplayLanguages = supported; // or map to nice names
    }

    // 2) LanguageManager current
    var lm = ConvoCoreLanguageManager.Instance;
    var current = lm.CurrentLanguage ?? "en";

    int idx = 0;
    for (int i = 0; i < _cachedSupportedLanguages.Length; i++)
        if (string.Equals(_cachedSupportedLanguages[i], current, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }

    var newIdx = EditorGUILayout.Popup("Preview Language", idx, _cachedDisplayLanguages);
    if (newIdx != idx)
    {
        lm.SetLanguage(_cachedSupportedLanguages[newIdx]);
        Repaint();
    }

    // 3) YAML validation (lightweight due to memoization)
    var data = (ConvoCoreConversationData)target;

    // Only recompute when repainting layout (optional micro-opt)
    if (Event.current.type == EventType.Layout ||
        _cachedYamlLocales == null ||
        EditorApplication.timeSinceStartup - _lastYamlCheckTime > YAML_CHECK_INTERVAL)
    {
        _cachedYamlLocales = LocaleCache.GetLocales(data); // cached per TextAsset content
        _lastYamlCheckTime = EditorApplication.timeSinceStartup;
    }

    if (_cachedYamlLocales != null && _cachedYamlLocales.Count > 0)
    {
        // Diff: what's missing from YAML vs settings?
        var missing = _cachedSupportedLanguages
            .Where(s => !_cachedYamlLocales.Contains(s, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        var extras = _cachedYamlLocales
            .Where(s => !_cachedSupportedLanguages.Contains(s, StringComparer.OrdinalIgnoreCase))
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.TextField("Locales in YAML", string.Join(", ", _cachedYamlLocales));

        if (missing.Length > 0)
            EditorGUILayout.HelpBox($"Missing in YAML: {string.Join(", ", missing)}", MessageType.Info);

        if (extras.Length > 0)
            EditorGUILayout.HelpBox($"YAML has extra locales not in Settings: {string.Join(", ", extras)}", MessageType.None);

        // Helpful hint if currently selected not present
        if (!LocaleExists(_cachedYamlLocales, _cachedSupportedLanguages[newIdx]))
        {
            EditorGUILayout.HelpBox(
                "Selected preview language not found in YAML; runtime may fall back (e.g., to 'en').",
                MessageType.Info);
        }
    }

    EditorGUILayout.EndVertical();
    EditorGUILayout.Space();
}

        private static bool LocaleExists(List<string> list, string lang)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], lang, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
        // Assumes you have a settings singleton with string[] Locales (or SupportedLocales)
        static class LocaleSettingsCache
        {
            private static string[] _cached;
            private static uint _hash;

            public static string[] Get()
            {
                var settings = ConvoCoreSettings.Instance; 
                if (settings == null || settings.SupportedLanguages == null) return Array.Empty<string>();

                // Hash the array contents to detect changes
                uint h = 2166136261;
                var arr = settings.SupportedLanguages;
                for (int i = 0; i < arr.Count; i++)
                {
                    var s = arr[i] ?? "";
                    unchecked
                    {
                        for (int c = 0; c < s.Length; c++)
                        {
                            h ^= s[c];
                            h *= 16777619;
                        }
                        h ^= (uint)';'; h *= 16777619;
                    }
                }

                if (_cached != null && _hash == h) return _cached;

                // Normalize once (trim + distinct + sort)
                var newList = new List<string>(arr.Count);
                for (int i = 0; i < arr.Count; i++)
                {
                    var s = arr[i];
                    if (!string.IsNullOrWhiteSpace(s)) newList.Add(s.Trim());
                }

                _cached = newList
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                _hash = h;
                return _cached;
            }

            // Call if you edit settings in-place and want an immediate refresh
            public static void Invalidate() { _cached = null; _hash = 0; }
        }

        // Parse the embedded YAML to list locale keys present (case-insensitive, de-duplicated)
        private static List<string> TryGetLocalesFromEmbedded(ConvoCoreConversationData data)
        {
            try
            {
                if (data == null || data.ConversationYaml == null) return null;
                var yamlText = data.ConversationYaml.text;
                if (string.IsNullOrEmpty(yamlText)) return null;

                // Uses the runtime parser (already normalizes keys to case-insensitive dictionary).
                // TryParse is used here so a broken YAML file surfaces a warning rather than
                // silently returning null and leaving the inspector with no locale options.
                if (!ConvoCoreYamlParser.TryParse(yamlText, out var dict, out string parseErr))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[ConvoCore] Could not read locales from embedded YAML:\n{parseErr}", data);
                    return null;
                }
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kv in dict)
                {
                    var list = kv.Value;
                    if (list == null) continue;

                    foreach (var cfg in list)
                    {
                        if (cfg?.LocalizedDialogue == null) continue;
                        foreach (var lang in cfg.LocalizedDialogue.Keys)
                        {
                            if (!string.IsNullOrWhiteSpace(lang))
                                set.Add(lang.Trim());
                        }
                    }
                }

                return set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex)
            {
                // Keep the inspector resilient against unexpected runtime errors.
                UnityEngine.Debug.LogWarning(
                    $"[ConvoCore] Unexpected error reading locale keys from embedded YAML: {ex.Message}", data);
                return null;
            }
        }

        private void DrawExcelSourceSection()
        {
            if (_sourceExcelAsset == null || _sourceExcelAssetPath == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");

            var foldoutKey = "ConvoCore.ExcelSourceFoldout." + target.GetEntityId();
            _excelFoldout = EditorGUILayout.Foldout(_excelFoldout, "Excel Source", true, EditorStyles.foldoutHeader);
            EditorPrefs.SetBool(foldoutKey, _excelFoldout);

            if (_excelFoldout)
            {
                EditorGUILayout.Space(2);

                // Show link prompt when no asset assigned
                if (_sourceExcelAsset.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        "Link an .xlsx file to enable spreadsheet-driven dialogue authoring. " +
                        "Each sheet tab name must match a ConversationKey. " +
                        "Configure expected column headers in ConvoCoreSettings under the Spreadsheet tab.",
                        MessageType.Info);
                }

                // Object field for SourceExcelAsset
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_sourceExcelAsset, new GUIContent("Source .xlsx (Editor-only)"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    var newObj = _sourceExcelAsset.objectReferenceValue;
                    if (newObj != null)
                    {
                        var newPath = AssetDatabase.GetAssetPath(newObj);
                        _sourceExcelAssetPath.stringValue = newPath;
                        serializedObject.ApplyModifiedProperties();
                    }
                    else
                    {
                        _sourceExcelAssetPath.stringValue = string.Empty;
                        serializedObject.ApplyModifiedProperties();
                    }
                }

                // Validate extension
                if (_sourceExcelAsset.objectReferenceValue != null)
                {
                    var assignedPath = _sourceExcelAssetPath.stringValue;
                    if (!assignedPath.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase))
                    {
                        EditorGUILayout.HelpBox(
                            "The assigned file does not appear to be an .xlsx file. Only Excel workbooks are supported.",
                            MessageType.Warning);
                    }
                    else
                    {
                        // Show current linked path
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.TextField("Linked Path", assignedPath);

                        // Import button
                        if (GUILayout.Button("Import from Excel", GUILayout.Height(24)))
                        {
                            var data = (ConvoCoreConversationData)target;
                            bool ok = ConvoCoreExcelUtilities.RunFullPipeline(data, assignedPath, out var msg);
                            _lastExcelImportMessage = msg;
                            _lastExcelImportSuccess = ok;
                        }

                        // Result message
                        if (!string.IsNullOrEmpty(_lastExcelImportMessage))
                        {
                            EditorGUILayout.HelpBox(
                                _lastExcelImportMessage,
                                _lastExcelImportSuccess ? MessageType.Info : MessageType.Error);
                        }
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawYamlLinkingSection()
        {
            // Safety: if these props don't exist (older data class), skip drawing the section
            if (_sourceYaml == null || _conversationYaml == null || _sourceYamlAssetPath == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("YAML Linking (Recommended)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Link a plain .yaml file you edit normally. ConvoCore will embed its text as a sub-asset used at runtime. " +
                "This avoids StreamingAssets/Resources requirements and won’t conflict with other YAML tools.",
                MessageType.Info);

            // Source .yaml (DefaultAsset or TextAsset)
            EditorGUILayout.PropertyField(_sourceYaml, new GUIContent("Source .yaml (Editor-only)"));

            // Show current embedded TextAsset (read-only)
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_conversationYaml, new GUIContent("Embedded YAML (used at runtime)"));
            }

            // Show linked path (if any)
            if (!string.IsNullOrEmpty(_sourceYamlAssetPath.stringValue))
            {
                EditorGUILayout.LabelField("Linked Path", _sourceYamlAssetPath.stringValue, EditorStyles.miniLabel);
            }
            // Calculate button width to make all buttons equal
            float buttonWidth = (EditorGUIUtility.currentViewWidth - 20f) / 2f;

            // Buttons row
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(_conversationYaml.objectReferenceValue == null ? "Link & Embed" : "Sync From Source", GUILayout.Height(22),
                    GUILayout.Width(buttonWidth)))
            {
                var data = (ConvoCoreConversationData)target;
                if (_sourceYaml.objectReferenceValue == null)
                {
                    Debug.LogError("Please assign a Source .yaml asset to link.");
                }
                else
                {
                    var srcObj  = _sourceYaml.objectReferenceValue;
                    var srcPath = AssetDatabase.GetAssetPath(srcObj);

                    // Only allow .yml/.yaml or TextAsset
                    var isYamlExt  = srcPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                                     srcPath.EndsWith(".yml",  StringComparison.OrdinalIgnoreCase);
                    var isTextAsset = srcObj is TextAsset;

                    if (!isYamlExt && !isTextAsset)
                    {
                        Debug.LogError("Source must be a .yaml/.yml file or a TextAsset.");
                    }
                    else
                    {
                        // Use the new method that returns the embedded TextAsset
                        var embeddedAsset = TryEmbedFromPath(data, srcPath);
                        if (embeddedAsset != null)
                        {
                            _sourceYamlAssetPath.stringValue = srcPath;
                            
                            // Assign the embedded asset directly to the SerializedProperty
                            _conversationYaml.objectReferenceValue = embeddedAsset;
                            
                            // Apply all changes
                            serializedObject.ApplyModifiedProperties();

                            Debug.Log($"ConvoCore: Embedded YAML text from '{srcPath}' into '{AssetDatabase.GetAssetPath(data)}'.");
                        }
                    }
                }
            }

            using (new EditorGUI.DisabledScope(_sourceYaml.objectReferenceValue == null))
            {
                if (GUILayout.Button("Ping Source", GUILayout.Height(22),GUILayout.ExpandWidth(true)))
                {
                    EditorGUIUtility.PingObject(_sourceYaml.objectReferenceValue);
                }
            }

            EditorGUILayout.EndHorizontal();

            // Second row of buttons with uniform width
            EditorGUILayout.BeginHorizontal();

            // Clear Link button - use ExpandWidth to match first row
            using (new EditorGUI.DisabledScope(_sourceYaml.objectReferenceValue == null && _conversationYaml.objectReferenceValue == null))
            {
                if (GUILayout.Button("Clear Link (keeps embedded)", GUILayout.Height(18), GUILayout.Width(buttonWidth)))
                {
                    _sourceYaml.objectReferenceValue = null;
                    _sourceYamlAssetPath.stringValue = string.Empty;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            // Clear Embedded button - use ExpandWidth to match first row
            using (new EditorGUI.DisabledScope(_conversationYaml.objectReferenceValue == null))
            {
                if (GUILayout.Button("Clear Embedded", GUILayout.Height(18), GUILayout.Width(buttonWidth)))
                {
                    if (EditorUtility.DisplayDialog("Clear Embedded YAML", 
                        "Are you sure you want to remove the embedded YAML? This will delete the embedded TextAsset permanently.", 
                        "Yes, Remove", "Cancel"))
                    {
                        var data = (ConvoCoreConversationData)target;
                        ClearEmbeddedYaml(data);
                        
                        // Update the SerializedProperty to reflect the change
                        _conversationYaml.objectReferenceValue = null;
                        serializedObject.ApplyModifiedProperties();
                        
                        Debug.Log("ConvoCore: Cleared embedded YAML.");
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }


        private static void ClearEmbeddedYaml(ConvoCoreConversationData data)
        {
            // Path of the Conversation asset
            var convPath = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(convPath))
            {
                Debug.LogError("ConvoCore: Could not resolve asset path for Conversation asset.");
                return;
            }

            // Remove the current embedded TextAsset from the field
            if (data.ConversationYaml != null)
            {
                Object.DestroyImmediate(data.ConversationYaml, true);
                data.ConversationYaml = null;
            }

            // Also clean up any stray "EmbeddedYaml" sub-assets
            var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(convPath);
            if (reps != null)
            {
                foreach (var rep in reps)
                {
                    if (rep is TextAsset repTA && repTA.name == "EmbeddedYaml")
                    {
                        Object.DestroyImmediate(repTA, true);
                    }
                }
            }

            // Mark dirty and save
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            
            // Force reimport to ensure changes are visible immediately
            AssetDatabase.ImportAsset(convPath, ImportAssetOptions.ForceUpdate);
        }

        private static TextAsset TryEmbedFromPath(ConvoCoreConversationData data, string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath)) return null;

            // Load text either from TextAsset or via File
            string text = null;
            var srcObj = AssetDatabase.LoadAssetAtPath<Object>(sourcePath);
            if (srcObj is TextAsset ta) text = ta.text;
            else                        text = File.ReadAllText(sourcePath);
            if (string.IsNullOrEmpty(text)) return null;

            // Path of the Conversation asset we will embed into
            var convPath = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(convPath))
            {
                Debug.LogError("ConvoCore: Could not resolve asset path for Conversation asset.");
                return null;
            }

            // --- Clean up any existing embedded assets first ---
            // Remove whatever the field currently points to
            if (data.ConversationYaml != null)
            {
                Object.DestroyImmediate(data.ConversationYaml, true);
                data.ConversationYaml = null;
            }

            // Remove any stray "EmbeddedYaml" sub-assets
            var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(convPath);
            if (reps != null)
            {
                foreach (var rep in reps)
                {
                    if (rep is TextAsset repTA && repTA.name == "EmbeddedYaml")
                    {
                        Object.DestroyImmediate(repTA, true);
                    }
                }
            }

            // Save to ensure cleanup is committed
            AssetDatabase.SaveAssets();

            // --- Create new embedded TextAsset ---
            var embedded = new TextAsset(text) { name = "EmbeddedYaml" };
            
            // Add as sub-asset
            AssetDatabase.AddObjectToAsset(embedded, data);

            // Optional: auto-fill FilePath for persistent/Addressables fallbacks if empty
            if (string.IsNullOrEmpty(data.FilePath))
            {
                var baseName = Path.GetFileNameWithoutExtension(sourcePath);
                data.FilePath = $"ConvoCore/Dialogue/{baseName}";
            }

            // Mark dirty and save
            EditorUtility.SetDirty(embedded);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            
            // Force reimport to ensure the sub-asset is properly recognized
            AssetDatabase.ImportAsset(convPath, ImportAssetOptions.ForceUpdate);
            
            // Return the embedded asset so we can assign it to the SerializedProperty
            return embedded;
        }
        /// Draws validation tools section with buttons for manual validation
        private void DrawValidationToolsSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Validation Tools", EditorStyles.boldLabel);
    
            EditorGUILayout.HelpBox(
                "Use these tools to validate and debug your dialogue data. " +
                "The validation will automatically fix missing primary character representations.",
                MessageType.Info
            );

            // Create a horizontal layout for the buttons
            EditorGUILayout.BeginHorizontal();

            // Validate and Fix button
            if (GUILayout.Button("Validate & Fix All Dialogue Lines", GUILayout.Height(25)))
            {
                var conversationData = (ConvoCoreConversationData)target;
                conversationData.ValidateAndFixDialogueLines();
                EditorUtility.SetDirty(target);
                Debug.Log($"Manual validation completed for {conversationData.name}");
            }

            // Debug profiles button
            if (GUILayout.Button("Debug Character Profiles", GUILayout.Height(25)))
            {
                var conversationData = (ConvoCoreConversationData)target;
                conversationData.DebugCharacterProfiles();
            }

            EditorGUILayout.EndHorizontal();

            // Second row of buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload YAML Now",GUILayout.Height(25)))
            {
                var data = (ConvoCoreConversationData)target;
                data.InitializeDialogueData();           // uses the embedded TextAsset first
                EditorUtility.SetDirty(target);
                Debug.Log($"Reloaded YAML for '{data.name}'.");
            }
            // Sync object references button
            if (GUILayout.Button("Sync Object References", GUILayout.Height(25)))
            {
                var conversationData = (ConvoCoreConversationData)target;
                conversationData.SyncAllRepresentationObjectReferences();
                EditorUtility.SetDirty(target);
                Debug.Log($"Object reference sync completed for {conversationData.name}");
            }

            // Force save button
            if (GUILayout.Button("Force Save Asset", GUILayout.Height(25)))
            {
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
                Debug.Log($"Forced save completed for {target.name}");
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        /// <summary>
        /// Draws the File Path field with browse functionality.
        /// </summary>
        private void DrawFilePathField()
        {
            EditorGUILayout.BeginVertical("box");
            // Section header
            EditorGUILayout.LabelField("YML Dialogue Data File Path", EditorStyles.boldLabel);

            // Description
            EditorGUILayout.HelpBox(
                "Specify the path to the YML file containing conversation data. " +
                "Ensure this file exists inside the /StreamingAssets/ folder.",
                MessageType.Info
            );

            // Draw the FilePath property field
            EditorGUILayout.PropertyField(_filePath, new GUIContent("File Path"));

            // Browse button to load file path
            if (GUILayout.Button("Browse YML File"))
            {
                // Open file panel for YML files
                string filePath = EditorUtility.OpenFilePanel("Select YML File", Application.streamingAssetsPath, "yml");
                if (!string.IsNullOrEmpty(filePath))
                {
                    // Ensure the file resides in the StreamingAssets folder
                    if (filePath.StartsWith(Application.streamingAssetsPath))
                    {
                        _filePath.stringValue = filePath.Substring(Application.streamingAssetsPath.Length + 1); // Relative path
                        serializedObject.ApplyModifiedProperties(); // Apply changes
                    }
                    else
                    {
                        Debug.LogError("Selected file must reside inside the StreamingAssets folder.");
                    }
                }
            }
            EditorGUILayout.EndVertical();

        }

        /// <summary>
        /// Draws the Conversation Key field with an import button.
        /// </summary>
        private void DrawConversationKeyField()
        {
            // Section header
            EditorGUILayout.LabelField("Conversation Key", EditorStyles.miniBoldLabel);

            // Description
            EditorGUILayout.HelpBox(
                "Specify the unique identifier for the conversation. " +
                "Ensure it matches an existing key in your YML data file.",
                MessageType.Info
            );

            // Draw the ConversationKey property field
            EditorGUILayout.PropertyField(_conversationKey, new GUIContent("Conversation Key"));

            // Button to import data using the ConversationKey
            if (GUILayout.Button("Import From YAML For Key"))
            {
                // Ensure the ConversationKey is not empty
                if (string.IsNullOrEmpty(_conversationKey.stringValue))
                {
                    Debug.LogError("Please provide a valid conversation key.");
                }
                else
                {
                    // Safely import the conversation using the provided key
                    ConvoCoreConversationData obj = (ConvoCoreConversationData)target;
                    obj.ConvoCoreYamlUtilities.ImportFromYamlForKey(_conversationKey.stringValue);
                    
                    // After import, validate the data
                    obj.ValidateAndFixDialogueLines();
                    EditorUtility.SetDirty(target);
                }
            }
        }
    }
    static class LocaleCache
{
    // One cache entry per TextAsset
    private sealed class Entry
    {
        public uint Hash;
        public List<string> Locales = new List<string>();
    }

    // EntityId -> Entry
    private static readonly Dictionary<EntityId, Entry> _cache = new Dictionary<EntityId, Entry>();

    // Fast non-alloc hash for strings (FNV-1a 32-bit)
    private static uint Fnva32(string s)
    {
        unchecked
        {
            const uint fnvOffset = 2166136261;
            const uint fnvPrime  = 16777619;
            uint hash = fnvOffset;
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= fnvPrime;
            }
            return hash;
        }
    }

    /// <summary>
    /// Returns cached locales for this TextAsset, recomputing only if the YAML text changed.
    /// </summary>
    public static List<string> GetLocales(ConvoCoreConversationData data)
    {
        if (data == null) return null;
        var ta = data.ConversationYaml;
        if (ta == null) return null;

        EntityId id = ta.GetEntityId();
        string yamlText = ta.text; // ok on main thread
        if (string.IsNullOrEmpty(yamlText)) return null;

        uint h = Fnva32(yamlText);

        if (_cache.TryGetValue(id, out var entry) && entry.Hash == h)
        {
            // Content unchanged -> return cached list
            return entry.Locales;
        }

        // Content changed or first time -> (re)parse and cache
        var locales = ComputeLocales(yamlText);
        if (entry == null)
        {
            entry = new Entry();
            _cache[id] = entry;
        }

        entry.Hash = h;
        entry.Locales.Clear();
        if (locales != null && locales.Count > 0)
            entry.Locales.AddRange(locales);

        return entry.Locales;
    }

    // Your existing logic, but driven from a string and with minimal allocs
    private static List<string> ComputeLocales(string yamlText)
    {
        try
        {
            // Uses your runtime parser (assumed to normalize keys case-insensitively)
            var dict = ConvoCoreYamlParser.Parse(yamlText);
            if (dict == null) return null;

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in dict)
            {
                var list = kv.Value;
                if (list == null) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    var cfg = list[i];
                    var loc = cfg?.LocalizedDialogue;
                    if (loc == null) continue;

                    foreach (var lang in loc.Keys)
                    {
                        if (!string.IsNullOrWhiteSpace(lang))
                            set.Add(lang.Trim());
                    }
                }
            }

            if (set.Count == 0) return new List<string>(0);

            // Sort once, return pooled list
            var arr = set.ToArray();
            Array.Sort(arr, StringComparer.OrdinalIgnoreCase);
            return new List<string>(arr);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Optional: clear cache for a specific asset when it’s deleted or changed externally.
    /// You can call this from an AssetPostprocessor if desired.
    /// </summary>
    public static void Invalidate(TextAsset ta)
    {
        if (ta != null)
            _cache.Remove(ta.GetEntityId());
    }
}

}