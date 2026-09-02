using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
#if UNITY_EDITOR
using WolfstagInteractive.WitWeaver.Editor;
#endif

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Role of a line's representation slot during resolution
    /// (see <see cref="WitWeaverConversationData.ResolveRepresentation"/>).
    /// </summary>
    public enum RepresentationRole
    {
        /// <summary>
        /// The index-0 speaker slot. Never resolves to null while the profile has any
        /// representation: an empty selection auto-assigns the first entry with a warning,
        /// and a stale reference falls back loudly.
        /// </summary>
        Speaker,

        /// <summary>
        /// A non-speaker visible slot. An empty selection is a legal "None" and resolves to
        /// null with no logging.
        /// </summary>
        Visible
    }

    [HelpURL(
        "https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverConversationData.html")]
    [CreateAssetMenu(fileName = "New WitWeaver Conversation",
        menuName = "WitWeaver/Conversation Dialogue Object")]
    public partial class WitWeaverConversationData : ScriptableObject
    {
        /// <summary>Human-readable title, separate from the asset file name.</summary>
        public string ConversationTitle;

        public List<WitWeaverCharacterProfileBaseData> ConversationParticipantProfiles =
            new List<WitWeaverCharacterProfileBaseData>();

        [Tooltip("Per-participant default configuration entry names for prefab representations. " +
                 "These override the representation asset's default entry when no per-line entry name is set.")]
        public List<ParticipantConfigurationSlot> ParticipantConfigurationDefaults =
            new List<ParticipantConfigurationSlot>();

        /// <summary>
        /// Returns the default configuration entry name for a participant, as set in
        /// <see cref="ParticipantConfigurationDefaults"/>. Returns null if no slot is configured
        /// for the given character ID.
        /// </summary>
        public string GetParticipantDefaultEntry(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            foreach (var slot in ParticipantConfigurationDefaults)
                if (slot.CharacterID == characterId && !string.IsNullOrEmpty(slot.DefaultConfigurationEntryName))
                    return slot.DefaultConfigurationEntryName;
            return null;
        }

        /// <summary>
        /// Returns true if the given line should display text in the UI.
        /// </summary>
        public bool ShouldDisplayText(DialogueLineInfo line)
        {
            return line.PresentationMode != ConversationPresentationMode.AudioOnly;
        }

        /// <summary>
        /// Returns true if the given line should trigger audio playback.
        /// </summary>
        public bool ShouldPlayAudio(DialogueLineInfo line)
        {
            return line.PresentationMode != ConversationPresentationMode.TextOnly;
        }

        public List<DialogueLineInfo> DialogueLines; // Metadata for all dialogues in the YAML

        [Header("Presentation")]
        [Tooltip("Default presentation mode applied to new lines created during YAML sync. Does not retroactively change existing lines.")]
        public ConversationPresentationMode DefaultPresentationMode = ConversationPresentationMode.AudioAndText;

        [Header("Audio")]
        [Tooltip("Optional. Assign an audio manifest to enable voice clip playback for this conversation.")]
        public WitWeaverAudioManifest AudioManifest;

        public TextAsset ConversationYaml;
        public bool AllowPersistentOverrides = true; // enable device-side hotfixes
        [Tooltip("Optional. Relative path without extension used to look up a post-ship text override at " +
                 "persistentDataPath/WitWeaver/Dialogue/<FilePath>.yml (or .yaml). " +
                 "Only used when Allow Persistent Overrides is enabled.")]
        public string FilePath;
#if UNITY_EDITOR
        [HideInInspector] public UnityEngine.Object SourceYaml; // .yaml or TextAsset
        [HideInInspector] public string SourceYamlAssetPath; // AssetDatabase path for auto-sync
        [HideInInspector] public UnityEngine.Object SourceSpreadsheetAsset; // .xlsx for spreadsheet-driven authoring
        [HideInInspector] public string SourceSpreadsheetAssetPath; // AssetDatabase path for spreadsheet auto-sync

        // Provenance of the current EmbeddedYaml sub-asset: which linked source produced it and
        // when. Written only through WitWeaverEmbedUtility; shown by the inspector's Dialogue
        // Source section and used for cross-source staleness warnings.
        [HideInInspector] public string EmbeddedFromSourcePath;
        [HideInInspector] public long EmbeddedAtTicks; // DateTime.UtcNow.Ticks at embed time
#endif
        [Tooltip("Define the unique key for the conversation.")]
        public string ConversationKey; // Add this field to hold the key

        // ----- GUID Identity -----

        [SerializeField, HideInInspector] private string _conversationGuid;

        /// <summary>
        /// Asset GUID of this conversation's editor-only node graph companion asset, when one
        /// exists. Managed by the graph editor tooling; empty for conversations without a graph.
        /// </summary>
        [HideInInspector] public string GraphAssetGuid;

        /// <summary>
        /// How this conversation is authored. In <see cref="ConversationAuthoringMode.Graph"/>
        /// mode the node graph is the sole editing surface: the inspector hides direct line
        /// editing and all changes flow through the graph's bake. Managed by the editor tooling.
        /// </summary>
        [HideInInspector] public ConversationAuthoringMode AuthoringMode = ConversationAuthoringMode.LinearList;

        /// <summary>
        /// Stable unique identifier for this conversation. Generated automatically on first access
        /// and persisted via <see cref="_conversationGuid"/>. Use for save-system keying.
        /// </summary>
        public string ConversationGuid
        {
            get
            {
                if (string.IsNullOrEmpty(_conversationGuid))
                    _conversationGuid = System.Guid.NewGuid().ToString();
                return _conversationGuid;
            }
        }

        /// <summary>
        /// Generates a new GUID for this asset. Use only when intentionally breaking
        /// save-data continuity (e.g. duplicating an asset that should be independent).
        /// </summary>
        public void RegenerateGuid()
        {
            _conversationGuid = System.Guid.NewGuid().ToString();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Returns the index of the dialogue line with the given stable <c>LineID</c>, or -1 when
        /// the id is empty or no line matches. Used for intra-conversation jumps and save resume.
        /// </summary>
        public int GetLineIndexById(string lineId)
        {
            if (DialogueLines == null || string.IsNullOrEmpty(lineId)) return -1;
            for (int i = 0; i < DialogueLines.Count; i++)
            {
                if (DialogueLines[i]?.LineID == lineId)
                    return i;
            }
            return -1;
        }

        /// <summary>Authoring surface for a conversation: the classic inspector line list, or the node graph.</summary>
        public enum ConversationAuthoringMode
        {
            LinearList = 0,
            Graph = 1
        }

        private Dictionary<string, List<DialogueYamlConfig>> _dialogueDataByKey; // Stored YAML data at runtime

        public WitWeaverConversationData()
        {
            WitWeaverYamlUtilities = new WitWeaverYamlUtilities(this);
        }

        public WitWeaverYamlUtilities WitWeaverYamlUtilities { get; }

        // OnValidate is called whenever the object is loaded or a value is changed in the inspector
        private void OnValidate()
        {
            ValidateAndFixDialogueLines();

            // Ensure every asset has a stable GUID
            if (string.IsNullOrEmpty(_conversationGuid))
                _conversationGuid = System.Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Validates and fixes dialogue line data to ensure proper serialization
        /// </summary>
        public void ValidateAndFixDialogueLines()
        {
            if (DialogueLines == null) return;

            bool verboseLogs = WitWeaverYamlLoader.Settings?.VerboseLogs ?? false;

            if (verboseLogs)
                Debug.Log($"=== Starting Dialogue Validation for {name} ===");

            bool madeChanges = false;

            for (int i = 0; i < DialogueLines.Count; i++)
            {
                var line = DialogueLines[i];
                if (line == null) continue;
                if (string.IsNullOrEmpty(line.LineID))
                {
                    Debug.LogError($"[Conversation file {name}] Dialogue Line ID {line.ConversationLineIndex} is empty. " +
                                   $"Re-embed the conversation on this conversation object and reimport the dialogue lines.",this);
                }
                line.EnsureCharacterRepresentationListInitialized();

                if (ValidatePrimaryCharacterRepresentation(line, i))
                {
                    madeChanges = true;
                }

                for (int r = 1; r < line.CharacterRepresentations.Count; r++)
                {
                    ValidateNonSpeakerRepresentation(line, r, i);
                }

                if (string.IsNullOrEmpty(line.ConversationID))
                {
                    line.ConversationID = ConversationKey;
                    madeChanges = true;
                }

                if (line.ConversationLineIndex != i)
                {
                    line.ConversationLineIndex = i;
                    madeChanges = true;
                }
            }

            if (madeChanges)
            {
                if (verboseLogs)
                    Debug.Log($"Validation completed with automatic fixes applied to {name}.");
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
            else
            {
                if (verboseLogs)
                    Debug.Log($"Validation completed - no changes needed for {name}.");
            }
        }

        private bool ValidatePrimaryCharacterRepresentation(DialogueLineInfo line, int lineIndex)
        {
            bool verboseLogs = WitWeaverYamlLoader.Settings?.VerboseLogs ?? false;

            if (verboseLogs)
                Debug.Log($"Validating line {lineIndex}: CharacterID='{line.characterID}'");

            if (string.IsNullOrEmpty(line.characterID))
            {
                if (verboseLogs)
                    Debug.LogWarning($"Line {lineIndex}: CharacterID is not set for the speaking character.");
                return false;
            }

            line.EnsureCharacterRepresentationListInitialized();

            var speakerProfile = ResolveCharacterProfile(ConversationParticipantProfiles, line.characterID);
            if (speakerProfile == null)
            {
                if (verboseLogs)
                    Debug.LogWarning($"Line {lineIndex}: No profile found for CharacterID '{line.characterID}'.");
                return false;
            }

            var speakerRep = line.CharacterRepresentations.Count > 0
                ? line.CharacterRepresentations[0]
                : new CharacterRepresentationData();

            if (verboseLogs)
            {
                Debug.Log(
                    $"Line {lineIndex}: Found profile '{speakerProfile.CharacterName}' with {speakerProfile.Representations?.Count ?? 0} representations");
                Debug.Log($"Line {lineIndex}: Current Speaker representation state:");
                Debug.Log($"SelectedRepresentationName: '{speakerRep.SelectedRepresentationName}'");
                Debug.Log(
                    $"SelectedRepresentation: {(speakerRep.SelectedRepresentation != null ? "NOT NULL" : "NULL")}");
                Debug.Log($"SelectedCharacterID: '{speakerRep.SelectedCharacterID}'");
            }

            bool needsAutoFix = string.IsNullOrEmpty(speakerRep.SelectedRepresentationID) &&
                                string.IsNullOrEmpty(speakerRep.SelectedRepresentationName) &&
                                speakerRep.SelectedRepresentation == null &&
                                string.IsNullOrEmpty(speakerRep.SelectedCharacterID);

            if (verboseLogs)
                Debug.Log($"Line {lineIndex}: NeedsAutoFix = {needsAutoFix}");

            if (needsAutoFix)
            {
                if (speakerProfile.Representations is { Count: > 0 })
                {
                    var firstRep = speakerProfile.Representations[0];

                    if (verboseLogs)
                        Debug.Log(
                            $"Line {lineIndex}: First representation found: CharacterRepresentationName='{firstRep.CharacterRepresentationName}', Object={(firstRep.CharacterRepresentationType != null ? "NOT NULL" : "NULL")}");

                    speakerRep.SelectedRepresentationID = firstRep.RepresentationID;
                    speakerRep.SelectedRepresentationName = firstRep.CharacterRepresentationName;
                    speakerRep.SelectedRepresentation = firstRep.CharacterRepresentationType;

                    line.CharacterRepresentations[0] = speakerRep;

                    if (verboseLogs)
                        Debug.Log(
                            $"Line {lineIndex}: Auto-assigned primary representation '{firstRep.CharacterRepresentationName}' for character '{speakerProfile.CharacterName}'.");

                    return true;
                }

                if (verboseLogs)
                    Debug.LogWarning(
                        $"Line {lineIndex}: Character '{speakerProfile.CharacterName}' has no available representations.");

                return false;
            }
            else
            {
                bool needsSync = false;

                if ((!string.IsNullOrEmpty(speakerRep.SelectedRepresentationID) ||
                     !string.IsNullOrEmpty(speakerRep.SelectedRepresentationName)) &&
                    speakerRep.SelectedRepresentation == null)
                {
                    bool changedRep = false;

                    // Upgrade a legacy display-name reference to the stable ID before resolving.
                    if (string.IsNullOrEmpty(speakerRep.SelectedRepresentationID) &&
                        speakerProfile.TryGetRepresentationIdByName(speakerRep.SelectedRepresentationName,
                            out var migratedId))
                    {
                        speakerRep.SelectedRepresentationID = migratedId;
                        changedRep = true;
                    }

                    if (speakerProfile.TryGetRepresentation(speakerRep.SelectedRepresentationID,
                            out var representation))
                    {
                        speakerRep.SelectedRepresentation = representation;
                        changedRep = true;

                        if (verboseLogs)
                            Debug.Log(
                                $"Line {lineIndex}: Synced object reference for representation '{speakerRep.SelectedRepresentationName}'.");
                    }
                    else
                    {
                        string requested = !string.IsNullOrEmpty(speakerRep.SelectedRepresentationID)
                            ? speakerRep.SelectedRepresentationID
                            : speakerRep.SelectedRepresentationName;
                        Debug.LogWarning(
                            $"Line {lineIndex}: Could not resolve representation '{requested}' in profile '{speakerProfile.CharacterName}'.",
                            this);
                    }

                    if (changedRep)
                    {
                        line.CharacterRepresentations[0] = speakerRep;
                        needsSync = true;
                    }
                }

                if (verboseLogs)
                    Debug.Log(
                        $"Line {lineIndex}: Primary representation appears to be already set, skipping auto-fix. Sync needed: {needsSync}");

                return needsSync;
            }
        }

        /// <summary>
        /// Forces synchronization of object references for all dialogue lines that have representation names but missing object references
        /// </summary>
        [ContextMenu("Sync All Representation Object References")]
        public void SyncAllRepresentationObjectReferences()
        {
            bool verboseLogs = WitWeaverYamlLoader.Settings?.VerboseLogs ?? false;

            if (verboseLogs)
                Debug.Log("=== Syncing All Representation Object References ===");

            bool madeChanges = false;

            if (DialogueLines == null) return;

            for (int i = 0; i < DialogueLines.Count; i++)
            {
                var line = DialogueLines[i];
                if (line == null) continue;

                line.EnsureCharacterRepresentationListInitialized();

                if (line.CharacterRepresentations == null || line.CharacterRepresentations.Count == 0)
                    continue;

                var speakerRep = line.CharacterRepresentations[0];
                if (SyncRepresentationObjectReference(ref speakerRep, line.characterID, i, "Speaker"))
                {
                    line.CharacterRepresentations[0] = speakerRep;
                    madeChanges = true;
                }

                for (int r = 1; r < line.CharacterRepresentations.Count; r++)
                {
                    var rep = line.CharacterRepresentations[r];

                    if (string.IsNullOrEmpty(rep.SelectedCharacterID))
                        continue;

                    if (SyncRepresentationObjectReference(ref rep, rep.SelectedCharacterID, i, $"Visible[{r}]"))
                    {
                        line.CharacterRepresentations[r] = rep;
                        madeChanges = true;
                    }
                }
            }

            if (madeChanges)
            {
                if (verboseLogs)
                    Debug.Log("Representation object reference sync completed with changes.");
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.AssetDatabase.SaveAssets();
#endif
            }
            else
            {
                if (verboseLogs)
                    Debug.Log("Representation object reference sync completed - no changes needed.");
            }
        }

        /// <summary>
        /// Helper method to sync a single representation object reference
        /// </summary>
        private bool SyncRepresentationObjectReference(ref CharacterRepresentationData representationData,
            string characterID, int lineIndex, string type)
        {
            if ((string.IsNullOrEmpty(representationData.SelectedRepresentationID) &&
                 string.IsNullOrEmpty(representationData.SelectedRepresentationName)) ||
                representationData.SelectedRepresentation != null)
            {
                return false;
            }

            bool verboseLogs = WitWeaverYamlLoader.Settings?.VerboseLogs ?? false;

            var profile = ResolveCharacterProfile(ConversationParticipantProfiles, characterID);
            if (profile == null)
            {
                if (verboseLogs)
                    Debug.LogWarning(
                        $"Line {lineIndex}: Cannot sync {type} representation - profile not found for CharacterID '{characterID}'.");
                return false;
            }

            bool changed = false;

            // Upgrade a legacy display-name reference to the stable ID before resolving.
            if (string.IsNullOrEmpty(representationData.SelectedRepresentationID) &&
                profile.TryGetRepresentationIdByName(representationData.SelectedRepresentationName, out var migratedId))
            {
                representationData.SelectedRepresentationID = migratedId;
                changed = true;
            }

            if (profile.TryGetRepresentation(representationData.SelectedRepresentationID, out var representation))
            {
                representationData.SelectedRepresentation = representation;
                if (verboseLogs)
                    Debug.Log(
                        $"Line {lineIndex}: Synced {type} representation object reference for '{representationData.SelectedRepresentationName}'.");
                return true;
            }

            string requested = !string.IsNullOrEmpty(representationData.SelectedRepresentationID)
                ? representationData.SelectedRepresentationID
                : representationData.SelectedRepresentationName;
            Debug.LogWarning(
                $"Line {lineIndex}: Could not find {type} representation '{requested}' in profile '{profile.CharacterName}'.",
                this);
            return changed;
        }


        private void ValidateNonSpeakerRepresentation(DialogueLineInfo line, int repIndex, int lineIndex)
        {
            bool verboseLogs = WitWeaverYamlLoader.Settings?.VerboseLogs ?? false;

            if (line.CharacterRepresentations == null) return;
            if (repIndex < 0 || repIndex >= line.CharacterRepresentations.Count) return;

            var rep = line.CharacterRepresentations[repIndex];

            if (string.IsNullOrEmpty(rep.SelectedCharacterID))
                return;

            var selectedProfile = ConversationParticipantProfiles
                .FirstOrDefault(p => p != null && p.CharacterID == rep.SelectedCharacterID);

            if (selectedProfile == null)
            {
                if (verboseLogs)
                    Debug.LogWarning(
                        $"Line {lineIndex}: Visible character [{repIndex}] references unknown CharacterID '{rep.SelectedCharacterID}'.");
                return;
            }

            if (string.IsNullOrEmpty(rep.SelectedRepresentationID) &&
                string.IsNullOrEmpty(rep.SelectedRepresentationName))
                return;

            bool resolved = !string.IsNullOrEmpty(rep.SelectedRepresentationID)
                ? selectedProfile.TryGetRepresentation(rep.SelectedRepresentationID, out _)
                : selectedProfile.TryGetRepresentationIdByName(rep.SelectedRepresentationName, out _);

            if (!resolved)
            {
                string requested = !string.IsNullOrEmpty(rep.SelectedRepresentationID)
                    ? rep.SelectedRepresentationID
                    : rep.SelectedRepresentationName;
                Debug.LogWarning(
                    $"Line {lineIndex}: Visible character [{repIndex}] representation '{requested}' not found in profile '{selectedProfile.CharacterName}'.",
                    this);
            }
        }


        /// <summary>
        /// Forces validation of dialogue lines (accessible from context menu)
        /// </summary>
        [ContextMenu("Force Validate Dialogue Lines")]
        public void ForceValidateDialogueLines()
        {
            bool verboseLogs = WitWeaverYamlLoader.Settings?.VerboseLogs ?? false;

            if (verboseLogs)
                Debug.Log("Manually triggering dialogue line validation...");

            ValidateAndFixDialogueLines();
#if UNITY_EDITOR
            ValidateChoiceLabels();
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only authoring check for player choice labels. Reports labels that are missing
        /// for a language listed in WitWeaver settings, choices with no labels at all, and label
        /// entries whose language is no longer listed there.
        ///
        /// Nothing is modified: orphaned entries are only reported, never removed.
        /// </summary>
        private void ValidateChoiceLabels()
        {
            if (DialogueLines == null) return;

            var supportedLanguages = WitWeaverSettings.Instance?.SupportedLanguages;

            foreach (var line in DialogueLines)
            {
                if (line == null) continue;
                if (line.LineContinuationSettings.Mode != LineContinuationMode.PlayerChoice) continue;

                var choices = line.LineContinuationSettings.Choices;
                if (choices == null) continue;

                for (int choiceIndex = 0; choiceIndex < choices.Count; choiceIndex++)
                {
                    var labels = choices[choiceIndex].Labels;

                    // A choice with no labels at all always resolves to "[Choice]" at runtime,
                    // which is never intentional.
                    if (labels == null || labels.Count == 0)
                    {
                        Debug.LogWarning(
                            $"[WitWeaver] Conversation '{ConversationKey}' line '{line.LineID}' " +
                            $"choice {choiceIndex}: no labels defined. Runtime will show '[Choice]'.",
                            this);
                        continue;
                    }

                    if (supportedLanguages != null)
                    {
                        foreach (var lang in supportedLanguages)
                        {
                            if (string.IsNullOrWhiteSpace(lang)) continue;

                            bool hasText = false;
                            foreach (var label in labels)
                            {
                                if (!string.Equals(label.Language, lang, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                hasText = !string.IsNullOrWhiteSpace(label.Text);
                                break;
                            }

                            if (!hasText)
                            {
                                Debug.LogWarning(
                                    $"[WitWeaver] Conversation '{ConversationKey}' line '{line.LineID}' " +
                                    $"choice {choiceIndex}: missing '{lang}' label. Runtime will fall back.",
                                    this);
                            }
                        }
                    }

                    // Orphans are harmless at runtime, so these are informational only.
                    foreach (var label in labels)
                    {
                        if (string.IsNullOrWhiteSpace(label.Language)) continue;

                        bool supported = false;
                        if (supportedLanguages != null)
                        {
                            foreach (var lang in supportedLanguages)
                            {
                                if (string.Equals(label.Language, lang, StringComparison.OrdinalIgnoreCase))
                                {
                                    supported = true;
                                    break;
                                }
                            }
                        }

                        if (!supported)
                        {
                            Debug.Log(
                                $"[WitWeaver] Conversation '{ConversationKey}' line '{line.LineID}' " +
                                $"choice {choiceIndex}: orphaned '{label.Language}' label " +
                                "(language not listed in WitWeaver settings).",
                                this);
                        }
                    }
                }
            }
        }
#endif

        /// <summary>
        /// Debug method to inspect character profiles structure
        /// </summary>
        [ContextMenu("Debug Character Profiles")]
        public void DebugCharacterProfiles()
        {
            Debug.Log("=== Character Profiles Debug ===");
            foreach (var profile in ConversationParticipantProfiles)
            {
                if (profile == null)
                {
                    Debug.Log("NULL PROFILE FOUND");
                    continue;
                }

                Debug.Log($"Profile: {profile.CharacterName} (ID: {profile.CharacterID})");
                Debug.Log($"  Representations count: {profile.Representations?.Count ?? 0}");

                if (profile.Representations != null)
                {
                    for (int i = 0; i < profile.Representations.Count; i++)
                    {
                        var rep = profile.Representations[i];
                        Debug.Log($"[{i}] RepresentationName: '{rep.CharacterRepresentationName}'");
                        Debug.Log($"[{i}] CharacterRepresentationName: '{rep.CharacterRepresentationName}'");
                        Debug.Log(
                            $"[{i}] CharacterRepresentation: {(rep.CharacterRepresentationType != null ? rep.CharacterRepresentationType.GetType().Name : "NULL")}");
                    }
                }
            }
        }

        public WitWeaverCharacterProfileBaseData ResolveCharacterProfile(
            List<WitWeaverCharacterProfileBaseData> profiles, string characterID)
        {
            if (profiles == null || string.IsNullOrEmpty(characterID))
            {
                bool verboseLogs = WitWeaverYamlLoader.Settings?.VerboseLogs ?? false;
                if (verboseLogs)
                    Debug.LogWarning("CharacterID is missing or no profiles are available.");
                return null;
            }

            foreach (var profile in profiles)
            {
                if (profile == null)
                {
                    continue;
                }

                if (profile.CharacterID == characterID)
                {
                    return profile;
                }
            }

            bool verboseLogsEnabled = WitWeaverYamlLoader.Settings?.VerboseLogs ?? false;
            if (verboseLogsEnabled)
                Debug.LogWarning($"Profile not found for CharacterID: {characterID}");
            return null; // Profile not found
        }

        /// <summary>
        /// Single runtime resolution path from a line's representation entry to a representation
        /// asset. Used by the runner, the UI foundation's expression-action pass, and the sample
        /// UIs; custom UIs should call this instead of hand-rolling resolution.
        ///
        /// <see cref="RepresentationRole.Speaker"/> never resolves to null while the profile has
        /// any representation: an empty selection auto-assigns the first entry with a warning,
        /// and a stale ID warns once per session and substitutes the first entry.
        /// <see cref="RepresentationRole.Visible"/> treats an empty selection as a legal "None"
        /// and returns null silently.
        ///
        /// Editor validation and inspectors intentionally do not use this path — they check the
        /// authored data via the quiet profile primitives
        /// (<see cref="WitWeaverCharacterProfileBaseData.TryGetRepresentation"/> and friends).
        /// </summary>
        /// <param name="data">The line's representation slot entry.</param>
        /// <param name="fallbackCharacterId">CharacterID used to resolve the profile when the
        /// entry does not target an explicit participant — pass <c>line.characterID</c> for the
        /// speaker slot, null otherwise.</param>
        /// <param name="role">Speaker or Visible slot semantics (see summary).</param>
        public CharacterRepresentationBase ResolveRepresentation(
            in CharacterRepresentationData data,
            string fallbackCharacterId,
            RepresentationRole role)
        {
            // 1. Profile: an explicit participant target wins, else the caller's fallback.
            WitWeaverCharacterProfileBaseData profile;
            if (!string.IsNullOrEmpty(data.SelectedCharacterID))
            {
                profile = ResolveCharacterProfile(ConversationParticipantProfiles, data.SelectedCharacterID);
                if (profile == null)
                {
                    Debug.LogWarning($"Profile with CharacterID '{data.SelectedCharacterID}' not found.", this);
                    return null;
                }
            }
            else
            {
                profile = ResolveCharacterProfile(ConversationParticipantProfiles, fallbackCharacterId);
            }

            // 2. No profile: the entry's direct object reference is the only thing left to honor.
            if (profile == null)
            {
                if (data.SelectedRepresentation != null)
                {
                    if (role == RepresentationRole.Speaker)
                        Debug.LogWarning(
                            $"No profile resolved for CharacterID '{fallbackCharacterId}'; using the line's direct representation reference.",
                            this);
                    return data.SelectedRepresentation;
                }

                if (role == RepresentationRole.Speaker)
                    Debug.LogError(
                        $"Cannot resolve a profile for CharacterID '{fallbackCharacterId}'; the speaker has no representation.",
                        this);
                return null;
            }

            // 3. Fully empty selection: legal "None" for visible slots; auto-assign for the speaker.
            if (string.IsNullOrEmpty(data.SelectedRepresentationID) &&
                string.IsNullOrEmpty(data.SelectedRepresentationName) &&
                data.SelectedRepresentation == null)
            {
                if (role == RepresentationRole.Visible)
                    return null;

                if (profile.Representations is { Count: > 0 })
                {
                    var first = profile.Representations[0];
                    if (first?.CharacterRepresentationType != null)
                    {
                        Debug.LogWarning(
                            $"Auto-assigning first available representation '{first.CharacterRepresentationName}' from profile '{profile.CharacterName}' for the speaker.",
                            profile);
                        return first.CharacterRepresentationType;
                    }
                }

                Debug.LogError(
                    $"Speaker '{profile.CharacterName}' has no available representations. Ensure the character profile has at least one representation assigned.",
                    profile);
                return null;
            }

            // 4. Object-reference-only entry (legacy data with no ID or name).
            string identifier = !string.IsNullOrEmpty(data.SelectedRepresentationID)
                ? data.SelectedRepresentationID
                : data.SelectedRepresentationName;
            if (string.IsNullOrEmpty(identifier))
                return data.SelectedRepresentation;

            // 5. Stable ID (or legacy display name for unmigrated data). GetRepresentation owns
            //    all miss handling: warn-once per id per session + first-entry substitution.
            var representation = profile.GetRepresentation(identifier);
            if (representation != null)
                return representation;

            // 6. GetRepresentation is null only when the profile's list is empty (it already
            //    logged that error) — honor the entry's direct reference if one exists.
            return data.SelectedRepresentation;
        }

        /// <summary>
        /// Resolves the speaker slot (index 0) of a line via <see cref="ResolveRepresentation"/>.
        /// </summary>
        public CharacterRepresentationBase ResolveSpeakerRepresentation(DialogueLineInfo line)
        {
            if (line == null)
                return null;

            line.EnsureCharacterRepresentationListInitialized();
            var speakerData = line.CharacterRepresentations.Count > 0
                ? line.CharacterRepresentations[0]
                : default;

            return ResolveRepresentation(in speakerData, line.characterID, RepresentationRole.Speaker);
        }

        /// <summary>
        /// Initializes the YAML runtime data.
        /// Should be called before trying to fetch runtime dialogue text.
        /// </summary>
        public void InitializeDialogueData()
        {
            string yamlData = WitWeaverYamlLoader.Load(this);
            if (string.IsNullOrEmpty(yamlData))
            {
                Debug.LogError(
                    $"YAML not found. Checked the embedded/assigned TextAsset and the persistent override at persistentDataPath/WitWeaver/Dialogue/{FilePath}.yml.");
                return;
            }

            try
            {
                _dialogueDataByKey = WitWeaverYamlParser.Parse(yamlData);

                if (WitWeaverYamlLoader.Settings?.VerboseLogs == true)
                    Debug.Log(
                        $"Successfully loaded YAML data. Found {_dialogueDataByKey.Count} conversation sections.");

                for (int i = 0; i < DialogueLines.Count; i++)
                {
                    var currentLine = DialogueLines[i];
                    if (currentLine == null) continue;

                    if (!_dialogueDataByKey.TryGetValue(currentLine.ConversationID, out var configList) ||
                        configList == null)
                    {
                        Debug.LogWarning($"No config list found for ConversationID: '{currentLine.ConversationID}'");
                        continue;
                    }

                    DialogueYamlConfig matchingConfig = null;

                    // Prefer matching by stable LineID
                    if (!string.IsNullOrEmpty(currentLine.LineID))
                    {
                        for (int c = 0; c < configList.Count; c++)
                        {
                            var cfg = configList[c];
                            if (cfg != null && cfg.LineID == currentLine.LineID)
                            {
                                matchingConfig = cfg;
                                break;
                            }
                        }
                    }
                    // Fallback for legacy assets whose lines have no LineID at all. A line WITH
                    // a LineID that is absent from the YAML was deleted there; matching it by
                    // index would hand it the next line's text, so it keeps its serialized text
                    // until a structural import removes it.
                    else
                    {
                        matchingConfig = (currentLine.ConversationLineIndex >= 0 &&
                                          currentLine.ConversationLineIndex < configList.Count)
                            ? configList[currentLine.ConversationLineIndex]
                            : null;
                    }

                    if (matchingConfig?.LocalizedDialogue == null)
                    {
                        if (WitWeaverYamlLoader.Settings?.VerboseLogs == true)
                        {
                            Debug.LogWarning(
                                $"No matching config found for LineID='{currentLine.LineID}' (fallback index {currentLine.ConversationLineIndex}) " +
                                $"for conversation '{currentLine.ConversationID}'.");
                        }

                        continue;
                    }

                    var localizedDialogueList = new List<LocalizedDialogue>();
                    foreach (var kvp in matchingConfig.LocalizedDialogue)
                    {
                        localizedDialogueList.Add(new LocalizedDialogue
                        {
                            Language = kvp.Key,
                            Text = kvp.Value
                        });
                    }

                    // DialogueLineInfo is a class, so just assign directly
                    currentLine.LocalizedDialogues = localizedDialogueList;

                    if (WitWeaverYamlLoader.Settings?.VerboseLogs == true)
                        Debug.Log($"Updated line {i} with {localizedDialogueList.Count} translations");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize dialogue data: {ex.Message}\n{ex.StackTrace}");
                _dialogueDataByKey = null;
            }

            foreach (var profile in ConversationParticipantProfiles)
            {
                foreach (var representationPair in profile.Representations)
                {
                    if (representationPair == null)
                    {
                        Debug.LogError($"Representation pair on profile: {profile.name} is null.", profile);
                        continue;
                    }

                    if (representationPair.CharacterRepresentationType == null)
                    {
                        Debug.LogError(
                            $"Representation pair on profile: {profile.name} has no CharacterRepresentationType set.",
                            profile);
                        continue;
                    }

                    if (representationPair.CharacterRepresentationType is IWitWeaverRepresentationInitializable
                        initializable)
                        initializable.Initialize();
                }
            }
        }

        // Finds the player's profile from the list based on the IsPlayer flag.
        public WitWeaverCharacterProfileBaseData GetPlayerProfile()
        {
            return ConversationParticipantProfiles.FirstOrDefault(profile => profile.IsPlayerCharacter);
        }

        public IEnumerator ActionsBeforeDialogueLine(WitWeaver core, DialogueLineInfo lineInfo,
            List<BaseDialogueLineAction> capture)
        {
            foreach (var action in lineInfo.ActionsBeforeDialogueLine)
            {
                if (action == null)
                {
                    Debug.LogError("Line " + lineInfo.ConversationLineIndex + " has null action");
                    continue;
                }

                if (!core.ShouldExecuteAction(action, lineInfo.ConversationLineIndex))
                {
                    continue;
                }

                var instance = Instantiate(action);
                capture?.Add(instance);
                yield return core.StartCoroutine(instance.ExecuteLineAction());

                // only destroy if we are not capturing for reverse
                if (capture == null) DestroyImmediate(instance);
            }
        }

        public IEnumerator DoActionsAfterDialogueLine(WitWeaver core, DialogueLineInfo lineInfo,
            List<BaseDialogueLineAction> capture)
        {
            foreach (var action in lineInfo.ActionsAfterDialogueLine)
            {
                if (action == null)
                {
                    Debug.LogError("Line " + lineInfo.ConversationLineIndex + " has null action");
                    continue;
                }

                if (!core.ShouldExecuteAction(action, lineInfo.ConversationLineIndex))
                {
                    continue;
                }

                var instance = Instantiate(action);
                capture?.Add(instance);
                yield return core.StartCoroutine(instance.ExecuteLineAction());

                if (capture == null) DestroyImmediate(instance);
            }
        }

    }

}