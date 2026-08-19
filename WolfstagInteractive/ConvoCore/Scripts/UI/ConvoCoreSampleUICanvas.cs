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
using Image = UnityEngine.UI.Image;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Canvas-space dialogue UI supporting both sprite and prefab-in-canvas character representations.
    ///
    /// Slot configuration is driven entirely by <see cref="ConvoCoreUIFoundation.DisplaySlots"/>:
    /// each entry's <see cref="ConvoCoreUIFoundation.DisplaySlotDefinition.SlotObject"/> serves as
    /// the anchor for prefab characters and the Image source for sprite full-body characters.
    /// Slot assignment uses <see cref="DialogueLineDisplayOptions.DisplaySlot"/> (named addressing)
    /// first, then index-based fallback.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreSampleUICanvas.html")]
    public class ConvoCoreSampleUICanvas : ConvoCoreUIFoundation
    {
        [Header("Prefab Representation")]
        [Tooltip("Spawner used to place prefab characters into canvas slot anchors.")]
        [SerializeField] private ConvoCorePrefabRepresentationSpawner prefabRepresentationSpawner;

        /// <summary>
        /// Slot count mirrors <see cref="ConvoCoreUIFoundation.DisplaySlots"/>; falls back to 1 if
        /// no slots have been configured on the foundation.
        /// </summary>
        public virtual int MaxVisibleCharacterSlots => Mathf.Max(1, DisplaySlots.Count);
        [Header("Choice UI")]
        [SerializeField] private GameObject ChoicePanel;
        [SerializeField] private Transform ChoiceButtonContainer;
        [SerializeField] private GameObject ChoiceButtonPrefab;

        [Header("Dialogue UI")]
        [SerializeField] private TextMeshProUGUI DialogueText;
        [SerializeField] private TextMeshProUGUI SpeakerName;
        [SerializeField] private GameObject DialoguePanel;
        [SerializeField] private Image SpeakerPortraitImage;
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
                    Debug.LogWarning($"[ConvoCoreSampleUICanvas] No ConvoCorePrefabRepresentationSpawner assigned on '{gameObject.name}'.");
            }

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

            int uiCap = Mathf.Max(1, MaxVisibleCharacterSlots);
            int showCount = Mathf.Min(uiCap, lineInfo.CharacterRepresentations.Count);

            HideAllSpriteImages();

            for (int i = 0; i < showCount; i++)
                RenderRepresentation(lineInfo.CharacterRepresentations[i], i);

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
            HideAllSpriteImages();
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
                Debug.LogWarning("[ConvoCoreSampleUICanvas] ChoicePanel, ChoiceButtonContainer, or ChoiceButtonPrefab not assigned. Auto-selecting first choice.");
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
        // Representation rendering
        // ------------------------------------------------------------------

        protected virtual void RenderRepresentation(ConvoCoreConversationData.CharacterRepresentationData data, int index)
        {
            var conversationData = ConvoCoreInstance?.GetCurrentConversationData();
            if (conversationData == null) return;

            var representation = GetRepresentationFromData(conversationData, data);
            if (representation == null) return;

            if (representation is PrefabCharacterRepresentationData prefabRep)
            {
                var anchor = GetSlotAnchor(data.LineSpecificDisplayOptions, index);
                var display = prefabRepresentationSpawner?.ResolveCharacter(
                    prefabRep,
                    data.SelectedExpressionId,
                    data.LineSpecificDisplayOptions,
                    anchor);

                // Run any BaseExpressionAction ScriptableObjects attached to this expression.
                RunExpressionActions(prefabRep, data.SelectedExpressionId, _lastLineIndex, display);
                return;
            }

            var processed = representation.ProcessExpression(data.SelectedExpressionId);
            if (processed is SpriteExpressionMapping spriteMapping)
            {
                RenderSpriteRepresentation(spriteMapping, data.LineSpecificDisplayOptions, index);

                // Sprite visuals are applied above; now run the representation's expression actions.
                RunExpressionActions(representation, data.SelectedExpressionId, _lastLineIndex, display: null);
                return;
            }

            if (processed is AnimatedExpressionMapping animatedMapping)
            {
                RenderAnimatedRepresentation(animatedMapping,
                    representation as AnimatedCharacterRepresentationData,
                    data.LineSpecificDisplayOptions, index);

                // Animated visuals are applied above; now run the representation's expression actions.
                RunExpressionActions(representation, data.SelectedExpressionId, _lastLineIndex, display: null);
                return;
            }

            Debug.LogWarning($"[ConvoCoreSampleUICanvas] Unhandled representation type '{representation.GetType().Name}'. " +
                             "Override RenderRepresentation in a subclass to handle custom types.");
        }

        /// <summary>
        /// Returns the slot anchor (RectTransform) for a representation.
        /// Named addressing via <see cref="DialogueLineDisplayOptions.DisplaySlot"/> takes precedence;
        /// falls back to index-based lookup into <see cref="ConvoCoreUIFoundation.DisplaySlots"/>.
        /// </summary>
        private RectTransform GetSlotAnchor(DialogueLineDisplayOptions options, int index)
        {
            if (!string.IsNullOrEmpty(options?.DisplaySlot))
            {
                var def = DisplaySlots.FirstOrDefault(s => s?.SlotName == options.DisplaySlot);
                var rt = def?.SlotObject?.GetComponent<RectTransform>();
                if (rt != null) return rt;
            }

            if (index >= 0 && index < DisplaySlots.Count)
                return DisplaySlots[index]?.SlotObject?.GetComponent<RectTransform>();

            return null;
        }

        private static DialogueLineDisplayOptions MergeDisplayOptions(
            DialogueLineDisplayOptions perLine,
            DialogueLineDisplayOptions expressionDefault)
        {
            if (perLine == null) return expressionDefault ?? new DialogueLineDisplayOptions();
            if (expressionDefault == null) return perLine;

            return new DialogueLineDisplayOptions
            {
                FlipPortraitX = perLine.FlipPortraitX || expressionDefault.FlipPortraitX,
                FlipPortraitY = perLine.FlipPortraitY || expressionDefault.FlipPortraitY,
                FlipFullBodyX = perLine.FlipFullBodyX || expressionDefault.FlipFullBodyX,
                FlipFullBodyY = perLine.FlipFullBodyY || expressionDefault.FlipFullBodyY,
                DisplaySlot   = !string.IsNullOrEmpty(perLine.DisplaySlot)
                                    ? perLine.DisplaySlot
                                    : expressionDefault.DisplaySlot,
                PortraitScale = perLine.PortraitScale != Vector3.one
                                    ? perLine.PortraitScale
                                    : expressionDefault.PortraitScale,
                FullBodyScale = perLine.FullBodyScale != Vector3.one
                                    ? perLine.FullBodyScale
                                    : expressionDefault.FullBodyScale,
            };
        }

        private void RenderSpriteRepresentation(SpriteExpressionMapping spriteMapping,
            DialogueLineDisplayOptions lineOptions, int index)
        {
            var displayOptions = MergeDisplayOptions(lineOptions, spriteMapping.DisplayOptions);

            // Portrait image is reserved for the primary speaker (index 0) only.
            // Secondary characters have a full-body slot but do not overwrite the speaker portrait.
            if (index == 0 && SpeakerPortraitImage && spriteMapping.PortraitSprite)
            {
                SpeakerPortraitImage.sprite = spriteMapping.PortraitSprite;
                SpeakerPortraitImage.rectTransform.localScale = new Vector3(
                    displayOptions.FlipPortraitX ? -displayOptions.PortraitScale.x : displayOptions.PortraitScale.x,
                    displayOptions.FlipPortraitY ? -displayOptions.PortraitScale.y : displayOptions.PortraitScale.y,
                    displayOptions.PortraitScale.z);
                SpeakerPortraitImage.gameObject.SetActive(true);
                TryFadeIn(SpeakerPortraitImage);
            }

            var fullBodyImage = GetSpriteSlotImage(displayOptions?.DisplaySlot ?? string.Empty, index);
            if (fullBodyImage && spriteMapping.FullBodySprite)
            {
                fullBodyImage.sprite = spriteMapping.FullBodySprite;
                fullBodyImage.rectTransform.localScale = new Vector3(
                    displayOptions.FlipFullBodyX ? -displayOptions.FullBodyScale.x : displayOptions.FullBodyScale.x,
                    displayOptions.FlipFullBodyY ? -displayOptions.FullBodyScale.y : displayOptions.FullBodyScale.y,
                    displayOptions.FullBodyScale.z);
                fullBodyImage.gameObject.SetActive(true);
                TryFadeIn(fullBodyImage);
            }
        }

        /// <summary>
        /// Renders an animated expression mapping onto the speaker portrait (index 0)
        /// and the character's full-body slot Image, mirroring
        /// <see cref="RenderSpriteRepresentation"/> but driving the images through a
        /// <see cref="ConvoCoreAnimatedPortraitPlayer"/> instead of a static sprite.
        /// </summary>
        protected virtual void RenderAnimatedRepresentation(AnimatedExpressionMapping mapping,
            AnimatedCharacterRepresentationData representationData, DialogueLineDisplayOptions lineOptions, int index)
        {
            var displayOptions = MergeDisplayOptions(lineOptions, mapping.DisplayOptions);
            bool useUnscaledTime = representationData == null || representationData.UseUnscaledTime;

            // Portrait image is reserved for the primary speaker (index 0) only.
            if (index == 0 && SpeakerPortraitImage && mapping.PortraitAnimation is { IsConfigured: true })
            {
                SpeakerPortraitImage.rectTransform.localScale = new Vector3(
                    displayOptions.FlipPortraitX ? -displayOptions.PortraitScale.x : displayOptions.PortraitScale.x,
                    displayOptions.FlipPortraitY ? -displayOptions.PortraitScale.y : displayOptions.PortraitScale.y,
                    displayOptions.PortraitScale.z);
                SpeakerPortraitImage.gameObject.SetActive(true);
                ConvoCoreAnimatedPortraitPlayer.GetOrAdd(SpeakerPortraitImage)
                    .Play(mapping.PortraitAnimation, useUnscaledTime);
                TryFadeIn(SpeakerPortraitImage);
            }

            var fullBodyImage = GetSpriteSlotImage(displayOptions?.DisplaySlot ?? string.Empty, index);
            if (fullBodyImage && mapping.FullBodyAnimation is { IsConfigured: true })
            {
                fullBodyImage.rectTransform.localScale = new Vector3(
                    displayOptions.FlipFullBodyX ? -displayOptions.FullBodyScale.x : displayOptions.FullBodyScale.x,
                    displayOptions.FlipFullBodyY ? -displayOptions.FullBodyScale.y : displayOptions.FullBodyScale.y,
                    displayOptions.FullBodyScale.z);
                fullBodyImage.gameObject.SetActive(true);
                ConvoCoreAnimatedPortraitPlayer.GetOrAdd(fullBodyImage)
                    .Play(mapping.FullBodyAnimation, useUnscaledTime);
                TryFadeIn(fullBodyImage);
            }
        }

        /// <summary>
        /// Returns the full-body sprite Image for a slot. Looks up by slot name in
        /// <see cref="ConvoCoreUIFoundation.DisplaySlots"/> (via <see cref="Image"/> on the
        /// <see cref="ConvoCoreUIFoundation.DisplaySlotDefinition.SlotObject"/>), then falls back
        /// to index.
        /// </summary>
        private Image GetSpriteSlotImage(string displaySlot, int index)
        {
            if (!string.IsNullOrEmpty(displaySlot))
            {
                var def = DisplaySlots.FirstOrDefault(s => s?.SlotName == displaySlot);
                var img = def?.SlotObject?.GetComponent<Image>();
                if (img != null) return img;
            }

            if (index >= 0 && index < DisplaySlots.Count)
                return DisplaySlots[index]?.SlotObject?.GetComponent<Image>();

            return null;
        }

        private void TryFadeIn(Graphic graphic) => graphic.GetComponent<IConvoCoreFadeIn>()?.FadeIn();

        private void HideAllSpriteImages()
        {
            if (SpeakerPortraitImage)
            {
                // Stop any animated playback so Image.enabled is restored and hosted
                // animator children deactivate before the next line renders.
                ConvoCoreAnimatedPortraitPlayer.StopOn(SpeakerPortraitImage.gameObject);
                SpeakerPortraitImage.gameObject.SetActive(false);
            }

            foreach (var slot in DisplaySlots)
            {
                var slotImage = slot?.SlotObject?.GetComponent<Image>();
                if (slotImage == null) continue;
                ConvoCoreAnimatedPortraitPlayer.StopOn(slotImage.gameObject);
                slotImage.gameObject.SetActive(false);
            }
        }

        private CharacterRepresentationBase GetRepresentationFromData(ConvoCoreConversationData convoData,
            ConvoCoreConversationData.CharacterRepresentationData data)
        {
            if (!string.IsNullOrEmpty(data.SelectedCharacterID))
            {
                var profile = convoData.ConversationParticipantProfiles.FirstOrDefault(p =>
                    p.CharacterID == data.SelectedCharacterID);
                return profile?.GetRepresentation(data.SelectedRepresentationName);
            }
            return data.SelectedRepresentation;
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
            AdvanceDialogueAction?.Disable();
            PreviousDialogueAction?.Disable();
            _committedLineIndices.Clear();
        }
    }
}