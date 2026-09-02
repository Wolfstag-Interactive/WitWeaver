using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    [HelpURL(
        "https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverYamlUtilities.html")]
    public class WitWeaverYamlUtilities
    {
        private readonly WitWeaverConversationData _witWeaverConversationData;

        public WitWeaverYamlUtilities(WitWeaverConversationData witWeaverConversationData)
        {
            _witWeaverConversationData = witWeaverConversationData;
        }

        /// <summary>
        /// Imports dialogue metadata from the YAML file.
        /// Keeps actions intact for existing keys/indices during reimport.
        /// </summary>
        /// <summary>
        /// Imports dialogue metadata from the YAML file.
        /// Preserves Unity-authored per-line data by ConversationID+LineId (fallback to index for legacy).
        /// </summary>
        public void ImportFromYamlForKey(string conversationKey, bool suppressSourceWriteBack = false)
        {
            if (string.IsNullOrEmpty(_witWeaverConversationData.FilePath) &&
                _witWeaverConversationData.ConversationYaml == null)
            {
                Debug.LogError("filePath not set for the conversation and no ConversationYaml assigned!");
                return;
            }

            string yamlData = _witWeaverConversationData.ConversationYaml != null
                ? _witWeaverConversationData.ConversationYaml.text
                : WitWeaverYamlLoader.Load(_witWeaverConversationData);
            if (string.IsNullOrEmpty(yamlData))
            {
                string sourcesMsg =
                    $"Checked the embedded/assigned TextAsset and the persistent override at persistentDataPath/WitWeaver/Dialogue/{_witWeaverConversationData.FilePath}.yml.";
                Debug.LogError($"YAML file not found. {sourcesMsg}");
                return;
            }
            
            bool verboseLogs = WitWeaverYamlLoader.Settings?.VerboseLogs ?? false;

            var source = _witWeaverConversationData.ConversationYaml != null
                ? $"embedded TextAsset '{_witWeaverConversationData.ConversationYaml.name}'"
                : $"file at FilePath '{_witWeaverConversationData.FilePath}'";

            Dictionary<string, List<DialogueYamlConfig>> dialoguesBySection;
            if (!WitWeaverYamlParser.TryParse(yamlData, out dialoguesBySection,
                    out IReadOnlyList<WitWeaverYamlDiagnostic> parseDiagnostics))
            {
                // Errors always surface regardless of VerboseLogs.
                Debug.LogError(
                    $"WitWeaver: Failed to parse YAML for conversation key '{conversationKey}' " +
                    $"on asset '{_witWeaverConversationData.name}' (source: {source}).\n" +
                    WitWeaverYamlDiagnostic.Format(null, parseDiagnostics));
                return;
            }

            // Surface any warnings only when verbose logging is on.
            if (verboseLogs && parseDiagnostics.Count > 0)
                Debug.LogWarning(
                    $"WitWeaver: YAML for '{conversationKey}' on '{_witWeaverConversationData.name}' " +
                    $"(source: {source}) parsed with warnings.\n" +
                    WitWeaverYamlDiagnostic.Format(null, parseDiagnostics));

            if (!dialoguesBySection.TryGetValue(conversationKey, out var yamlConfigs) || yamlConfigs == null)
            {
                var availableKeys = string.Join(", ", dialoguesBySection.Keys);
                Debug.LogError(
                    $"WitWeaver: Conversation key '{conversationKey}' not found in YAML " +
                    $"for asset '{_witWeaverConversationData.name}'. " +
                    $"Available keys in the YAML: [{availableKeys}]. " +
                    $"Ensure the sheet tab name (for spreadsheets) or top-level YAML key exactly matches the ConversationKey.");
                return;
            }

            bool idsAdded = false;
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < yamlConfigs.Count; i++)
            {
                var cfg = yamlConfigs[i];
                if (cfg == null) continue;

                if (string.IsNullOrWhiteSpace(cfg.LineID))
                {
                    cfg.LineID = WitWeaverLineID.NewLineID();
                    idsAdded = true;
                }

                if (!seenIds.Add(cfg.LineID))
                {
                    Debug.LogError(
                        $"Duplicate LineId '{cfg.LineID}' detected in conversation '{conversationKey}'. " +
                        "LineIds must be unique. Fix the YAML and reimport.");
                    return;
                }
            }

#if UNITY_EDITOR
            // suppressSourceWriteBack: set by imports whose data did not come from the linked YAML
            // file (e.g. the spreadsheet pipeline), so generated LineIDs never overwrite that file.
            if (idsAdded && !suppressSourceWriteBack)
            {
                try
                {
                    string updatedYaml = WitWeaverYamlSerializer.Serialize(dialoguesBySection);

                    bool wrote = false;
                    string assetPath = null;

                    if (!string.IsNullOrEmpty(_witWeaverConversationData.SourceYamlAssetPath))
                        assetPath = _witWeaverConversationData.SourceYamlAssetPath;
                    else if (_witWeaverConversationData.ConversationYaml != null)
                        assetPath = UnityEditor.AssetDatabase.GetAssetPath(_witWeaverConversationData.ConversationYaml);

                    // Writeback only for Assets paths. Never Packages.
                    if (!string.IsNullOrEmpty(assetPath) && assetPath.StartsWith("Assets/"))
                    {
                        var fullPath = System.IO.Path.GetFullPath(assetPath);
                        System.IO.File.WriteAllText(fullPath, updatedYaml);
                        UnityEditor.AssetDatabase.ImportAsset(assetPath);
                        wrote = true;
                    }

                    if (!wrote)
                    {
                        Debug.LogWarning(
                            "WitWeaver: LineId values were generated but could not be written back because the YAML source is not a writable Assets/ project file. " +
                            "To lock IDs (and enable safe CSV import), link a YAML asset under Assets/ or embed YAML into the Conversation asset.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"WitWeaver: Failed to write back generated LineIds to YAML source. {ex.Message}");
                }
            }
#endif

            if (verboseLogs)
                Debug.Log($"Importing {yamlConfigs.Count} lines for conversation key '{conversationKey}'.");

            if (_witWeaverConversationData.DialogueLines == null)
                _witWeaverConversationData.DialogueLines = new List<WitWeaverConversationData.DialogueLineInfo>();

            var updatedDialogueLines = new List<WitWeaverConversationData.DialogueLineInfo>(yamlConfigs.Count);

            for (int i = 0; i < yamlConfigs.Count; i++)
            {
                var yamlConfig = yamlConfigs[i] ?? new DialogueYamlConfig();

                // Loop 1 skips null entries so they never receive a generated LineID.
                // Guard here so those entries (and any other edge case) don't produce a
                // DialogueLineInfo with an empty LineID that triggers a false-positive error.
                if (string.IsNullOrWhiteSpace(yamlConfig.LineID))
                    yamlConfig.LineID = WitWeaverLineID.NewLineID();

                // Preserve existing Unity-authored data by ConversationID+LineId (fallback: same index)
                var existing = FindExistingLine(_witWeaverConversationData, conversationKey, yamlConfig.LineID, i);

                var localizedDialogueList = new List<WitWeaverConversationData.LocalizedDialogue>();
                if (yamlConfig.LocalizedDialogue != null)
                {
                    foreach (var kvp in yamlConfig.LocalizedDialogue)
                    {
                        localizedDialogueList.Add(new WitWeaverConversationData.LocalizedDialogue
                        {
                            Language = kvp.Key,
                            Text = kvp.Value,
                            // YAML only carries text — audio clips are Unity-authored, so carry
                            // them over from the matched existing line or they are lost on reimport.
                            Clip = FindExistingClip(existing, kvp.Key)
                        });
                    }
                }

                var newLineInfo = new WitWeaverConversationData.DialogueLineInfo(conversationKey)
                {
                    ConversationID = conversationKey,
                    ConversationLineIndex = i,
                    LineID = yamlConfig.LineID, // Your field name is LineID
                    characterID = yamlConfig.CharacterID,
                    LocalizedDialogues = localizedDialogueList,

                    // Preserve authored state
                    CharacterRepresentations = existing?.CharacterRepresentations != null
                        ? new List<WitWeaverConversationData.CharacterRepresentationData>(existing
                            .CharacterRepresentations)
                        : new List<WitWeaverConversationData.CharacterRepresentationData>(),

                    ActionsBeforeDialogueLine = existing?.ActionsBeforeDialogueLine != null
                        ? new List<BaseDialogueLineAction>(existing.ActionsBeforeDialogueLine)
                        : new List<BaseDialogueLineAction>(),

                    ActionsAfterDialogueLine = existing?.ActionsAfterDialogueLine != null
                        ? new List<BaseDialogueLineAction>(existing.ActionsAfterDialogueLine)
                        : new List<BaseDialogueLineAction>(),

                    PresentationMode = existing?.PresentationMode ?? _witWeaverConversationData.DefaultPresentationMode,

                    UserInputMethod = existing != null
                        ? existing.UserInputMethod
                        : WitWeaverConversationData.DialogueLineProgressionMethod.UserInput,
                    TimeBeforeNextLine = existing != null ? existing.TimeBeforeNextLine : 0f,
                    LineContinuationSettings = existing != null
                        ? existing.LineContinuationSettings
                        : new WitWeaverConversationData.LineContinuation
                        {
                            Mode = WitWeaverConversationData.LineContinuationMode.Continue,
                            TargetAliasOrName = null,
                            TargetContainer = null,
                            PushReturnPoint = false
                        }
                };

                newLineInfo.EnsureCharacterRepresentationListInitialized();
                updatedDialogueLines.Add(newLineInfo);
            }

            _witWeaverConversationData.DialogueLines = updatedDialogueLines;
        }

        /// <summary>
        /// Searches for an existing dialogue line in the provided conversation data.
        /// Matches lines based on ConversationID and LineID if available, or falls back to the provided index.
        /// </summary>
        /// <param name="data">The conversation data containing a list of dialogue lines to search.</param>
        /// <param name="key">The unique identifier for the conversation to match.</param>
        /// <param name="lineId">The unique identifier for the specific line of dialogue. Can be null or empty.</param>
        /// <param name="indexFallback">The fallback index to use when LineID is not provided or no match is found by LineID.</param>
        /// <returns>
        /// The matching <c>DialogueLineInfo</c> instance if found, or <c>null</c> if no match is found.
        /// </returns>
        private static WitWeaverConversationData.DialogueLineInfo FindExistingLine(
            WitWeaverConversationData data, string key, string lineId, int indexFallback)
        {
            if (data?.DialogueLines == null) return null;

            if (!string.IsNullOrEmpty(lineId))
            {
                for (int i = 0; i < data.DialogueLines.Count; i++)
                {
                    var dl = data.DialogueLines[i];
                    if (dl.ConversationID == key && dl.LineID == lineId)
                        return dl;
                }

                // No line with this LineID exists: the YAML line is new (or its ID changed).
                // Falling back to the index here would make it inherit an unrelated line's
                // authored actions/representations/clips, so it starts fresh instead.
                return null;
            }

            // Legacy fallback for YAML lines that carry no LineID at all
            for (int i = 0; i < data.DialogueLines.Count; i++)
            {
                var dl = data.DialogueLines[i];
                if (dl.ConversationID == key && dl.ConversationLineIndex == indexFallback)
                    return dl;
            }
            return null;
        }

        /// <summary>
        /// Returns the authored <c>AudioClip</c> for the given language from an existing line's
        /// localized entries, or <c>null</c> when there is no existing line or no clip for that language.
        /// </summary>
        private static AudioClip FindExistingClip(
            WitWeaverConversationData.DialogueLineInfo existing, string language)
        {
            if (existing?.LocalizedDialogues == null || string.IsNullOrEmpty(language))
                return null;

            for (int i = 0; i < existing.LocalizedDialogues.Count; i++)
            {
                var ld = existing.LocalizedDialogues[i];
                if (string.Equals(ld.Language, language, StringComparison.OrdinalIgnoreCase))
                    return ld.Clip;
            }
            return null;
        }
    }
}