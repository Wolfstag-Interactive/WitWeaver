using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverDialogueHistoryUI.html")]
[AddComponentMenu("WitWeaver/UI/WitWeaver Dialogue History UI")]
    public class WitWeaverDialogueHistoryUI : MonoBehaviour
    {
        [Header("Renderer Configuration")]
        [SerializeField] private WitWeaverSettings witWeaverSettings;
        [SerializeField] private string selectedProfileName;

        [Header("General Settings")]
        public int maxEntries = 100;

        // Internal state
        private readonly List<DialogueHistoryEntry> _entries = new();
        private IWitWeaverHistoryRenderer _renderer;
        private WitWeaverHistoryRendererProfile _activeProfile;
        private bool _isInitialized;

        /// <summary>
        /// Initializes the dialogue history renderer with an external context
        /// (typically provided by the Sample UI).
        /// </summary>
        public void InitializeRenderer(DialogueHistoryRendererContext context)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[WitWeaver] DialogueHistoryUI already initialized — ignoring.");
                return;
            }

            if (witWeaverSettings == null)
            {
                Debug.LogWarning("[WitWeaver] Missing settings reference.");
                return;
            }
            _activeProfile = witWeaverSettings.GetRendererProfile(selectedProfileName);
            if (_activeProfile == null)
            {
                Debug.LogWarning("[WitWeaver] No renderer profile found.");
                return;
            }

            _renderer = WitWeaverHistoryRendererRegistry.CreateInstance(_activeProfile.RendererName);

            if (_renderer != null)
            {
                // Inject defaults if caller didn’t specify them
                if (context.MaxEntries <= 0)
                    context.MaxEntries = maxEntries;

                _renderer.Initialize(context);
                _isInitialized = true;
            }
            else
            {
                Debug.LogWarning($"[WitWeaver] Could not instantiate renderer '{_activeProfile.RendererName}'.");
            }
        }

        /// <summary>
        /// Adds a new line to the dialogue history and forwards it to the renderer.
        /// </summary>
        public void AddLine(string speaker, string text, Color speakerColor)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[WitWeaver] DialogueHistoryUI not initialized — call InitializeRenderer first.");
                return;
            }

            var entry = new DialogueHistoryEntry { Speaker = speaker, Text = text,SpeakerTextColor = speakerColor};

            _entries.Add(entry);
            if (_entries.Count > maxEntries)
                _entries.RemoveAt(0);

            _renderer?.RenderEntry(entry);
        }

        /// <summary>
        /// Clears all dialogue history lines.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _renderer?.Clear();
        }

        private void Update()
        {
            if (_isInitialized)
                _renderer?.Tick(Time.deltaTime);
        }
    }
}