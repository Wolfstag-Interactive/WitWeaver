using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    /// <summary>
    /// Attach alongside a <see cref="ConvoCore"/> component to persist and restore
    /// conversation state (active line, visited lines, conversation-scoped variables).
    ///
    /// Implements <see cref="IConvoStartContextProvider"/> so that
    /// <see cref="ConvoCore.StartConversation"/> automatically picks up the restore
    /// context from this component.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SaveSystem_1_1ConvoCoreConversationSaveManager.html")]
    public class ConvoCoreConversationSaveManager : MonoBehaviour, IConvoStartContextProvider
    {
        // ── Conversation identity ─────────────────────────────────────────────

        [Tooltip("Assign a single ConvoCoreConversationData for direct-conversation mode.")]
        public ConvoCoreConversationData DirectConversation;

        [Tooltip("Assign a ConversationContainer to pick a conversation by index.")]
        public ConversationContainer ConversationContainer;

        [Tooltip("Index into ConversationContainer.Conversations. Ignored when DirectConversation is set.")]
        public int ActiveConversationIndex;

        // ── References ───────────────────────────────────────────────────────

        public ConvoCoreSaveManager SaveManager;
        public ConvoVariableStore    VariableStore;

        // ── Start behaviour ──────────────────────────────────────────────────

        [Tooltip("How to start the conversation when a saved snapshot is found.\n" +
                 "Fresh: ignore the snapshot.\n" +
                 "Resume: restore active line + visited history.\n" +
                 "Restart: restore variables but start from the first line.")]
        [SerializeField] private ConvoStartMode _defaultStartMode = ConvoStartMode.Fresh;

        [Tooltip("Controls what happens when RestoreConversationSnapshot is called manually " +
                 "and the snapshot has not already been auto-applied.")]
        [SerializeField] private ConvoRestoreBehavior _restoreBehavior = ConvoRestoreBehavior.ResumeFromActiveLine;

        // ── Auto-commit ──────────────────────────────────────────────────────

        [Tooltip("Write the snapshot to the Save Manager every time the conversation starts.")]
        [SerializeField] private bool _autoCommitOnStart;

        [Tooltip("Write the snapshot when the conversation ends or is stopped.")]
        [SerializeField] private bool _autoCommitOnEnd;

        [Tooltip("Write the snapshot each time a line finishes.")]
        [SerializeField] private bool _autoCommitOnLineComplete;

        [Tooltip("Write the snapshot each time a player choice is made.")]
        [SerializeField] private bool _autoCommitOnChoiceMade;

        // ── Auto-restore ─────────────────────────────────────────────────────

        [Tooltip("Attempt to restore from a saved snapshot in Awake.")]
        [SerializeField] private bool _autoRestoreOnAwake;

        [Tooltip("Attempt to restore from a saved snapshot in Start.")]
        [SerializeField] private bool _autoRestoreOnStart;

        // ── Runtime state ────────────────────────────────────────────────────

        private ConvoCore              _runner;
        private string                 _activeLineId;
        private bool                   _isComplete;
        private readonly HashSet<string> _visitedLineIds = new HashSet<string>();

        // Context prepared by TryAutoRestore, consumed by GetStartContext
        private bool             _contextReady;
        private ConvoStartContext _pendingContext;

        public bool     IsDirty        { get; private set; }
        public DateTime LastCommitTime { get; private set; }

        // ── Events ───────────────────────────────────────────────────────────

        /// <summary>Fired when <see cref="_restoreBehavior"/> is
        /// <see cref="ConvoRestoreBehavior.AskViaEvent"/>. Subscribe to decide
        /// whether to resume or restart, then call <see cref="ResumeFromSnapshot"/>
        /// or <see cref="RestartWithRestoredVariables"/>.</summary>
        public event Action<ConversationSnapshot> OnRestoreDecisionRequired;

        // ── Read-only state accessors (for editor + external code) ────────────

        public string ActiveLineId            => _activeLineId;
        public bool   IsComplete              => _isComplete;
        public int    VisitedLinesCount       => _visitedLineIds.Count;
        public int    ConversationVariablesCount =>
            VariableStore != null ? VariableStore.GetByScope(ConvoVariableScope.Conversation).Count : 0;

        // ── IConvoStartContextProvider ────────────────────────────────────────

        /// <summary>Called by <see cref="ConvoCore.StartConversation"/> via GetComponent.</summary>
        public ConvoStartContext GetStartContext()
        {
            return _contextReady
                ? _pendingContext
                : new ConvoStartContext { Mode = ConvoStartMode.Fresh };
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            _runner = GetComponent<ConvoCore>();
            if (_autoRestoreOnAwake) TryAutoRestore();
        }

        private void Start()
        {
            if (_autoRestoreOnStart) TryAutoRestore();
        }

        private void OnEnable()
        {
            if (_runner == null) _runner = GetComponent<ConvoCore>();
            if (_runner == null) return;

            _runner.OnConversationStarted += HandleConversationStarted;
            _runner.OnConversationEnded   += HandleConversationEnded;
            _runner.OnLineStarted         += HandleLineStarted;
            _runner.OnLineCompleted       += HandleLineCompleted;
            _runner.OnChoiceMade          += HandleChoiceMade;
        }

        private void OnDisable()
        {
            if (_runner == null) return;

            _runner.OnConversationStarted -= HandleConversationStarted;
            _runner.OnConversationEnded   -= HandleConversationEnded;
            _runner.OnLineStarted         -= HandleLineStarted;
            _runner.OnLineCompleted       -= HandleLineCompleted;
            _runner.OnChoiceMade          -= HandleChoiceMade;
        }

        // ── Restore ──────────────────────────────────────────────────────────

        /// <summary>Loads the saved snapshot and prepares a start context for the next
        /// <see cref="ConvoCore.StartConversation"/> call. Safe to call multiple times;
        /// does nothing when <see cref="SaveManager"/> is not initialized.</summary>
        public void TryAutoRestore()
        {
            if (SaveManager == null)
            {
                Debug.LogWarning("[ConvoCoreConversationSaveManager] SaveManager not assigned. Skipping restore.");
                return;
            }

            if (!SaveManager.IsInitialized)
            {
                Debug.LogWarning("[ConvoCoreConversationSaveManager] SaveManager is not yet initialized. Skipping restore.");
                return;
            }

            var data = GetActiveConversation();
            if (data == null)
            {
                Debug.LogWarning("[ConvoCoreConversationSaveManager] No active conversation to restore.");
                return;
            }

            var snapshot = SaveManager.GetConversationSnapshot(data.ConversationGuid);
            if (snapshot == null) return; // no saved state, start fresh

            if (_defaultStartMode == ConvoStartMode.Fresh) return; // user wants to ignore the snapshot

            if (_restoreBehavior == ConvoRestoreBehavior.AskViaEvent)
            {
                OnRestoreDecisionRequired?.Invoke(snapshot);
                return;
            }

            ApplySnapshot(snapshot, _defaultStartMode);
        }

        /// <summary>Call from an <see cref="OnRestoreDecisionRequired"/> handler to
        /// resume from the supplied snapshot's active line.</summary>
        public void ResumeFromSnapshot(ConversationSnapshot snapshot)
        {
            if (snapshot == null) return;
            ApplySnapshot(snapshot, ConvoStartMode.Resume);
        }

        /// <summary>Call from an <see cref="OnRestoreDecisionRequired"/> handler to
        /// restore variables from the snapshot but restart from line 0.</summary>
        public void RestartWithRestoredVariables(ConversationSnapshot snapshot)
        {
            if (snapshot == null) return;
            ApplySnapshot(snapshot, ConvoStartMode.Restart);
        }

        private void ApplySnapshot(ConversationSnapshot snapshot, ConvoStartMode mode)
        {
            // Restore conversation-scoped variables to the store
            if (VariableStore != null && snapshot.Variables != null)
                VariableStore.RestoreEntries(snapshot.Variables);

            // Update local tracking state
            _activeLineId = snapshot.ActiveLineId;
            _isComplete   = snapshot.IsComplete;
            _visitedLineIds.Clear();
            if (snapshot.VisitedLineIds != null)
                foreach (var id in snapshot.VisitedLineIds)
                    _visitedLineIds.Add(id);

            // Prepare the context that GetStartContext() will return
            switch (mode)
            {
                case ConvoStartMode.Resume:
                    _pendingContext = new ConvoStartContext
                    {
                        Mode          = ConvoStartMode.Resume,
                        StartLineId   = snapshot.ActiveLineId,
                        VisitedLineIds = snapshot.VisitedLineIds
                    };
                    _contextReady = true;
                    break;

                case ConvoStartMode.Restart:
                    _pendingContext = new ConvoStartContext { Mode = ConvoStartMode.Restart };
                    _contextReady = true;
                    break;
            }

            IsDirty = false;
        }

        // ── Commit ───────────────────────────────────────────────────────────

        /// <summary>Builds a snapshot from current state and registers it with the
        /// <see cref="SaveManager"/>. No-ops with a warning when references are missing.</summary>
        public void CommitSnapshot()
        {
            if (SaveManager == null)
            {
                Debug.LogWarning("[ConvoCoreConversationSaveManager] SaveManager not assigned. Skipping commit.");
                return;
            }

            var data = GetActiveConversation();
            if (data == null)
            {
                Debug.LogWarning("[ConvoCoreConversationSaveManager] No active conversation. Skipping commit.");
                return;
            }

            var snapshot = BuildSnapshot(data);
            SaveManager.RegisterConversationSnapshot(snapshot);
            IsDirty       = false;
            LastCommitTime = DateTime.Now;
            _contextReady  = false; // context has been consumed by a play session
        }

        /// <summary>Returns a snapshot of the current conversation state without committing.</summary>
        public ConversationSnapshot GetConversationSnapshot()
        {
            var data = GetActiveConversation();
            return data != null ? BuildSnapshot(data) : null;
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private ConvoCoreConversationData GetActiveConversation()
        {
            if (DirectConversation != null) return DirectConversation;

            if (ConversationContainer?.Conversations != null &&
                ActiveConversationIndex >= 0 &&
                ActiveConversationIndex < ConversationContainer.Conversations.Count)
            {
                return ConversationContainer.Conversations[ActiveConversationIndex]?.ConversationData;
            }

            return null;
        }

        private ConversationSnapshot BuildSnapshot(ConvoCoreConversationData data)
        {
            return new ConversationSnapshot
            {
                ConversationId = data.ConversationGuid,
                ActiveLineId   = _activeLineId,
                IsComplete     = _isComplete,
                SaveTimestamp  = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                VisitedLineIds = new List<string>(_visitedLineIds),
                Variables      = VariableStore?.ExportByScope(ConvoVariableScope.Conversation)
                                 ?? new List<ConvoVariableEntry>()
            };
        }

        // ── ConvoCore event handlers ─────────────────────────────────────────

        private void HandleConversationStarted()
        {
            _isComplete = false;
            IsDirty     = true;
            if (_autoCommitOnStart) CommitSnapshot();
        }

        private void HandleConversationEnded()
        {
            _isComplete = true;
            IsDirty     = true;
            if (_autoCommitOnEnd) CommitSnapshot();
        }

        private void HandleLineStarted(string lineId)
        {
            _activeLineId = lineId;
            _visitedLineIds.Add(lineId);
        }

        private void HandleLineCompleted(string lineId)
        {
            IsDirty = true;
            if (_autoCommitOnLineComplete) CommitSnapshot();
        }

        private void HandleChoiceMade(int choiceIndex)
        {
            IsDirty = true;
            if (_autoCommitOnChoiceMade) CommitSnapshot();
        }
    }
}