using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// World-space dialogue UI for 3D character conversations. No sprite support; no slot system.
    /// Character placement is entirely behaviour-driven via <see cref="ConvoCoreCharacterBehaviour"/>.
    ///
    /// Characters are persistent across lines. Expression application is the only per-line
    /// operation; characters are not despawned and re-spawned between lines.
    ///
    /// <see cref="ConvoCoreCharacterBehaviour.OnConversationBegin"/> is called when
    /// <see cref="ConvoCore.StartedConversation"/> fires.
    /// <see cref="ConvoCoreCharacterBehaviour.OnConversationEnd"/> is called when
    /// <see cref="ConvoCore.EndedConversation"/> fires. The spawner is never called directly
    /// by this UI for release; that responsibility belongs to the behaviour.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreSampleUI3D.html")]
    public class ConvoCoreSampleUI3D : ConvoCoreUIFoundation
    {
        [Header("Spawner")]
        [Tooltip("Spawner passed to the behaviour for prefab lifecycle management.")]
        [SerializeField] private ConvoCorePrefabRepresentationSpawner prefabRepresentationSpawner;

        [Header("Choice UI")]
        [SerializeField] private GameObject ChoicePanel;
        [SerializeField] private Transform ChoiceButtonContainer;
        [SerializeField] private GameObject ChoiceButtonPrefab;

        [Header("Dialogue UI")]
        [SerializeField] private TextMeshProUGUI DialogueText;
        [SerializeField] private TextMeshProUGUI SpeakerName;
        [SerializeField] private GameObject DialoguePanel;
        [SerializeField] private Button ContinueButton;
        [SerializeField] private Button PreviousLineButton;

        [Header("Dialogue History")]
        [SerializeField] private RectTransform DialogueHistoryPanelRoot;
        [SerializeField] private ScrollRect DialogueHistoryScrollRect;
        [SerializeField] private Image DialogueHistoryContentBackground;
        [SerializeField] private TMP_Text DialogueHistoryText;
        [SerializeField] private Button ToggleDialogueHistoryButton;

        [Header("Settings")]
        [SerializeField] private bool AllowLineAdvanceOutsideButton;
        [SerializeField] private bool EnableTypewriterEffect = true;
        [SerializeField] private float TypewriterSpeed = 0.05f;
        [SerializeField] private bool CanSkipTypewriter = true;
        public bool AutoHideUIOnStart;

        [Header("Input")]
        [SerializeField] private InputAction AdvanceDialogueAction;
        [SerializeField] private InputAction PreviousDialogueAction;

        // Per-character behaviour tracking: keyed by CharacterID.
        private readonly Dictionary<string, List<ConvoCoreCharacterBehaviour>> _activeBehavioursByCharacter = new();

        private Coroutine _typewriterCoroutine;
        private bool _isTyping;
        private string _fullText = "";
        private bool _isWaitingForInput;
        private bool _historyVisible;
        private CanvasGroup _historyGroup;
        private bool _togglingGuard;
        private readonly List<GameObject> _spawnedChoiceButtons = new();

        private string _lastSpeakerName;
        private Color _lastSpeakerColor;
        private string _lastLineText;
        private int _lastLineIndex = -1;
        private readonly HashSet<int> _committedLineIndices = new();

        // ------------------------------------------------------------------
        // Initialization
        // ------------------------------------------------------------------

        private void Start()
        {
            if (AutoHideUIOnStart)
                HideDialogue();
        }

        public override void InitializeUI(ConvoCore convoCoreInstance)
        {
            base.InitializeUI(convoCoreInstance);

            if (prefabRepresentationSpawner == null)
            {
                prefabRepresentationSpawner = GetComponent<ConvoCorePrefabRepresentationSpawner>();
                if (prefabRepresentationSpawner == null)
                    Debug.LogWarning($"[ConvoCoreSampleUI3D] No ConvoCorePrefabRepresentationSpawner assigned on '{gameObject.name}'.");
            }

            // Subscribe to conversation lifecycle events so behaviours can be notified.
            ConvoCoreInstance.StartedConversation += OnConversationStarted;
            ConvoCoreInstance.EndedConversation   += OnConversationEnded;

            var historyOutput = new TMPDialogueHistoryOutput(DialogueHistoryText, DialogueHistoryScrollRect);
            var ctx = new DialogueHistoryRendererContext
            {
                OutputHandler = historyOutput,
                DefaultSpeakerColor = Color.white,
                MaxEntries = ConvoCoreDialogueHistoryUI.maxEntries
            };

            HideDialogue();
            ContinueButton?.onClick.AddListener(OnContinueButtonPressed);
            PreviousLineButton?.onClick.AddListener(OnPreviousLineButtonPressed);
            ToggleDialogueHistoryButton?.onClick.AddListener(ToggleDialogueHistoryUI);
            AdvanceDialogueAction?.Enable();
            PreviousDialogueAction?.Enable();
            DontDestroyOnLoad(gameObject);
            ConvoCoreDialogueHistoryUI.InitializeRenderer(ctx);
        }

        private void OnConversationStarted()
        {
            if (prefabRepresentationSpawner == null) return;
            var convoData = ConvoCoreInstance.GetCurrentConversationData();
            if (convoData?.ParticipantConfigurationDefaults == null) return;

            int total = convoData.ParticipantConfigurationDefaults.Count;
            for (int idx = 0; idx < total; idx++)
            {
                var slot = convoData.ParticipantConfigurationDefaults[idx];
                if (slot.SpawnTiming != ConvoCoreSpawnTiming.OnConversationBegin) continue;

                var profile = convoData.ConversationParticipantProfiles
                    .FirstOrDefault(p => p.CharacterID == slot.CharacterID);
                if (profile == null) continue;

                PrefabCharacterRepresentationData prefabRep = null;
                foreach (var pair in profile.Representations)
                {
                    if (pair?.CharacterRepresentationType is PrefabCharacterRepresentationData p)
                    { prefabRep = p; break; }
                }
                if (prefabRep == null) continue;

                var entry = prefabRep.GetEntry(slot.DefaultConfigurationEntryName);
                if (entry?.CharacterBehaviours == null || entry.CharacterBehaviours.Count == 0) continue;

                var behaviours = GetOrTransitionBehaviours(slot.CharacterID, entry.CharacterBehaviours);
                var ctx = new CharacterBehaviourContext
                {
                    CharacterIndex         = idx,
                    TotalCharacters        = total,
                    CharacterId            = slot.CharacterID,
                    ConfigurationEntryName = entry.EntryName
                };
                foreach (var b in behaviours)
                    b?.ResolvePresence(prefabRep, ctx, prefabRepresentationSpawner);
            }
        }

        private void OnConversationEnded()
        {
            foreach (var behaviourList in _activeBehavioursByCharacter.Values)
                foreach (var b in behaviourList)
                    b?.OnConversationEnd();
            _activeBehavioursByCharacter.Clear();
        }

        private void OnEnable()
        {
            AdvanceDialogueAction?.Enable();
            PreviousDialogueAction?.Enable();
            if (AdvanceDialogueAction != null) AdvanceDialogueAction.performed += OnAdvancePerformed;
            if (PreviousDialogueAction != null) PreviousDialogueAction.performed += OnPreviousPerformed;
        }

        private void OnDisable()
        {
            AdvanceDialogueAction?.Disable();
            PreviousDialogueAction?.Disable();
            if (AdvanceDialogueAction != null) AdvanceDialogueAction.performed -= OnAdvancePerformed;
            if (PreviousDialogueAction != null) PreviousDialogueAction.performed -= OnPreviousPerformed;
        }

        // ------------------------------------------------------------------
        // Dialogue UI overrides
        // ------------------------------------------------------------------

        public override void UpdateDialogueUI(ConvoCoreConversationData.DialogueLineInfo lineInfo, string localizedText,
            string speakerName, CharacterRepresentationBase primaryRepresentation,
            ConvoCoreCharacterProfileBaseData primaryProfile)
        {
            DisplayDialogue(localizedText);
            SpeakerName.text = speakerName;
            SpeakerName.color = primaryProfile.CharacterNameColor;

            _lastSpeakerName = speakerName;
            _lastSpeakerColor = primaryProfile.CharacterNameColor;
            _lastLineText = localizedText;
            _lastLineIndex = lineInfo.ConversationLineIndex;

            lineInfo.EnsureCharacterRepresentationListInitialized();

            if (prefabRepresentationSpawner != null)
            {
                var convoData = ConvoCoreInstance.GetCurrentConversationData();
                int count = lineInfo.CharacterRepresentations.Count;
                for (int i = 0; i < count; i++)
                {
                    var charData = lineInfo.CharacterRepresentations[i];
                    var rep = GetRepresentationFromData(convoData, charData);

                    if (rep is not PrefabCharacterRepresentationData prefabRep)
                        continue;

                    // Determine the character ID for registry-first lookup and per-character tracking.
                    var characterId = !string.IsNullOrEmpty(charData.SelectedCharacterID)
                        ? charData.SelectedCharacterID
                        : rep.name;

                    // Resolve the configuration entry: per-line → participant default → asset default.
                    var entryName = ResolveEntryName(convoData, charData, characterId);
                    var entry = prefabRep.GetEntry(entryName);
                    if (entry?.CharacterBehaviours == null || entry.CharacterBehaviours.Count == 0)
                    {
                        Debug.LogWarning($"[ConvoCoreSampleUI3D] Configuration entry '{entryName}' for '{rep.name}' has no Character Behaviours assigned. Skipping character {i}.");
                        continue;
                    }

                    // Transition the behaviours if they changed since the last line.
                    var behaviours = GetOrTransitionBehaviours(characterId, entry.CharacterBehaviours);

                    var ctx = new CharacterBehaviourContext
                    {
                        CharacterIndex         = i,
                        TotalCharacters        = count,
                        DisplayOptions         = charData.LineSpecificDisplayOptions,
                        CharacterId            = characterId,
                        ConfigurationEntryName = entry.EntryName
                    };

                    // Fan out across all behaviours; use the first non-null display.
                    IConvoCoreCharacterDisplay display = null;
                    foreach (var behaviour in behaviours)
                    {
                        var d = behaviour?.ResolvePresence(prefabRep, ctx, prefabRepresentationSpawner);
                        if (display == null && d != null)
                            display = d;
                    }

                    if (display == null)
                    {
                        Debug.LogWarning($"[ConvoCoreSampleUI3D] All behaviours returned null for character {i} ('{rep.name}'). Expression will not be applied.");
                        continue;
                    }

                    display.BindRepresentation(rep);

                    if (charData.LineSpecificDisplayOptions != null)
                        display.ApplyDisplayOptions(charData.LineSpecificDisplayOptions);

                    if (!string.IsNullOrEmpty(charData.SelectedExpressionId))
                        display.ApplyExpression(charData.SelectedExpressionId);

                    // Run any BaseExpressionAction ScriptableObjects attached to this expression.
                    // The display only handles built-in visuals; the representation owns the actions.
                    RunExpressionActions(
                        rep, charData.SelectedExpressionId, lineInfo.ConversationLineIndex, display);
                }
            }

            ContinueButton?.gameObject.SetActive(true);
            RefreshNavButtons();
        }

        public override void DisplayDialogue(string text)
        {
            _fullText = text;
            DialogueText.text = text;
            DialogueText.gameObject.SetActive(true);
            SpeakerName.gameObject.SetActive(true);
            DialoguePanel.gameObject.SetActive(true);

            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }

            if (EnableTypewriterEffect)
            {
                DialogueText.text = "";
                _isTyping = true;
                _typewriterCoroutine = StartCoroutine(TypewriterEffect(text));
            }
            else
            {
                _isTyping = false;
            }
        }

        public override void HideDialogue()
        {
            DialogueText.gameObject.SetActive(false);
            SpeakerName.gameObject.SetActive(false);
            DialoguePanel.gameObject.SetActive(false);
            ContinueButton.gameObject.SetActive(false);
            ToggleDialogueHistoryUI(false);
            RefreshNavButtons();

            foreach (var btn in _spawnedChoiceButtons)
                if (btn != null) Destroy(btn);
            _spawnedChoiceButtons.Clear();
            ChoicePanel?.SetActive(false);
        }

        public override IEnumerator WaitForUserInput()
        {
            _isWaitingForInput = true;
            ContinueButton.gameObject.SetActive(true);

            while (_isWaitingForInput)
                yield return null;

            ContinueButton.gameObject.SetActive(false);
        }

        public override IEnumerator PresentChoices(
            List<ConvoCoreConversationData.ChoiceOption> options,
            List<string> localizedLabels,
            ChoiceResult result)
        {
            if (ChoicePanel == null || ChoiceButtonContainer == null || ChoiceButtonPrefab == null)
            {
                Debug.LogWarning("[ConvoCoreSampleUI3D] ChoicePanel, ChoiceButtonContainer, or ChoiceButtonPrefab not assigned. Auto-selecting first choice.");
                result.SelectedIndex = 0;
                yield break;
            }

            ChoicePanel.SetActive(true);
            ContinueButton?.gameObject.SetActive(false);

            _spawnedChoiceButtons.Clear();
            for (int i = 0; i < localizedLabels.Count; i++)
            {
                int captured = i;
                var instance = Instantiate(ChoiceButtonPrefab, ChoiceButtonContainer);
                _spawnedChoiceButtons.Add(instance);
                var btn = instance.GetComponent<Button>();
                var label = instance.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = localizedLabels[captured];
                if (btn != null) btn.onClick.AddListener(() => result.SelectedIndex = captured);
            }

            yield return new WaitUntil(() => result.IsResolved);

            foreach (var btn in _spawnedChoiceButtons)
                if (btn != null) Destroy(btn);
            _spawnedChoiceButtons.Clear();

            ChoicePanel.SetActive(false);
            ContinueButton?.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------
        // Input and button handling
        // ------------------------------------------------------------------

        public void OnContinueButtonPressed()
        {
            if (!_isWaitingForInput) return;

            if (_isTyping && CanSkipTypewriter)
            {
                CompleteTypewriter();
                return;
            }

            if (_lastLineIndex >= 0 && !_committedLineIndices.Contains(_lastLineIndex))
            {
                if (!string.IsNullOrEmpty(_lastSpeakerName) && !string.IsNullOrEmpty(_lastLineText))
                {
                    ConvoCoreDialogueHistoryUI.AddLine(_lastSpeakerName, _lastLineText, _lastSpeakerColor);
                    _committedLineIndices.Add(_lastLineIndex);
                }
            }

            _isWaitingForInput = false;
            RaiseAdvance();
        }

        private void OnPreviousLineButtonPressed()
        {
            if (_isTyping && CanSkipTypewriter)
            {
                CompleteTypewriter();
                return;
            }
            _isWaitingForInput = false;
            RaiseReverse();
        }

        private void OnAdvancePerformed(InputAction.CallbackContext context)
        {
            if (AllowLineAdvanceOutsideButton && IsPointerOverUI()) return;
            if (_isTyping && !CanSkipTypewriter) return;

            if (_isWaitingForInput && (AllowLineAdvanceOutsideButton || !IsPointerOverUIElement(ContinueButton)))
                OnContinueButtonPressed();
            else if (_isTyping && CanSkipTypewriter)
                CompleteTypewriter();
        }

        private void OnPreviousPerformed(InputAction.CallbackContext context)
        {
            if (IsPointerOverUI()) return;
            if (_isTyping && !CanSkipTypewriter) return;
            OnPreviousLineButtonPressed();
        }

        private void RefreshNavButtons()
        {
            bool canReverse = ConvoCoreInstance != null && ConvoCoreInstance.CanReverseOneLine && !_historyVisible;
            if (PreviousLineButton != null) PreviousLineButton.interactable = canReverse;
        }

        // ------------------------------------------------------------------
        // Typewriter
        // ------------------------------------------------------------------

        private IEnumerator TypewriterEffect(string text)
        {
            DialogueText.text = "";
            for (int i = 0; i < text.Length; i++)
            {
                DialogueText.text = text.Substring(0, i + 1);
                yield return new WaitForSeconds(TypewriterSpeed);
            }
            _isTyping = false;
            _typewriterCoroutine = null;
        }

        private void CompleteTypewriter()
        {
            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }
            DialogueText.text = _fullText;
            _isTyping = false;
        }

        // ------------------------------------------------------------------
        // History
        // ------------------------------------------------------------------

        public void ToggleDialogueHistoryUI() => ToggleDialogueHistoryUI(null);

        public void ToggleDialogueHistoryUI(bool? setVisible)
        {
            if (_togglingGuard || DialogueHistoryPanelRoot == null) return;

            bool target = setVisible ?? !_historyVisible;
            _togglingGuard = true;
            _historyVisible = target;

            if (_historyGroup != null)
            {
                _historyGroup.alpha = target ? 1f : 0f;
                _historyGroup.interactable = target;
                _historyGroup.blocksRaycasts = target;
                DialogueHistoryPanelRoot.gameObject.SetActive(true);
            }
            else
            {
                DialogueHistoryPanelRoot.gameObject.SetActive(target);
            }

            if (DialogueHistoryContentBackground)
                DialogueHistoryContentBackground.enabled = target;

            if (ContinueButton != null)
                ContinueButton.interactable = !target;

            if (target && DialogueHistoryScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                DialogueHistoryScrollRect.normalizedPosition = new Vector2(0f, 0f);
            }

            _togglingGuard = false;
            RefreshNavButtons();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Resolves the configuration entry name using the three-level priority chain:
        /// per-line override → participant default on the conversation → representation asset default.
        /// Returns null to signal "use the asset default entry".
        /// </summary>
        private static string ResolveEntryName(
            ConvoCoreConversationData convoData,
            ConvoCoreConversationData.CharacterRepresentationData charData,
            string characterId)
        {
            if (!string.IsNullOrEmpty(charData.SelectedConfigurationEntryName))
                return charData.SelectedConfigurationEntryName;

            var participantDefault = convoData?.GetParticipantDefaultEntry(characterId);
            if (!string.IsNullOrEmpty(participantDefault))
                return participantDefault;

            return null; // signals GetEntry(null) → GetDefaultEntry()
        }

        /// <summary>
        /// Returns the active behaviour list for the given character.
        /// If the behaviours changed since the last line, the old ones are ended and the new ones begun.
        /// On first appearance the behaviours are begun immediately.
        /// </summary>
        private List<ConvoCoreCharacterBehaviour> GetOrTransitionBehaviours(
            string characterId, List<ConvoCoreCharacterBehaviour> newBehaviours)
        {
            if (_activeBehavioursByCharacter.TryGetValue(characterId, out var current))
            {
                if (current == newBehaviours || current.SequenceEqual(newBehaviours))
                    return current;
                foreach (var b in current)
                    b?.OnConversationEnd();
            }
            foreach (var b in newBehaviours)
                b?.OnConversationBegin();
            _activeBehavioursByCharacter[characterId] = newBehaviours;
            return newBehaviours;
        }

        private CharacterRepresentationBase GetRepresentationFromData(ConvoCoreConversationData convoData,
            ConvoCoreConversationData.CharacterRepresentationData data)
        {
            if (!string.IsNullOrEmpty(data.SelectedCharacterID))
            {
                var profile = convoData?.ConversationParticipantProfiles.FirstOrDefault(p =>
                    p.CharacterID == data.SelectedCharacterID);
                return profile?.GetRepresentation(data.SelectedRepresentationName);
            }
            return data.SelectedRepresentation;
        }

        private static bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            var inputModule = EventSystem.current.currentInputModule as InputSystemUIInputModule;
            return inputModule != null
                ? EventSystem.current.IsPointerOverGameObject(-1)
                : EventSystem.current.IsPointerOverGameObject();
        }

        private static bool IsPointerOverUIElement(Component uiElement)
        {
            if (uiElement == null) return false;
            var rt = uiElement.GetComponent<RectTransform>();
            if (rt == null) return false;

            Vector2 mousePosition;
#if ENABLE_INPUT_SYSTEM
            mousePosition = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : (Vector2)Input.mousePosition;
#else
            mousePosition = Input.mousePosition;
#endif
            return rt.rect.Contains(rt.InverseTransformPoint(mousePosition));
        }

        private void OnDestroy()
        {
            if (ConvoCoreInstance != null)
            {
                ConvoCoreInstance.StartedConversation -= OnConversationStarted;
                ConvoCoreInstance.EndedConversation   -= OnConversationEnded;
            }

            AdvanceDialogueAction?.Disable();
            PreviousDialogueAction?.Disable();
            _committedLineIndices.Clear();
            _activeBehavioursByCharacter.Clear();
        }
    }
}
