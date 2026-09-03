// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.IO;
using Object = UnityEngine.Object;
using System.Linq;
using System.Collections.Generic;

namespace WolfstagInteractive.WitWeaver.Editor
{
    /// <summary>
    /// The single custom inspector for <see cref="WitWeaverConversationData"/>.
    /// Combines YAML/Spreadsheet linking and import, participant configuration, dialogue line
    /// rendering and validation tooling with the presentation-mode and audio-manifest sections.
    /// </summary>
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverConversationDataEditor.html")]
    [CustomEditor(typeof(WitWeaverConversationData))]
    public class WitWeaverConversationDataEditor : UnityEditor.Editor
    {
        private SerializedProperty _conversationKey;
        private SerializedProperty _filePath;
        private SerializedProperty _allowPersistentOverrides;

        private SerializedProperty _conversationYaml;
        private SerializedProperty _sourceYaml;
        private SerializedProperty _sourceYamlAssetPath;
        private SerializedProperty _sourceSpreadsheetAsset;
        private SerializedProperty _sourceSpreadsheetAssetPath;

        private const string SourceTabPrefKey = "WitWeaver.ConversationEditor.DialogueSourceTab";
        private static readonly string[] SourceTabLabels = { "YAML", "Spreadsheet" };
        private int _sourceTab;

        // Cross-source staleness check state (throttled; see DrawSharedEmbedBlock)
        private double _lastStaleCheckTime;
        private bool _otherSourceIsNewer;
        private bool _otherSourceMissing;
        private string _otherSourcePath;

        private string[] _cachedSupportedLanguages;
        private string[] _cachedDisplayLanguages;
        private List<string> _cachedYamlLocales;
        private double _lastYamlCheckTime;
        private const double YAML_CHECK_INTERVAL = 1.0;

        private ReorderableList _participantConfigList;
        private void OnEnable()
        {
            _sourceTab = EditorPrefs.GetInt(SourceTabPrefKey, 0);
            CacheLanguageSettings();
        }
        private void CacheLanguageSettings()
        {
            var loader = new WitWeaverLanguageSettingsLoader();
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
            _allowPersistentOverrides = serializedObject.FindProperty("AllowPersistentOverrides");
            _conversationKey          = serializedObject.FindProperty("ConversationKey");
            _conversationYaml         = serializedObject.FindProperty("ConversationYaml");
            _sourceYaml               = serializedObject.FindProperty("SourceYaml");
            _sourceYamlAssetPath      = serializedObject.FindProperty("SourceYamlAssetPath");
            _sourceSpreadsheetAsset         = serializedObject.FindProperty("SourceSpreadsheetAsset");
            _sourceSpreadsheetAssetPath     = serializedObject.FindProperty("SourceSpreadsheetAssetPath");

            // Auto-sync ParticipantConfigurationDefaults before drawing.
            SyncParticipantConfigurationDefaults();

            // Optional sections contributed by other editor assemblies (e.g. the graph editor).
            WitWeaverConversationInspectorHooks.InvokeDrawExtraSection((WitWeaverConversationData)target);

            // Presentation & audio sections (drawn up top; their properties are
            // skipped in the generic iteration below so they are not drawn twice).
            DrawPresentationSection();
            DrawAudioSection();

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
                    property.name == "AllowPersistentOverrides" ||
                    property.name == "SourceSpreadsheetAsset" ||
                    property.name == "SourceSpreadsheetAssetPath" ||
                    property.name == "EmbeddedFromSourcePath" ||
                    property.name == "EmbeddedAtTicks" ||
                    property.name == "ConversationKey" ||
                    property.name == "ConversationYaml" ||
                    property.name == "SourceYaml" ||
                    property.name == "SourceYamlAssetPath" ||
                    property.name == "DefaultPresentationMode" ||
                    property.name == "AudioManifest" ||
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
                    // Graph-authored conversations edit lines exclusively in the graph; the
                    // baked list is hidden here so there is a single editing surface.
                    if (IsGraphAuthored())
                        DrawGraphModeLinesSummary(property);
                    else
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
                var conversationData = (WitWeaverConversationData)target;
                conversationData.ValidateAndFixDialogueLines();
                EditorUtility.SetDirty(target);
            }
            DrawLanguagePreviewSection();

            // Add validation tools section
            DrawValidationToolsSection();

