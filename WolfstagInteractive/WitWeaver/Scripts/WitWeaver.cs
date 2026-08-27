using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Main conversation runner MonoBehaviour. Attach to a GameObject, assign a
    /// <see cref="WitWeaverUIFoundation"/> subclass to <c>ConversationUI</c>, then call
    /// <see cref="StartConversation"/> to begin playback. Subscribe to
    /// <see cref="CompletedConversation"/>, <see cref="StartedConversation"/>,
    /// <see cref="EndedConversation"/>, and <see cref="PausedConversation"/> to respond to
    /// conversation lifecycle events.
    /// </summary>
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaver.html")]
    public class WitWeaver : MonoBehaviour,IWitWeaverRunner
    {

         private WitWeaverConversationData ConversationData;
        public WitWeaverConversationData GetCurrentConversationData() => ConversationData;
        [Header("Conversation Settings")]
        [SerializeReference] public IWitWeaverInput Input = new SingleConversationInput();

        [Header("Conversation UI")]
        public WitWeaverUIFoundation ConversationUI;

        [Header("Audio")]
        [Tooltip("Optional. Assign a WitWeaverUnityAudioProvider or any MonoBehaviour implementing IWitWeaverAudioProvider. If unassigned, audio playback is skipped.")]
        [SerializeField] private MonoBehaviour _audioProviderObject;

        private IWitWeaverAudioProvider _audioProvider;

        /// <summary>
        /// Used when <see cref="WitWeaverSettings.GoBackLabel"/> resolves to nothing, so the option
        /// is still readable in a project whose settings asset has not been filled in.
        /// </summary>
        private const string k_DefaultGoBackLabel = "← Go Back";

        [Header("Debug")]
        [Tooltip("When enabled, each dialogue line is printed to the Console. Click a log entry to highlight this runner in the Hierarchy.")]
        [SerializeField] private bool _debugLogLines;
        
        public ConversationState CurrentDialogueState { get; private set; } = ConversationState.Inactive;
        
        private int _currentLineIndex = 0;
        private WitWeaverDialogueLocalizationHandler LocalizationHandler;
        public event Action StartedConversation;
        public event Action PausedConversation;
        public event Action EndedConversation;
        public event Action CompletedConversation;

        // Save-system hooks for the save manager
        public event Action         OnConversationStarted;
        public event Action         OnConversationEnded;
        public event Action<string> OnLineStarted;
        public event Action<string> OnLineCompleted;
        public event Action<int>    OnChoiceMade;

        // Visited-line tracking (runtime only)
        [NonSerialized] private readonly HashSet<string> _visitedLineIds = new HashSet<string>();
        private readonly Dictionary<BaseDialogueLineAction, HashSet<int>> _executedActionsPerLine =
            new Dictionary<BaseDialogueLineAction, HashSet<int>>();
        private bool _reverseRequested;
        private bool _advanceRequested;
        public enum ConversationState
        {
            Inactive,
            Active,
            Paused,
            Completed
        }
        private readonly Stack<(WitWeaverConversationData convo, int index)> _returnStack =
            new Stack<(WitWeaverConversationData, int)>();

        private readonly IConversationContext _context = DefaultConversationContext.Instance;

        // ----- Visited-line API -----

        /// <summary>Replaces the current visited-line set with the provided list.</summary>
        public void SetVisitedLines(List<string> lineIds)
        {
            _visitedLineIds.Clear();
            if (lineIds == null) return;
            for (int i = 0; i < lineIds.Count; i++)
                _visitedLineIds.Add(lineIds[i]);
        }

        /// <summary>Adds all provided line IDs to the existing visited-line set.</summary>
        internal void ApplyVisitedLines(List<string> lineIds)
        {
            if (lineIds == null) return;
            for (int i = 0; i < lineIds.Count; i++)
                _visitedLineIds.Add(lineIds[i]);
        }

        /// <summary>Sets the current line index to the line matching <paramref name="lineId"/>.</summary>
        internal void BeginFromLine(string lineId)
        {
            if (ConversationData == null || string.IsNullOrEmpty(lineId)) return;
            int index = ConversationData.GetLineIndexById(lineId);
            if (index >= 0)
            {
                _currentLineIndex = index;
                return;
            }
            Debug.LogWarning($"[WitWeaver] BeginFromLine: LineID '{lineId}' not found. Starting from beginning.");
        }
        /// <summary>
        /// Represents a frame of dialogue within a conversation sequence.
        /// </summary>
        /// <remarks>
        /// The LineFrame class is utilized to track the current state and actions
        /// associated with a specific line during dialogue execution. It is used internally
        /// within the WitWeaver class to manage the transition of dialogue lines and their
        /// corresponding actions in the event that actions must be reversed
        /// </remarks>
        private sealed class LineFrame
        {
            public int LineIndex;
            public readonly List<BaseDialogueLineAction> Before = new();
            public readonly List<BaseDialogueLineAction> After = new();
        }
        public bool CanReverseOneLine =>
            CurrentDialogueState == ConversationState.Active && _currentLineIndex > 0 && _currentLineIndex <= ConversationData.DialogueLines.Count - 1;

        
        private readonly List<LineFrame> _history = new();
        private void Awake()
        {
            LocalizationHandler = new WitWeaverDialogueLocalizationHandler(WitWeaverLanguageManager.Instance);

            if (_audioProviderObject is IWitWeaverAudioProvider provider)
                _audioProvider = provider;
            else if (_audioProviderObject != null)
                Debug.LogWarning($"[WitWeaver] Audio Provider Object assigned on '{name}' does not implement IWitWeaverAudioProvider. Audio will be skipped.", this);
        }

        /// <summary>
        /// Inject a custom audio provider at runtime. Use this for middleware integrations
        /// that instantiate their provider via code rather than the inspector.
        /// </summary>
        public void SetAudioProvider(IWitWeaverAudioProvider provider) => _audioProvider = provider;

        /// <summary>
        /// Main coroutine that handles the conversation flow
        /// </summary>
        private IEnumerator ExecuteDialogueSequence()
        {
            while (_currentLineIndex < ConversationData.DialogueLines.Count && CurrentDialogueState == ConversationState.Active)
            {
                var line = ConversationData.DialogueLines[_currentLineIndex];
                var lineId = line.LineID;
                _visitedLineIds.Add(lineId ?? string.Empty);
                OnLineStarted?.Invoke(lineId);

                // Resolve the primary character profile
                var primaryProfile = ConversationData.ResolveCharacterProfile(
                    ConversationData.ConversationParticipantProfiles, 
                    line.characterID);

                if (primaryProfile == null)
                {
                    Debug.LogError($"Cannot resolve character profile for ID: '{line.characterID}'. Skipping line {_currentLineIndex}.");
                    _currentLineIndex++;
                    continue;
                }

                // Get the primary character representation
                line.EnsureCharacterRepresentationListInitialized();
                var speakerRepData = (line.CharacterRepresentations != null && line.CharacterRepresentations.Count > 0)
                    ? line.CharacterRepresentations[0]
                    : default;

                var primaryRepresentation = GetPrimaryCharacterRepresentation(primaryProfile, speakerRepData);

                if (primaryRepresentation == null)
                {
                    if (primaryProfile && string.IsNullOrEmpty(primaryProfile.CharacterName) ||
                        string.IsNullOrWhiteSpace(primaryProfile.CharacterName))
                    {
                        Debug.LogError($"Cannot resolve primary character representation for character representation asset'{primaryProfile.name}'. " +
                                       $"Line index: {_currentLineIndex}. " +
                                       $"CharacterID: '{line.characterID}'. " +
                                       $"Primary characters must have a valid representation. Skipping line.",primaryProfile);
                    }
                    else
                    {
                        Debug.LogError($"Cannot resolve primary character representation for '{primaryProfile.CharacterName}'. " +
                                       $"Line index: {_currentLineIndex}. " +
                                       $"CharacterID: '{line.characterID}'. " +
                                       $"Primary characters must have a valid representation. Skipping line.",primaryProfile);
                    }
                    
                    _currentLineIndex++;
                    continue;
                }

                // Get localized dialogue
                var localizedResult = LocalizationHandler.GetLocalizedDialogue(line);
                if (!localizedResult.Success)
                {
                    Debug.LogError(localizedResult.ErrorMessage);
                    _currentLineIndex++;
                    continue;
                }
                else if (localizedResult.IsFallback)
                {
                    Debug.LogWarning(localizedResult.ErrorMessage);
                }

                var frame = new LineFrame { LineIndex = _currentLineIndex };

                // before line actions
                if (line.ActionsBeforeDialogueLine is { Count: > 0 })
                {
                    yield return StartCoroutine(ConversationData.ActionsBeforeDialogueLine(this, line, frame.Before));
                }

                // Resolve presentation flags
                bool showText  = ConversationData.ShouldDisplayText(line);
                bool playAudio = ConversationData.ShouldPlayAudio(line);

                // Resolve audio reference
                WitWeaverAudioReference audioRef = null;
                WitWeaverAudioReference inlineRef = null; // transient SO destroyed after progression
                if (playAudio && ConversationData.AudioManifest != null)
                {
                    var backend = ConversationData.AudioManifest.Backend;
                    if (backend == AudioBackend.UnityAudioSource)
                    {
                        // Try shared WitWeaverAudioReference first (e.g. one asset shared across lines)
                        audioRef = ConversationData.AudioManifest.Resolve(line.LineID, localizedResult.UsedLanguage);

                        // Then try direct AudioClip on the manifest entry
                        if (audioRef == null)
                        {
                            var clip = ConversationData.AudioManifest.ResolveClip(line.LineID, localizedResult.UsedLanguage)
                                       ?? localizedResult.ResolvedClip;
                            if (clip != null)
                            {
                                var unityRef = ScriptableObject.CreateInstance<WitWeaverUnityAudioReference>();
                                unityRef.Clip = clip;
                                inlineRef = unityRef;
                                audioRef  = unityRef;
                            }
                        }
                    }
                    else
                    {
                        // FMOD / Wwise / Custom: try a WitWeaverAudioReference SO first (user may have assigned one)
                        audioRef = ConversationData.AudioManifest.Resolve(line.LineID, localizedResult.UsedLanguage);

                        // Fall back to EventKey string wrapped in a transient reference
                        if (audioRef == null)
                        {
                            var key = ConversationData.AudioManifest.ResolveEventKey(line.LineID, localizedResult.UsedLanguage);
                            if (!string.IsNullOrEmpty(key))
                            {
                                var keyRef = ScriptableObject.CreateInstance<WitWeaverAudioEventKeyReference>();
                                keyRef.EventKey = key;
                                inlineRef = keyRef;
                                audioRef  = keyRef;
                            }
                        }

                        if (_audioProvider == null)
                            Debug.LogWarning($"[WitWeaver] AudioManifest backend is '{backend}' but no Audio Provider is assigned on '{name}'. Assign an IWitWeaverAudioProvider component.", this);
                    }
                }
                else if (playAudio && localizedResult.ResolvedClip != null)
                {
                    // No manifest — fall back to clip embedded in LocalizedDialogue (Unity-only path)
                    var unityRef = ScriptableObject.CreateInstance<WitWeaverUnityAudioReference>();
                    unityRef.Clip = localizedResult.ResolvedClip;
                    inlineRef = unityRef;
                    audioRef  = unityRef;
                }

                // Play audio
                if (playAudio && audioRef != null && _audioProvider != null)
                    _audioProvider.PlayVoiceLine(line, audioRef);

                // Display text
                if (showText)
                {
                    string finalOutputString = ReplacePlayerNameInDialogueLine(localizedResult.Text);

                    if (_debugLogLines)
                        Debug.Log($"[WitWeaver] Line {_currentLineIndex} — {primaryProfile.CharacterName}: \"{finalOutputString}\"", this);

                    yield return StartCoroutine(
                        PlayDialogueLine(
                            ConversationUI,
                            line,
                            finalOutputString,
                            primaryProfile.CharacterName,
                            primaryRepresentation, primaryProfile
                        )
                    );
                }
                else
                {
                    if (_debugLogLines)
                        Debug.Log($"[WitWeaver] Line {_currentLineIndex} — {primaryProfile.CharacterName}: [AudioOnly]", this);

                    ConversationUI?.HideDialogue();
                }

                // Push the frame now (before progression) so a reverse request can locate it.
                // frame.After is populated later, once the line has actually been presented.
                if (_history.Count == frame.LineIndex)
                    _history.Add(frame);
                else if (_history.Count > frame.LineIndex)
                    _history[frame.LineIndex] = frame;

                // PlayerChoice mode: present options, branch on selection, skip normal input/continuation.
                if (line.LineContinuationSettings.Mode == WitWeaverConversationData.LineContinuationMode.PlayerChoice)
                {
                    var choices    = line.LineContinuationSettings.Choices;
                    bool allowBack = line.LineContinuationSettings.AllowGoBack;
                    int choiceCount = choices?.Count ?? 0;

                    if (choiceCount == 0 && !allowBack)
                    {
                        Debug.LogWarning($"[WitWeaver] Line {_currentLineIndex} has PlayerChoice mode but no choices defined. Advancing.");
                        _currentLineIndex++;
                        continue;
                    }

                    var labels = choiceCount > 0
                        ? ResolveChoiceLabels(choices)
                        : new System.Collections.Generic.List<string>();

                    // Append a "Go Back" entry only when there is a previous line to return to.
                    bool goBackAvailable = allowBack && _currentLineIndex > 0;
                    if (goBackAvailable)
                        labels.Add(ResolveLocalizedList(
                            WitWeaverSettings.Instance?.GoBackLabel, k_DefaultGoBackLabel));

                    if (labels.Count == 0)
                    {
                        _currentLineIndex++;
                        continue;
                    }

                    var choiceResult = new ChoiceResult();
                    yield return StartCoroutine(ConversationUI.PresentChoices(
                        choices ?? new System.Collections.Generic.List<WitWeaverConversationData.ChoiceOption>(),
                        labels,
                        choiceResult));

                    int selected = Mathf.Clamp(choiceResult.SelectedIndex, 0, labels.Count - 1);

                    // "Go Back" is always the last entry (index == choiceCount).
                    if (goBackAvailable && selected >= choiceCount)
                    {
                        _currentLineIndex = Mathf.Max(0, _currentLineIndex - 1);
                        continue;
                    }

                    if (choiceCount == 0)
                    {
                        _currentLineIndex++;
                        continue;
                    }

                    selected = Mathf.Clamp(selected, 0, choiceCount - 1);

                    // Line is fully presented once the player has made a selection —
                    // run the after-line actions before branching.
                    if (line.ActionsAfterDialogueLine is { Count: > 0 })
                        yield return StartCoroutine(ConversationData.DoActionsAfterDialogueLine(this, line, frame.After));

                    OnChoiceMade?.Invoke(selected);
                    if (!HandleChoiceBranch(choices[selected]))
                        break;

                    continue;
                }

                // Determine effective progression
                var effectiveProgression = line.UserInputMethod;

                // Auto-coerce UserInput to AudioComplete on AudioOnly lines —
                // a stalled conversation with no UI and no automatic advance is a silent failure mode
                if (!showText && effectiveProgression == WitWeaverConversationData.DialogueLineProgressionMethod.UserInput)
                {
                    effectiveProgression = (playAudio && _audioProvider != null)
                        ? WitWeaverConversationData.DialogueLineProgressionMethod.AudioComplete
                        : WitWeaverConversationData.DialogueLineProgressionMethod.Timed; // advances immediately (TimeBeforeNextLine = 0)
                }

                // Handle progression
                switch (effectiveProgression)
                {
                    case WitWeaverConversationData.DialogueLineProgressionMethod.AudioComplete:
                        yield return StartCoroutine(WaitForAudioComplete());
                        break;

                    case WitWeaverConversationData.DialogueLineProgressionMethod.Timed:
                        if (line.TimeBeforeNextLine > 0f)
                            yield return new WaitForSeconds(line.TimeBeforeNextLine);
                        break;

                    case WitWeaverConversationData.DialogueLineProgressionMethod.UserInput:
                    default:
                        _advanceRequested = false;
                        _reverseRequested = false;

                        yield return StartCoroutine(ConversationUI.WaitForUserInput());

                        if (_reverseRequested)
                        {
                            // Destroy any temporary inline audio reference before reversing
                            if (inlineRef != null) Destroy(inlineRef);
                            // undo current line and move back one
                            yield return StartCoroutine(ReverseOneLineRoutine());
                            continue; // restart loop, current index now points at previous line
                        }
                        break;
                }

                // Destroy temporary inline reference after line progression completes
                if (inlineRef != null) Destroy(inlineRef);

                // The line is now fully presented and the user has advanced —
                // run the after-line actions before applying continuation rules.
                if (line.ActionsAfterDialogueLine is { Count: > 0 })
                    yield return StartCoroutine(ConversationData.DoActionsAfterDialogueLine(this, line, frame.After));

                // At this point the line is fully completed. Apply continuation rules.
                if (!HandleLineContinuation(line))
                {
                    // Conversation ended or could not continue
                    break;
                }
                OnLineCompleted?.Invoke(lineId);
            }

            // End conversation
            CurrentDialogueState = ConversationState.Completed;
            OnConversationEnded?.Invoke();
            CompletedConversation?.Invoke();
            if (ConversationUI != null)
            {
                ConversationUI.HideDialogue();
            }
            Debug.Log("Conversation completed!");
        }
        private List<string> ResolveChoiceLabels(List<WitWeaverConversationData.ChoiceOption> choices)
        {
            var labels = new List<string>(choices.Count);
            foreach (var choice in choices)
                labels.Add(ResolveLocalizedList(choice.Labels, "[Choice]"));
            return labels;
        }

        /// <summary>
        /// Resolves a standalone list of localized entries (a choice label or the Go Back label)
        /// through the same fallback chain used for dialogue lines.
        /// </summary>
        /// <param name="entries">Localized entries to resolve. May be null or empty.</param>
        /// <param name="fallback">Returned when nothing resolves to usable text.</param>
        private string ResolveLocalizedList(List<WitWeaverConversationData.LocalizedDialogue> entries,
            string fallback)
        {
            // A settings asset that predates this field deserializes the list empty, so this is a
            // silent fallback rather than a warning.
            if (entries == null || entries.Count == 0) return fallback;
            if (LocalizationHandler == null) return fallback;

            // Reuse the localization handler by constructing a temporary line info
            var tempLine = new WitWeaverConversationData.DialogueLineInfo("choice")
            {
                LocalizedDialogues = entries
            };
            var result = LocalizationHandler.GetLocalizedDialogue(tempLine);
            return result.Success && !string.IsNullOrEmpty(result.Text) ? result.Text : fallback;
        }

        private bool HandleChoiceBranch(WitWeaverConversationData.ChoiceOption choice)
        {
            // Intra-conversation jump takes priority over container branching.
            if (!string.IsNullOrEmpty(choice.TargetLineID))
                return JumpToLineId(choice.TargetLineID, choice.PushReturnPoint);

            if (choice.TargetContainer == null)
            {
                Debug.LogWarning("[WitWeaver] Selected choice has no TargetContainer. Ending conversation.");
                CurrentDialogueState = ConversationState.Completed;
                return false;
            }

            // Reuse the existing container branch logic
            var continuation = new WitWeaverConversationData.LineContinuation
            {
                Mode = WitWeaverConversationData.LineContinuationMode.ContainerBranch,
                TargetContainer = choice.TargetContainer,
                TargetAliasOrName = choice.TargetAliasOrName,
                PushReturnPoint = choice.PushReturnPoint
            };

            return HandleContainerBranch(continuation);
        }

        private bool HandleLineContinuation(WitWeaverConversationData.DialogueLineInfo line)
        {
            var cont = line.LineContinuationSettings;

            switch (cont.Mode)
            {
                case WitWeaverConversationData.LineContinuationMode.Continue:
                    _currentLineIndex++;
                    return _currentLineIndex < ConversationData.DialogueLines.Count;

                case WitWeaverConversationData.LineContinuationMode.EndConversation:
                    CurrentDialogueState = ConversationState.Completed;
                    return false;

                case WitWeaverConversationData.LineContinuationMode.ContainerBranch:
                    return HandleContainerBranch(cont);

                case WitWeaverConversationData.LineContinuationMode.GoToLine:
                    return JumpToLineId(cont.TargetLineID, cont.PushReturnPoint);

                default:
                    _currentLineIndex++;
                    return _currentLineIndex < ConversationData.DialogueLines.Count;
            }
        }

        /// <summary>
        /// Jumps to another line in the current conversation by its stable LineID.
        /// Reverse history is positionally indexed, so a backward jump keeps the still-valid
        /// prefix (frames before the target) — reverse keeps working through hub loops — while
        /// a forward jump resets it (intermediate frames would be holes), matching
        /// <see cref="SwitchConversation"/>.
        /// </summary>
        private bool JumpToLineId(string targetLineId, bool pushReturnPoint)
        {
            int index = ConversationData != null ? ConversationData.GetLineIndexById(targetLineId) : -1;
            if (index < 0)
            {
                Debug.LogWarning(
                    $"[WitWeaver] GoToLine target '{targetLineId}' not found in '{ConversationData?.name}'.");
                return TryReturnOrEnd();
            }

            if (pushReturnPoint)
                _returnStack.Push((ConversationData, _currentLineIndex + 1));

            if (index <= _currentLineIndex)
            {
                // Backward jump (or self-loop): frames 0..index-1 stay positionally valid;
                // drop the rest — they are re-recorded in place as lines replay.
                if (_history.Count > index)
                    _history.RemoveRange(index, _history.Count - index);
            }
            else
            {
                // Forward jump: positional holes are undefined; reset.
                _history.Clear();
            }

            _currentLineIndex = index;
            return true;
        }

        private bool HandleContainerBranch(WitWeaverConversationData.LineContinuation cont)
        {
            if (ConversationData == null)
                return false;

            var container = cont.TargetContainer;
            if (container == null)
            {
                Debug.LogWarning("[WitWeaver] ContainerBranch used but TargetContainer is null.");
                return TryReturnOrEnd();
            }

            if (cont.PushReturnPoint)
                _returnStack.Push((ConversationData, _currentLineIndex + 1));

            var result = container.ResolveForBranch(_context, cont.TargetAliasOrName);
            if (result.Conversation == null)
            {
                Debug.LogWarning($"[WitWeaver] Container '{container.name}' returned no conversation for branch.");
                return TryReturnOrEnd();
            }

            SwitchConversation(result.Conversation, result.StartLineIndex);
            return true;
        }



        private bool TryReturnOrEnd()
        {
            if (_returnStack.Count > 0)
            {
                var rp = _returnStack.Pop();
                SwitchConversation(rp.convo, rp.index);
                return true;
            }

            CurrentDialogueState = ConversationState.Completed;
            return false;
        }


        private void SwitchConversation(WitWeaverConversationData newConversation, int startIndex)
        {
            if (newConversation == null)
            {
                CurrentDialogueState = ConversationState.Completed;
                return;
            }

            ConversationData = newConversation;
            ConversationData.InitializeDialogueData();

            _executedActionsPerLine.Clear();
            _history.Clear();

            var lines = ConversationData.DialogueLines;
            _currentLineIndex = Mathf.Clamp(startIndex, 0, lines.Count - 1);
        }


        // internal so WitWeaverConversationData can call it
        internal bool ShouldExecuteAction(BaseDialogueLineAction action, int lineIndex)
        {
            if (action == null)
                return false;

            if (!action.RunOnlyOncePerConversation)
                return true;

            if (!_executedActionsPerLine.TryGetValue(action, out var lines))
            {
                lines = new HashSet<int>();
                _executedActionsPerLine[action] = lines;
            }

            if (!lines.Add(lineIndex))
                return false;

            return true;
        }

        private IEnumerator ReverseOneLineRoutine()
        {
            _audioProvider?.StopVoiceLine();

            // undo the line we are currently sitting on
            int current = _currentLineIndex;
            if (current >= 0 && current < _history.Count)
            {
                var frame = _history[current];

                // reverse after actions in reverse order
                for (int i = frame.After.Count - 1; i >= 0; i--)
                {
                    var a = frame.After[i];
                    if (a != null) yield return StartCoroutine(a.ExecuteOnReversedLineAction());
                    if (a != null) DestroyImmediate(a);
                }

                // reverse before actions in reverse order
                for (int i = frame.Before.Count - 1; i >= 0; i--)
                {
                    var a = frame.Before[i];
                    if (a != null) yield return StartCoroutine(a.ExecuteOnReversedLineAction());
                    if (a != null) DestroyImmediate(a);
                }

                // drop this frame
                _history[current] = null;
            }

            // move cursor back one
            _currentLineIndex = Mathf.Max(0, _currentLineIndex - 1);

            // re-display the previous line's UI text and portraits without re-running actions
            var prevLine = ConversationData.DialogueLines[_currentLineIndex];

            var primaryProfile = ConversationData.ResolveCharacterProfile(
                ConversationData.ConversationParticipantProfiles,
                prevLine.characterID);

            prevLine.EnsureCharacterRepresentationListInitialized();
            var speakerRepData = (prevLine.CharacterRepresentations != null && prevLine.CharacterRepresentations.Count > 0)
                ? prevLine.CharacterRepresentations[0]
                : default;

            var primaryRepresentation = GetPrimaryCharacterRepresentation(primaryProfile, speakerRepData);

            var localized = LocalizationHandler.GetLocalizedDialogue(prevLine);
            string finalOutput = ReplacePlayerNameInDialogueLine(localized.Text);

            yield return StartCoroutine(
                PlayDialogueLine(ConversationUI, prevLine, finalOutput,
                    primaryProfile.CharacterName, primaryRepresentation, primaryProfile));

            // do not block on WaitForUserInput here. let your UI decide how back navigation returns to idle.
        }
        /// <summary>
        /// Helper method to get primary character representation (special handling for speakers)
        /// </summary>
        /// <param name="primaryProfile">The character profile of the speaker</param>
        /// <param name="representationData">The representation data from the dialogue line</param>
        private CharacterRepresentationBase GetPrimaryCharacterRepresentation(WitWeaverCharacterProfileBaseData primaryProfile, WitWeaverConversationData.CharacterRepresentationData representationData)
        {
            // For primary characters, if no specific representation is set, try to get the first available one
            if (string.IsNullOrEmpty(representationData.SelectedRepresentationName) && 
                representationData.SelectedRepresentation == null &&
                string.IsNullOrEmpty(representationData.SelectedCharacterID))
            {
                // This looks like an uninitialized primary character representation
                // Try to get the first available representation from the primary character's profile
                if (primaryProfile.Representations is { Count: > 0 })
                {
                    var result = primaryProfile.Representations[0].CharacterRepresentationType;
                    if (result != null)
                    {
                        Debug.LogWarning($"Auto-assigning first available representation '{primaryProfile.Representations[0].CharacterRepresentationName}' from profile '{primaryProfile.CharacterName}' for primary character.");
                        return result;
                    }
                }
                
                Debug.LogError($"Primary character '{primaryProfile.CharacterName}' has no available representations. Please ensure the character profile has at least one representation assigned.");
                return null;
            }
            
            // Use the standard resolution method for other cases
            return GetCharacterRepresentationFromData(primaryProfile, representationData, true);
        }
      
        /// <summary>
        /// Helper method to get character representation from representation data
        /// </summary>
        /// <param name="profile">The character profile to get representation from</param>
        /// <param name="representationData">The representation data</param>
        /// <param name="isPrimaryCharacter">Whether this is for a primary character (primary characters cannot be "None")</param>
        private CharacterRepresentationBase GetCharacterRepresentationFromData(WitWeaverCharacterProfileBaseData profile, WitWeaverConversationData.CharacterRepresentationData representationData, bool isPrimaryCharacter = false)
        {
            CharacterRepresentationBase result;
            
            // Check if this is using the new secondary/tertiary system (has SelectedCharacterID)
            if (!string.IsNullOrEmpty(representationData.SelectedCharacterID))
            {
                // Find the profile by the selected character ID
                var selectedProfile = ConversationData.ConversationParticipantProfiles
                    .FirstOrDefault(p => p != null && p.CharacterID == representationData.SelectedCharacterID);
                
                if (selectedProfile != null)
                {
                    if (!string.IsNullOrEmpty(representationData.SelectedRepresentationName))
                    {
                        result = selectedProfile.GetRepresentation(representationData.SelectedRepresentationName);
                        if (result != null)
                        {
                            return result;
                        }
                        Debug.LogWarning($"Representation '{representationData.SelectedRepresentationName}' not found in profile '{selectedProfile.CharacterName}'. Trying fallbacks.");
                        
                        // Fallback: Try to get the first available representation from the selected profile
                        if (selectedProfile.Representations is { Count: > 0 })
                        {
                            result = selectedProfile.Representations[0].CharacterRepresentationType;
                            if (result != null)
                            {
                                Debug.LogWarning($"Using first available representation from profile '{selectedProfile.CharacterName}'.");
                                return result;
                            }
                        }
                    }
                    else
                    {
                        // Handle "None" case for secondary/tertiary characters only
                        if (!isPrimaryCharacter)
                        {
                            return null; // Valid "None" selection for secondary/tertiary
                        }
                        else
                        {
                            Debug.LogError($"Primary character cannot have 'None' representation. Profile: '{selectedProfile.CharacterName}'");
                            return null;
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"Profile with CharacterID '{representationData.SelectedCharacterID}' not found.");
                    return null;
                }
            }
            else
            {
                // Handle primary character or fallback to the old system
                if (profile != null)
                {
                    // Check if this is an intentional "None" selection
                    bool isIntentionalNone = string.IsNullOrEmpty(representationData.SelectedRepresentationName) &&
                                            representationData.SelectedRepresentation == null;
                    
                    if (isIntentionalNone)
                    {
                        if (!isPrimaryCharacter)
                        {
                            // This is valid for secondary/tertiary characters
                            return null;
                        }
                        else
                        {
                            // This is NOT valid for primary characters - they must have a representation
                            Debug.LogError($"Primary character '{profile.CharacterName}' cannot have 'None' representation. The primary character must have a valid representation as they are the speaker.");
                            
                            // Try to auto-assign the first available representation as a fallback
                            if (profile.Representations is { Count: > 0 })
                            {
                                result = profile.Representations[0].CharacterRepresentationType;
                                if (result != null)
                                {
                                    Debug.LogWarning($"Auto-assigning first available representation from profile '{profile.CharacterName}' as fallback.");
                                    return result;
                                }
                            }
                            
                            return null; // No valid representation found
                        }
                    }
                    
                    // Try to get representation by name from the provided profile
                    if (!string.IsNullOrEmpty(representationData.SelectedRepresentationName))
                    {
                        result = profile.GetRepresentation(representationData.SelectedRepresentationName);
                        if (result != null)
                        {
                            return result;
                        }
                        Debug.LogWarning($"Representation '{representationData.SelectedRepresentationName}' not found in profile '{profile.CharacterName}'. Trying fallbacks.");
                    }
                    
                    // Fallback: Use SelectedRepresentation if available (for backward compatibility)
                    if (representationData.SelectedRepresentation != null)
                    {
                        return representationData.SelectedRepresentation;
                    }
                    
                    // Fallback: Get the first available representation from the profile
                    if (profile.Representations is { Count: > 0 })
                    {
                        result = profile.Representations[0].CharacterRepresentationType;
                        if (result != null)
                        {
                            Debug.LogWarning($"Using first available representation from profile '{profile.CharacterName}'.");
                            return result;
                        }
                    }
                    
                    Debug.LogError($"No valid representations found for profile '{profile.CharacterName}'.");
                }
                else
                {
                    Debug.LogError("Profile is null, cannot get character representation.");
                }
            }
            
            return null;
        }
        /// <summary>
        /// Plays a dialogue line with the UI foundation
        /// </summary>
        private IEnumerator PlayDialogueLine(WitWeaverUIFoundation uiFoundation,
            WitWeaverConversationData.DialogueLineInfo dialogueLineInfo, string localizedText,
            string speakingCharacterName, CharacterRepresentationBase characterRepresentation,
            WitWeaverCharacterProfileBaseData primaryProfile)
        {
            // Update the UI with dialogue information
            uiFoundation.UpdateDialogueUI(dialogueLineInfo, localizedText, speakingCharacterName, characterRepresentation,primaryProfile);
            yield return null; // Wait one frame for UI to update
        }
        /// <summary>
        /// Waits until the audio provider reports that playback has finished, or the conversation
        /// is no longer active, or the 5-minute safety cap is reached.
        /// </summary>
        private IEnumerator WaitForAudioComplete()
        {
            if (_audioProvider == null) yield break;

            // Safety guard: don't wait longer than 5 minutes regardless of clip length
            const float maxWait = 300f;
            float elapsed = 0f;

            // Wait one frame to allow the provider to begin playback before polling
            yield return null;

            while (_audioProvider.IsPlaying &&
                   CurrentDialogueState == ConversationState.Active &&
                   elapsed < maxWait)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        /// <summary>
        /// Replaces player name placeholders in dialogue text
        /// </summary>
        private string ReplacePlayerNameInDialogueLine(string dialogueText)
        {
            if (string.IsNullOrEmpty(dialogueText))
                return dialogueText;

            var playerProfile = ConversationData.GetPlayerProfile();
            if (playerProfile != null)
            {
                return dialogueText.Replace("{PlayerName}", playerProfile.CharacterName);
            }
            
            return dialogueText.Replace("{PlayerName}", "Player");
        }
        /// <summary>
        /// Updates UI for language change
        /// </summary>
        public void UpdateUIForLanguage(string selectedLanguage)
        {
            Debug.Log($"Language updated to: {selectedLanguage}");
            
            // Update the localization handler if it exists
            if (LocalizationHandler != null)
            {
                LocalizationHandler = new WitWeaverDialogueLocalizationHandler(WitWeaverLanguageManager.Instance);
            }
            
            // If we're currently in an active conversation, refresh the current dialogue line
            if (CurrentDialogueState == ConversationState.Active && 
                ConversationData != null && 
                ConversationData.DialogueLines != null && 
                _currentLineIndex < ConversationData.DialogueLines.Count)
            {
                var currentLine = ConversationData.DialogueLines[_currentLineIndex];
                
                // Get the localized dialogue for the new language
                if (LocalizationHandler != null)
                {
                    var localizedResult = LocalizationHandler.GetLocalizedDialogue(currentLine);
                    if (localizedResult.Success)
                    {
                        // Replace player name placeholders
                        string finalOutputString = ReplacePlayerNameInDialogueLine(localizedResult.Text);
                    
                        // Update the UI with both the new localized text and language code
                        if (ConversationUI != null)
                        {
                            ConversationUI.UpdateForLanguageChange(finalOutputString, selectedLanguage);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to get localized dialogue for new language '{selectedLanguage}': {localizedResult.ErrorMessage}");
                    }
                }
            }
            else
            {
                // If no active conversation, just notify about the language change
                if (ConversationUI != null)
                {
                    ConversationUI.UpdateForLanguageChange(null, selectedLanguage);
                }
            }
        }

        /// <summary>
        /// Pauses the current conversation
        /// </summary>
        public void PauseConversation()
        {
            if (CurrentDialogueState == ConversationState.Active)
            {
                CurrentDialogueState = ConversationState.Paused;
                _audioProvider?.PauseVoiceLine();
                Debug.Log("Conversation paused.");
                PausedConversation?.Invoke();
            }
        }

        /// <summary>
        /// Resumes a paused conversation
        /// </summary>
        public void ResumeConversation()
        {
            if (CurrentDialogueState == ConversationState.Paused)
            {
                CurrentDialogueState = ConversationState.Active;
                _audioProvider?.ResumeVoiceLine();
                Debug.Log("Conversation resumed.");
            }
        }
        /// <summary>
        /// Reverses the conversation and goes back a single line
        /// </summary>
        public void ReverseOneLine()
        {
            if (!CanReverseOneLine) return;
            StartCoroutine(ReverseOneLineRoutine());
        }
        /// <summary>
        /// Stops the current conversation
        /// </summary>
        public void StopConversation()
        {
            CurrentDialogueState = ConversationState.Inactive;
            _audioProvider?.StopVoiceLine();
            EndedConversation?.Invoke();
            OnConversationEnded?.Invoke();
            _currentLineIndex = 0;
            if (ConversationUI != null)
            {
                ConversationUI.HideDialogue();
            }
            Debug.Log("Conversation stopped.");
        }

        /// <summary>
        /// Initializes and starts the specified conversation, setting up the necessary UI and internal states.
        /// </summary>
        /// <param name="conversation">The conversation data to be played. Includes dialogue lines and relevant metadata.</param>
        public void PlayConversation(WitWeaverConversationData conversation)
        {
            if (conversation == null)
            {
                Debug.LogError("[WitWeaver] Play called with null conversation.");
                return;
            }
            ConversationData = conversation;
            ConversationData.InitializeDialogueData();
            // reset per conversation state
            _executedActionsPerLine.Clear();
            _history.Clear();
            _currentLineIndex = 0;
            
            if (ConversationUI != null)
            {
               BindUIEvents();
            }
            if (ConversationData == null)
            {
                Debug.LogError("ConversationData is not assigned!");
                return;
            }
            if (ConversationData.DialogueLines == null || ConversationData.DialogueLines.Count == 0)
            {
                Debug.LogError("No dialogue lines found in ConversationData!");
                return;
            }

            _visitedLineIds.Clear();
            _currentLineIndex = 0;

            // Auto-provision Unity audio provider when backend is UnityAudioSource and none assigned
            if (_audioProvider == null &&
                ConversationData.AudioManifest != null &&
                ConversationData.AudioManifest.Backend == AudioBackend.UnityAudioSource)
            {
                var src = gameObject.GetComponent<AudioSource>()
                          ?? gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                var prov = gameObject.GetComponent<WitWeaverUnityAudioProvider>()
                           ?? gameObject.AddComponent<WitWeaverUnityAudioProvider>();
                _audioProvider = prov;
            }

            // Let a co-located IWitWeaverStartContextProvider control how playback begins
            var provider = GetComponent<IWitWeaverStartContextProvider>();
            if (provider != null)
            {
                var ctx = provider.GetStartContext();
                if (ctx.Mode == WitWeaverStartMode.Resume)
                {
                    if (ctx.VisitedLineIds != null) SetVisitedLines(ctx.VisitedLineIds);
                    if (!string.IsNullOrEmpty(ctx.StartLineId)) BeginFromLine(ctx.StartLineId);
                }
                // Restart: index stays 0, _visitedLineIds already cleared
                // Fresh: nothing to do
            }

            CurrentDialogueState = ConversationState.Active;
            StartedConversation?.Invoke();
            OnConversationStarted?.Invoke();
            StartCoroutine(ExecuteDialogueSequence());
        }

        private void BindUIEvents()
        {
            ConversationUI.InitializeUI(this);
            ConversationUI.RequestAdvance += () => _advanceRequested = true;
            ConversationUI.RequestReverse += () => _reverseRequested = true;
        }
        /// <summary>
        /// Entry point for Unity SendMessage or systems that don't support method overloading.
        /// This triggers the conversation using the currently assigned Input settings.
        /// </summary>
        public void StartConversation()
        {
            PlayConversation();
        }

        /// <summary>
        /// Starts the playback of a conversation using the active input mechanism.
        /// </summary>
        /// <remarks>
        /// This method determines the appropriate way to play a conversation based on the current `Input` instance.
        /// For a `SingleConversationInput` with a valid conversation, it invokes the typed overload method.
        /// Otherwise, it delegates the playback control to the container input itself by calling `Input.Play()`.
        /// If no input mechanism is defined, a warning is logged, and the method returns without initiating playback.
        /// </remarks>
        public void PlayConversation()
        {
            if (Input == null)
            {
                Debug.LogWarning("[WitWeaver] Input is null.");
                return;
            }

            // If Input is a SingleConversationInput with a valid conversation, route to the typed overload
            if (Input is SingleConversationInput sci && sci.Conversation != null)
            {
                PlayConversation(sci.Conversation);
                return;
            }

            // Otherwise let the input drive (e.g., ContainerInput starts its own sequencing coroutine)
            Input.Play(this, this);
        }

    }

    /// <summary>
    /// Minimal interface implemented by <see cref="WitWeaver"/>. Allows external systems
    /// (container runners, save managers) to start a conversation without a direct
    /// MonoBehaviour reference.
    /// </summary>
    public interface IWitWeaverRunner
    {
        void PlayConversation(WitWeaverConversationData conversation);
        public event Action CompletedConversation;

    }
}