            // Unified source linking: YAML | Spreadsheet tabs plus the shared embed/status block
            DrawDialogueSourceSection();
            // Persistent override configuration
            DrawFilePathField();

            if (IsGraphAuthored())
                DrawGraphModeKeySection();
            else
                DrawAddDeleteSection();

            // Apply any modified properties
            serializedObject.ApplyModifiedProperties();
        }

        private bool IsGraphAuthored() =>
            target is WitWeaverConversationData data &&
            data.AuthoringMode == WitWeaverConversationData.ConversationAuthoringMode.Graph;

        /// <summary>Replaces the editable line list while the conversation is graph-authored.</summary>
        private static void DrawGraphModeLinesSummary(SerializedProperty linesProp)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Dialogue Lines", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"{linesProp.arraySize} baked line(s). This conversation is authored as a node graph — " +
                "open the graph to edit lines, choices and flow, then bake to update this asset.",
                MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Graph-mode replacement for the Line Data Controls box: the key stays editable (it must
        /// match the YAML top-level key), but list-level tooling (import button, delete-all) is
        /// hidden because those operations flow through the graph's bake and refresh.
        /// </summary>
        private void DrawGraphModeKeySection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Conversation Key", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Unique identifier for the conversation; must match the top-level key in the YAML file.",
                MessageType.Info);
            EditorGUILayout.PropertyField(_conversationKey, new GUIContent("Conversation Key"));
            EditorGUILayout.EndVertical();
        }

        // ------------------------------------------------------------------
        // Presentation & Audio
        // ------------------------------------------------------------------

        private void DrawPresentationSection()
        {
            var conversation = (WitWeaverConversationData)target;

            EditorGUILayout.LabelField("Presentation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("DefaultPresentationMode"),
                new GUIContent("Default Presentation Mode",
                    "Default mode applied to new lines during YAML sync. Does not retroactively change existing lines."));

            if (conversation.DefaultPresentationMode == ConversationPresentationMode.AudioOnly &&
                conversation.AudioManifest == null)
            {
                EditorGUILayout.HelpBox(
                    "Presentation mode is AudioOnly but no Audio Manifest is assigned. " +
                    "Lines will advance immediately with no audio or text output.",
                    MessageType.Warning);
            }

            if (GUILayout.Button("Apply Default Mode to All Lines"))
                ApplyDefaultModeToAllLines(conversation);

            EditorGUILayout.Space(6f);
        }

        private void DrawAudioSection()
        {
            var conversation = (WitWeaverConversationData)target;

            EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AudioManifest"),
                new GUIContent("Audio Manifest",
                    "Optional. Assign to enable voice clip playback for this conversation."));

            if (conversation.AudioManifest == null)
            {
                if (GUILayout.Button("Create Audio Manifest"))
                    CreateAudioManifest(conversation);
            }

            EditorGUILayout.Space(6f);
        }

        private static void ApplyDefaultModeToAllLines(WitWeaverConversationData conversation)
        {
            if (conversation.DialogueLines == null || conversation.DialogueLines.Count == 0)
            {
                EditorUtility.DisplayDialog("Apply Mode", "No dialogue lines found.", "OK");
                return;
            }

            Undo.RecordObject(conversation, "Apply Default Presentation Mode to All Lines");
            foreach (var line in conversation.DialogueLines)
                if (line != null)
                    line.PresentationMode = conversation.DefaultPresentationMode;
            EditorUtility.SetDirty(conversation);
            Debug.Log($"[WitWeaver] Applied '{conversation.DefaultPresentationMode}' to {conversation.DialogueLines.Count} line(s) on '{conversation.name}'.");
        }

        private static void CreateAudioManifest(WitWeaverConversationData conversation)
        {
            string conversationPath = AssetDatabase.GetAssetPath(conversation);
            string folder = Path.GetDirectoryName(conversationPath)?.Replace('\\', '/') ?? "Assets";
            string assetName = conversation.name + "_AudioManifest";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");

            var manifest = CreateInstance<WitWeaverAudioManifest>();
            manifest.Mode               = AudioManifestMode.ConversationDriven;
            manifest.SourceConversation = conversation;

            AssetDatabase.CreateAsset(manifest, assetPath);

            // Sync rows from the conversation
            SyncManifestRows(manifest, conversation);

            // Assign back to conversation
            Undo.RecordObject(conversation, "Create Audio Manifest");
            conversation.AudioManifest = manifest;
            EditorUtility.SetDirty(conversation);

            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(manifest);
            Debug.Log($"[WitWeaver] Created audio manifest at '{assetPath}'.");
        }

        private static void SyncManifestRows(WitWeaverAudioManifest manifest, WitWeaverConversationData conversation)
        {
            if (conversation.DialogueLines == null) return;

            manifest.Entries = new List<WitWeaverAudioManifest.AudioEntry>();

            foreach (var line in conversation.DialogueLines)
            {
                if (line == null) continue;

                if (line.LocalizedDialogues != null && line.LocalizedDialogues.Count > 0)
                {
                    foreach (var ld in line.LocalizedDialogues)
                    {
                        manifest.Entries.Add(new WitWeaverAudioManifest.AudioEntry
                        {
                            LineID      = line.LineID,
                            CharacterID = line.characterID,
                            Language    = ld.Language ?? ""
                        });
                    }
                }
                else
                {
                    manifest.Entries.Add(new WitWeaverAudioManifest.AudioEntry
                    {
                        LineID      = line.LineID,
                        CharacterID = line.characterID,
                        Language    = ""
                    });
                }
            }

            EditorUtility.SetDirty(manifest);
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
                    var conversationData = (WitWeaverConversationData)target;
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
            var conversationData = (WitWeaverConversationData)target;
            var ids = new List<string>();
            foreach (var profile in conversationData.ConversationParticipantProfiles)
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
                EditorGUILayout.HelpBox("No supported locales configured in WitWeaver Settings.", MessageType.Warning);
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
            var lm = WitWeaverLanguageManager.Instance;
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
            var data = (WitWeaverConversationData)target;

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
                var settings = WitWeaverSettings.Instance;
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
        private static List<string> TryGetLocalesFromEmbedded(WitWeaverConversationData data)
        {
            try
            {
                if (data == null || data.ConversationYaml == null) return null;
                var yamlText = data.ConversationYaml.text;
                if (string.IsNullOrEmpty(yamlText)) return null;

                // Uses the runtime parser (already normalizes keys to case-insensitive dictionary).
                // TryParse is used here so a broken YAML file surfaces a warning rather than
                // silently returning null and leaving the inspector with no locale options.
                if (!WitWeaverYamlParser.TryParse(yamlText, out var dict, out string parseErr))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[WitWeaver] Could not read locales from embedded YAML:\n{parseErr}", data);
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
                    $"[WitWeaver] Unexpected error reading locale keys from embedded YAML: {ex.Message}", data);
                return null;
            }
        }

        private void DrawDialogueSourceSection()
        {
            // Safety: if these props don't exist (older data class), skip drawing the section
            if (_sourceYaml == null || _conversationYaml == null || _sourceYamlAssetPath == null ||
                _sourceSpreadsheetAsset == null || _sourceSpreadsheetAssetPath == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Dialogue Source", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var newTab = GUILayout.Toolbar(_sourceTab, SourceTabLabels);
            if (EditorGUI.EndChangeCheck())
            {
                _sourceTab = newTab;
                EditorPrefs.SetInt(SourceTabPrefKey, _sourceTab);
            }

            EditorGUILayout.Space(4);

            if (_sourceTab == 0)
                DrawYamlSourcePanel();
            else
                DrawSpreadsheetSourcePanel();

            Separator();
            DrawSharedEmbedBlock();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawYamlSourcePanel()
        {
            EditorGUILayout.HelpBox(
                "Link a plain .yaml file you edit normally. WitWeaver embeds its text as a sub-asset used at runtime. " +
                "Saving the file auto-syncs the embed and the compiled lines' text.",
                MessageType.Info);

            // Source .yaml (DefaultAsset or TextAsset)
            EditorGUILayout.PropertyField(_sourceYaml, new GUIContent("Source .yaml (Editor-only)"));

            if (!string.IsNullOrEmpty(_sourceYamlAssetPath.stringValue))
            {
                EditorGUILayout.LabelField("Linked Path", _sourceYamlAssetPath.stringValue, EditorStyles.miniLabel);
            }

            bool inPlayMode = EditorApplication.isPlayingOrWillChangePlaymode;

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(inPlayMode))
            {
                var linkLabel = _conversationYaml.objectReferenceValue == null ? "Link & Embed" : "Sync From Source";
                if (GUILayout.Button(new GUIContent(linkLabel,
                        "Validate the linked .yaml (parse and LineID checks), embed its text, and sync compiled line text."),
                        GUILayout.Height(24)))
                {
                    var data = (WitWeaverConversationData)target;
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
                            // Same validated embed + text sync the YAML watcher uses on file save
                            var result = WitWeaverYamlWatcher.EmbedFromPath(data, srcPath);
                            if (result != WitWeaverYamlWatcher.EmbedResult.Failed)
                            {
                                // EmbedFromPath writes to the target directly; refresh before linking
                                serializedObject.Update();
                                _sourceYamlAssetPath.stringValue = srcPath;
                                serializedObject.ApplyModifiedProperties();

                                if (result == WitWeaverYamlWatcher.EmbedResult.Embedded)
                                    Debug.Log($"WitWeaver: Embedded YAML text from '{srcPath}' into '{AssetDatabase.GetAssetPath(data)}'.");
                            }
                        }
                    }
                }
            }

            using (new EditorGUI.DisabledScope(_sourceYaml.objectReferenceValue == null))
            {
                if (GUILayout.Button("Ping Source", GUILayout.Height(24)))
                {
                    EditorGUIUtility.PingObject(_sourceYaml.objectReferenceValue);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(inPlayMode ||
                       (_sourceYaml.objectReferenceValue == null &&
                        string.IsNullOrEmpty(_sourceYamlAssetPath.stringValue))))
            {
                if (GUILayout.Button(new GUIContent("Clear Link (keeps embedded)",
                        "Unlink the source file without touching the embedded YAML."), GUILayout.Height(22)))
                {
                    _sourceYaml.objectReferenceValue = null;
                    _sourceYamlAssetPath.stringValue = string.Empty;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            using (new EditorGUI.DisabledScope(inPlayMode || _conversationYaml.objectReferenceValue == null))
            {
                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button(new GUIContent("Clear Embedded",
                        "Permanently delete the embedded YAML TextAsset from this asset."), GUILayout.Height(22)))
                {
                    if (EditorUtility.DisplayDialog("Clear Embedded YAML",
                        "Are you sure you want to remove the embedded YAML? This will delete the embedded TextAsset permanently.",
                        "Yes, Remove", "Cancel"))
                    {
                        var data = (WitWeaverConversationData)target;
                        ClearEmbeddedYaml(data);

                        // Update the SerializedProperty to reflect the change
                        _conversationYaml.objectReferenceValue = null;
                        serializedObject.ApplyModifiedProperties();

                        Debug.Log("WitWeaver: Cleared embedded YAML.");
                    }
                }
                GUI.backgroundColor = prevColor;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSpreadsheetSourcePanel()
        {
            if (_sourceSpreadsheetAsset.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Link an .xlsx file to enable spreadsheet-driven dialogue authoring. " +
                    "Each sheet tab name must match a ConversationKey. " +
                    "Configure expected column headers in WitWeaverSettings under the Spreadsheet tab.",
                    MessageType.Info);
            }

            // Object field for SourceSpreadsheetAsset
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_sourceSpreadsheetAsset, new GUIContent("Source .xlsx (Editor-only)"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                var newObj = _sourceSpreadsheetAsset.objectReferenceValue;
                _sourceSpreadsheetAssetPath.stringValue = newObj != null ? AssetDatabase.GetAssetPath(newObj) : string.Empty;
                serializedObject.ApplyModifiedProperties();
            }

            if (_sourceSpreadsheetAsset.objectReferenceValue == null)
                return;

            var assignedPath = _sourceSpreadsheetAssetPath.stringValue;
            if (!assignedPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                EditorGUILayout.HelpBox(
                    "The assigned file does not appear to be an .xlsx file. Only .xlsx spreadsheets are supported.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Linked Path", assignedPath, EditorStyles.miniLabel);

            bool inPlayMode = EditorApplication.isPlayingOrWillChangePlaymode;

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(inPlayMode))
            {
                if (GUILayout.Button(new GUIContent("Import from Spreadsheet",
                        "Parse the workbook, write generated LineIDs back into it, embed the generated YAML, and rebuild the dialogue lines."),
                        GUILayout.Height(24)))
                {
                    WitWeaverSpreadsheetUtilities.RunFullPipeline((WitWeaverConversationData)target, assignedPath, out _);
                    serializedObject.Update();
                }
            }

            if (GUILayout.Button("Ping Source", GUILayout.Height(24)))
            {
                EditorGUIUtility.PingObject(_sourceSpreadsheetAsset.objectReferenceValue);
            }

            using (new EditorGUI.DisabledScope(inPlayMode))
            {
                if (GUILayout.Button(new GUIContent("Clear Link (keeps embedded)",
                        "Unlink the workbook without touching the embedded YAML."), GUILayout.Height(24)))
                {
                    _sourceSpreadsheetAsset.objectReferenceValue = null;
                    _sourceSpreadsheetAssetPath.stringValue = string.Empty;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            EditorGUILayout.EndHorizontal();

            // Latest import outcome: manual button or watcher-triggered auto-sync
            if (WitWeaverSpreadsheetImportStatus.TryGet((WitWeaverConversationData)target,
                    out bool importOk, out string importMsg, out var importWhenUtc) &&
                !string.IsNullOrEmpty(importMsg))
            {
                EditorGUILayout.HelpBox($"{importMsg} ({FormatRelativeTime(importWhenUtc)})",
                    importOk ? MessageType.Info : MessageType.Error);
            }
        }

        private static string FormatRelativeTime(DateTime whenUtc)
        {
            var span = DateTime.UtcNow - whenUtc;
            if (span.TotalSeconds < 60) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }

        /// <summary>
        /// The source-agnostic embed display shared by both tabs: the embedded TextAsset,
        /// its provenance, cross-source staleness/dual-link notices, and the sync status.
        /// </summary>
        private void DrawSharedEmbedBlock()
        {
            var data = (WitWeaverConversationData)target;

            // Current embedded TextAsset (read-only)
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_conversationYaml, new GUIContent("Embedded YAML (used at runtime)"));
            }

            bool hasEmbed = _conversationYaml.objectReferenceValue != null;
            bool hasYamlLink = !string.IsNullOrEmpty(_sourceYamlAssetPath.stringValue);
            bool hasSpreadsheetLink = !string.IsNullOrEmpty(_sourceSpreadsheetAssetPath.stringValue);

            if (hasEmbed)
            {
                if (!string.IsNullOrEmpty(data.EmbeddedFromSourcePath))
                {
                    var kind = WitWeaverEmbedUtility.IsSpreadsheetSource(data.EmbeddedFromSourcePath) ? "Spreadsheet" : "YAML";
                    EditorGUILayout.LabelField("Embedded From",
                        $"{data.EmbeddedFromSourcePath} ({kind})", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("Embedded From",
                        "unknown (embed predates source tracking; run a sync or import to record it)",
                        EditorStyles.miniLabel);
                }
            }
            else if (!hasYamlLink && !hasSpreadsheetLink)
            {
                EditorGUILayout.HelpBox(
                    "No dialogue source linked. Link a .yaml or .xlsx file above to embed dialogue into this asset.",
                    MessageType.Info);
            }

            UpdateCrossSourceStaleness(data, hasYamlLink, hasSpreadsheetLink);

            if (hasEmbed && _otherSourceIsNewer)
            {
                var embedKind = WitWeaverEmbedUtility.IsSpreadsheetSource(data.EmbeddedFromSourcePath) ? "Spreadsheet" : "YAML";
                var otherKind = WitWeaverEmbedUtility.IsSpreadsheetSource(_otherSourcePath) ? "Spreadsheet" : "YAML";
                EditorGUILayout.HelpBox(
                    $"The linked {otherKind} source '{_otherSourcePath}' was modified after the current embed " +
                    $"(which came from the {embedKind} source). The next import from that file will overwrite " +
                    "the embedded dialogue (last writer wins).",
                    MessageType.Warning);
            }
            else if (hasEmbed && _otherSourceMissing)
            {
                EditorGUILayout.HelpBox(
                    $"The linked source '{_otherSourcePath}' could not be found on disk.",
                    MessageType.Warning);
            }
            else if (hasYamlLink && hasSpreadsheetLink)
            {
                var provenance = string.IsNullOrEmpty(data.EmbeddedFromSourcePath)
                    ? "the current embed's source is unknown."
                    : $"the embed currently comes from the " +
                      $"{(WitWeaverEmbedUtility.IsSpreadsheetSource(data.EmbeddedFromSourcePath) ? "Spreadsheet" : "YAML")} source.";
                EditorGUILayout.HelpBox(
                    $"Both a YAML and a Spreadsheet source are linked. Whichever is imported most recently wins; {provenance}",
                    MessageType.Info);
            }

            DrawEmbeddedSyncStatus();
        }

        /// <summary>
        /// Throttled check of whether a linked source OTHER than the one that produced the embed
        /// has a newer write time than the embed itself. Advisory only (VCS checkouts refresh
        /// write times); same-source staleness is not checked because the watchers auto-heal it.
        /// </summary>
        private void UpdateCrossSourceStaleness(WitWeaverConversationData data, bool hasYamlLink, bool hasSpreadsheetLink)
        {
            if (EditorApplication.timeSinceStartup - _lastStaleCheckTime < YAML_CHECK_INTERVAL)
                return;
            _lastStaleCheckTime = EditorApplication.timeSinceStartup;

            _otherSourceIsNewer = false;
            _otherSourceMissing = false;
            _otherSourcePath = null;

            if (data.EmbeddedAtTicks <= 0) return;

            var candidates = new List<string>(2);
            if (hasYamlLink && _sourceYamlAssetPath.stringValue != data.EmbeddedFromSourcePath)
                candidates.Add(_sourceYamlAssetPath.stringValue);
            if (hasSpreadsheetLink && _sourceSpreadsheetAssetPath.stringValue != data.EmbeddedFromSourcePath)
                candidates.Add(_sourceSpreadsheetAssetPath.stringValue);

            foreach (var path in candidates)
            {
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    if (!File.Exists(fullPath))
                    {
                        _otherSourceMissing = true;
                        _otherSourcePath = path;
                        continue;
                    }

                    if (File.GetLastWriteTimeUtc(fullPath).Ticks > data.EmbeddedAtTicks)
                    {
                        _otherSourceIsNewer = true;
                        _otherSourcePath = path;
                        return;
                    }
                }
                catch
                {
                    // Unreadable path: treat like a missing file
                    _otherSourceMissing = true;
                    _otherSourcePath = path;
                }
            }
        }

        /// <summary>
        /// Shows a persistent warning when the compiled DialogueLines have drifted from the
        /// embedded dialogue source (YAML or spreadsheet-generated): structural drift needs an
        /// explicit structural import, while stale text can be synced in place.
        /// </summary>
        private void DrawEmbeddedSyncStatus()
        {
            var data = (WitWeaverConversationData)target;
            if (!YamlSyncStatusCache.Get(data, out int staleText, out int yamlOnly, out int assetOnly))
                return;

            if (yamlOnly == 0 && assetOnly == 0 && staleText == 0)
                return;

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (yamlOnly > 0 || assetOnly > 0)
                {
                    var message =
                        $"Structure drift: {yamlOnly} line(s) exist only in the embedded source and {assetOnly} " +
                        "line(s) exist only in this asset. Text stays synced for matching lines, but added or " +
                        "removed lines require a structural import.";

                    if (IsGraphAuthored())
                    {
                        // Structure flows through the graph for graph-authored assets
                        EditorGUILayout.HelpBox(
                            message + " This asset is graph-authored: use the graph's bake to update structure.",
                            MessageType.Warning);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(message, MessageType.Warning);

                        if (GUILayout.Button("Import From YAML For Key"))
                            RunImportFromYamlForKey();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"{staleText} dialogue line(s) have text that is out of date with the embedded source.",
                        MessageType.Warning);

                    if (GUILayout.Button("Sync Text Now"))
                    {
                        if (WitWeaverYamlTextSync.TryParseEmbedded(data, out var dict) &&
                            WitWeaverYamlTextSync.MergeLocalizedText(data, dict))
                        {
                            EditorUtility.SetDirty(data);
                            AssetDatabase.SaveAssets();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Single home for the structural import action. Write-back to a linked YAML file is
        /// suppressed when the embed came from a spreadsheet, so spreadsheet-derived data never overwrites it.
        /// </summary>
        private void RunImportFromYamlForKey()
        {
            if (string.IsNullOrEmpty(_conversationKey.stringValue))
            {
                Debug.LogError("Please provide a valid conversation key.");
                return;
            }

            var data = (WitWeaverConversationData)target;
            data.WitWeaverYamlUtilities.ImportFromYamlForKey(_conversationKey.stringValue,
                suppressSourceWriteBack: WitWeaverEmbedUtility.IsSpreadsheetSource(data.EmbeddedFromSourcePath));
            data.ValidateAndFixDialogueLines();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }


        private static void ClearEmbeddedYaml(WitWeaverConversationData data)
        {
            // Path of the Conversation asset
            var convPath = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(convPath))
            {
                Debug.LogError("WitWeaver: Could not resolve asset path for Conversation asset.");
                return;
            }

            // Remove the current embedded TextAsset from the field
            if (data.ConversationYaml != null)
            {
                Object.DestroyImmediate(data.ConversationYaml, true);
                data.ConversationYaml = null;
            }

            // The embed is gone, so its provenance no longer applies
            WitWeaverEmbedUtility.ClearProvenance(data);

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
                var conversationData = (WitWeaverConversationData)target;
                conversationData.ValidateAndFixDialogueLines();
                EditorUtility.SetDirty(target);
                Debug.Log($"Manual validation completed for {conversationData.name}");
            }

            // Debug profiles button
            if (GUILayout.Button("Debug Character Profiles", GUILayout.Height(25)))
            {
                var conversationData = (WitWeaverConversationData)target;
                conversationData.DebugCharacterProfiles();
            }

            EditorGUILayout.EndHorizontal();

            // Second row of buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload YAML Now",GUILayout.Height(25)))
            {
                var data = (WitWeaverConversationData)target;
                data.InitializeDialogueData();           // uses the embedded TextAsset first
                EditorUtility.SetDirty(target);
                Debug.Log($"Reloaded YAML for '{data.name}'.");
            }
            // Sync object references button
            if (GUILayout.Button("Sync Object References", GUILayout.Height(25)))
            {
                var conversationData = (WitWeaverConversationData)target;
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
        /// Draws the optional persistent-override configuration (post-ship text hotfixes).
        /// </summary>
        private void DrawFilePathField()
        {
            EditorGUILayout.BeginVertical("box");
            // Section header
            EditorGUILayout.LabelField("Persistent Override (optional)", EditorStyles.boldLabel);

            // Description
            EditorGUILayout.HelpBox(
                "Lets a shipped game override this conversation's dialogue text with a YAML file placed in " +
                "persistentDataPath/WitWeaver/Dialogue/. Useful for live-ops hotfixes and on-device iteration. " +
                "Overrides can only change text, not conversation structure.",
                MessageType.Info
            );

            if (_allowPersistentOverrides != null)
                EditorGUILayout.PropertyField(_allowPersistentOverrides, new GUIContent("Allow Persistent Overrides"));

            bool overridesEnabled = _allowPersistentOverrides == null || _allowPersistentOverrides.boolValue;
            using (new EditorGUI.DisabledScope(!overridesEnabled))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_filePath, new GUIContent("File Path",
                    "Relative path without extension. The override is looked up at " +
                    "persistentDataPath/WitWeaver/Dialogue/<File Path>.yml (or .yaml)."));
                if (EditorGUI.EndChangeCheck())
                {
                    _filePath.stringValue = StripYamlExtension(_filePath.stringValue);
                }

                // Pre-existing data may still carry an extension, which breaks the lookup
                // (it would resolve to '<value>.yml.yml' and never match)
                if (HasYamlExtension(_filePath.stringValue))
                {
                    EditorGUILayout.HelpBox(
                        "File Path should not include an extension; the override lookup would resolve to " +
                        $"'{_filePath.stringValue}.yml' and never be found.",
                        MessageType.Warning);
                    if (GUILayout.Button("Fix: Remove Extension"))
                    {
                        _filePath.stringValue = StripYamlExtension(_filePath.stringValue);
                        serializedObject.ApplyModifiedProperties();
                    }
                }

                if (!string.IsNullOrEmpty(_filePath.stringValue))
                {
                    EditorGUILayout.LabelField("Override Path",
                        $"persistentDataPath/WitWeaver/Dialogue/{_filePath.stringValue}.yml",
                        EditorStyles.miniLabel);
                }

                // Convenience: default the stem to the asset's name
                if (GUILayout.Button("Use Asset Name"))
                {
                    _filePath.stringValue = target.name;
                    serializedObject.ApplyModifiedProperties();
                }
            }
            EditorGUILayout.EndVertical();

        }

        private static bool HasYamlExtension(string value)
            => !string.IsNullOrEmpty(value) &&
               (value.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase));

        private static string StripYamlExtension(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                return value.Substring(0, value.Length - 5);
            if (value.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                return value.Substring(0, value.Length - 4);
            return value;
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

            // Legacy escape hatch: with an embed present, the structural import lives in the
            // Dialogue Source sync-status warning (which appears exactly when an import is
            // needed). Without an embed (FilePath/persistent-override based loading), that
            // warning cannot trigger, so keep the button available here.
            if (_conversationYaml == null || _conversationYaml.objectReferenceValue == null)
            {
                if (GUILayout.Button("Import From YAML For Key"))
                    RunImportFromYamlForKey();
            }
        }
    }
    /// <summary>
    /// Caches the drift/stale-text analysis drawn by the conversation inspector so the embedded
    /// YAML is only reparsed when the embedded text or the compiled lines actually change.
    /// </summary>
    static class YamlSyncStatusCache
    {
        private sealed class Entry
        {
            public uint Fingerprint;
            public bool Parsed;
            public int StaleText;
            public int YamlOnly;
            public int AssetOnly;
        }

        private static readonly Dictionary<EntityId, Entry> _cache = new Dictionary<EntityId, Entry>();

        /// <summary>
        /// Returns false when the asset has no embedded YAML or it fails to parse; otherwise
        /// outputs the current sync analysis, recomputing only when the data changed.
        /// </summary>
        public static bool Get(WitWeaverConversationData data, out int staleText, out int yamlOnly, out int assetOnly)
        {
            staleText = 0;
            yamlOnly = 0;
            assetOnly = 0;

            var text = data != null && data.ConversationYaml != null ? data.ConversationYaml.text : null;
            if (string.IsNullOrEmpty(text)) return false;

            uint fp = Fingerprint(data, text);
            EntityId id = data.GetEntityId();

            if (!_cache.TryGetValue(id, out var entry) || entry.Fingerprint != fp)
            {
                if (entry == null)
                {
                    entry = new Entry();
                    _cache[id] = entry;
                }

                entry.Fingerprint = fp;
                entry.Parsed = WitWeaverYamlTextSync.TryParseEmbedded(data, out var dict);
                if (entry.Parsed)
                {
                    WitWeaverYamlTextSync.Analyze(data, dict,
                        out entry.StaleText, out entry.YamlOnly, out entry.AssetOnly);
                }
            }

            if (!entry.Parsed) return false;

            staleText = entry.StaleText;
            yamlOnly = entry.YamlOnly;
            assetOnly = entry.AssetOnly;
            return true;
        }

        private static uint Fingerprint(WitWeaverConversationData data, string embeddedText)
        {
            unchecked
            {
                uint hash = 2166136261;
                hash = Fnva32(hash, embeddedText);

                if (data.DialogueLines != null)
                {
                    foreach (var line in data.DialogueLines)
                    {
                        if (line == null) continue;
                        hash = Fnva32(hash, line.ConversationID);
                        hash = Fnva32(hash, line.LineID);
                        hash ^= (uint)line.ConversationLineIndex;
                        hash *= 16777619;

                        if (line.LocalizedDialogues == null) continue;
                        foreach (var localized in line.LocalizedDialogues)
                        {
                            hash = Fnva32(hash, localized.Language);
                            hash = Fnva32(hash, localized.Text);
                        }
                    }
                }

                return hash;
            }
        }

        // FNV-1a 32-bit, continued from an existing hash; null strings hash as a separator
        private static uint Fnva32(uint hash, string s)
        {
            unchecked
            {
                const uint fnvPrime = 16777619;
                hash ^= 0xFF;
                hash *= fnvPrime;
                if (s == null) return hash;
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= fnvPrime;
                }
                return hash;
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
    public static List<string> GetLocales(WitWeaverConversationData data)
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
            var dict = WitWeaverYamlParser.Parse(yamlText);
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
