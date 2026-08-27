using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1SaveSystem_1_1WitWeaverSaveManager.html")]
[CreateAssetMenu(fileName = "NewSaveManager", menuName = "WitWeaver/Runtime/Save Manager")]
    public class WitWeaverSaveManager : ScriptableObject
    {
        public WitWeaverVariableStore VariableStore;
        public WitWeaverSettingsState SettingsState;

        [Header("Provider")]
        [SerializeField] private bool _useYaml = true;

        [Header("Defaults")]
        [SerializeField] private string _defaultSlot = "default";

        private IWitWeaverSaveProvider _provider;
        private List<ConversationSnapshot> _conversationSnapshots = new List<ConversationSnapshot>();

        public bool IsInitialized { get; private set; }
        public IWitWeaverSaveProvider Provider => _provider;

        // ----- Events -----

        public Action OnInitialized;
        public Action<string> OnSaveCompleted;
        public Action<string> OnLoadCompleted;
        public Action OnSettingsSaved;
        public Action OnSettingsLoaded;
        public Action<string, WitWeaverGameSnapshot> OnSnapshotAssembled;

        // ----- Initialization -----

        public void Initialize()
        {
            if (_provider == null)
            {
                _provider = _useYaml
                    ? (IWitWeaverSaveProvider)new YamlFileWitWeaverSaveProvider()
                    : new JsonFileWitWeaverSaveProvider();
            }

            IsInitialized = true;
            OnInitialized?.Invoke();
        }

        public void SetProvider(IWitWeaverSaveProvider provider)
        {
            _provider = provider;
        }

        // ----- Conversation Snapshot Registry -----

        public void RegisterConversationSnapshot(ConversationSnapshot snapshot)
        {
            if (snapshot == null) return;

            for (int i = 0; i < _conversationSnapshots.Count; i++)
            {
                if (_conversationSnapshots[i].ConversationId == snapshot.ConversationId)
                {
                    _conversationSnapshots[i] = snapshot;
                    return;
                }
            }
            _conversationSnapshots.Add(snapshot);
        }

        public ConversationSnapshot GetConversationSnapshot(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId)) return null;

            for (int i = 0; i < _conversationSnapshots.Count; i++)
            {
                if (_conversationSnapshots[i].ConversationId == conversationId)
                    return _conversationSnapshots[i];
            }
            return null;
        }

        // ----- Settings Methods -----

        public void SaveSettings()
        {
            if (_provider == null)
            {
                Debug.LogWarning("[WitWeaverSaveManager] Provider is null. Call Initialize() first.");
                return;
            }

            if (SettingsState == null)
            {
                Debug.LogWarning("[WitWeaverSaveManager] SettingsState is not assigned.");
                return;
            }

            var snapshot = SettingsState.ExportSnapshot();
            _provider.SaveSettings(WitWeaverKeys.Settings, snapshot);
            OnSettingsSaved?.Invoke();
        }

        public void LoadSettings()
        {
            if (_provider == null)
            {
                Debug.LogWarning("[WitWeaverSaveManager] Provider is null. Call Initialize() first.");
                return;
            }

            if (SettingsState == null)
            {
                Debug.LogWarning("[WitWeaverSaveManager] SettingsState is not assigned.");
                return;
            }

            var snapshot = _provider.LoadSettings(WitWeaverKeys.Settings);
            if (snapshot != null)
            {
                snapshot = WitWeaverSnapshotMigrator.Migrate(snapshot);
                SettingsState.RestoreFromSnapshot(snapshot);
            }

            OnSettingsLoaded?.Invoke();
        }

        public void InitializeSettings()
        {
            LoadSettings();

            // If no settings were saved, fall back to first supported language
            var langManager = WitWeaverLanguageManager.Instance;
            if (langManager != null)
            {
                var supported = langManager.GetSupportedLanguages();
                if (supported != null && supported.Count > 0)
                {
                    if (string.IsNullOrEmpty(langManager.CurrentLanguage))
                        langManager.SetLanguage(supported[0]);
                }
            }
        }

        // ----- Game Save Methods -----

        public void Save(string saveSlot)
        {
            if (_provider == null)
            {
                Debug.LogWarning("[WitWeaverSaveManager] Provider is null. Call Initialize() first.");
                return;
            }

            var settingsKey = WitWeaverKeys.Settings;
            if (saveSlot == settingsKey || WitWeaverKeys.GameSlot(saveSlot) == settingsKey)
            {
                Debug.LogWarning($"[WitWeaverSaveManager] Save slot '{saveSlot}' conflicts with the reserved settings key. Aborting save.");
                return;
            }

            var snapshot = AssembleGameSnapshot();
            var key = WitWeaverKeys.GameSlot(saveSlot);
            OnSnapshotAssembled?.Invoke(saveSlot, snapshot);
            _provider.Save(key, snapshot);
            OnSaveCompleted?.Invoke(saveSlot);
        }

        public void Load(string saveSlot)
        {
            if (_provider == null)
            {
                Debug.LogWarning("[WitWeaverSaveManager] Provider is null. Call Initialize() first.");
                return;
            }

            var key = WitWeaverKeys.GameSlot(saveSlot);
            var snapshot = _provider.Load(key);
            if (snapshot == null)
            {
                Debug.LogWarning($"[WitWeaverSaveManager] No save found for slot '{saveSlot}'.");
                return;
            }

            snapshot = WitWeaverSnapshotMigrator.Migrate(snapshot);
            RestoreFromSnapshot(snapshot);
            OnLoadCompleted?.Invoke(saveSlot);
        }

        public void SaveToDefaultSlot()
        {
            Save(_defaultSlot);
        }

        public void LoadFromDefaultSlot()
        {
            Load(_defaultSlot);
        }

        public bool HasSave(string saveSlot)
        {
            if (_provider == null)
            {
                Debug.LogWarning("[WitWeaverSaveManager] Provider is null. Call Initialize() first.");
                return false;
            }
            return _provider.HasSave(WitWeaverKeys.GameSlot(saveSlot));
        }

        public void DeleteSave(string saveSlot)
        {
            if (_provider == null)
            {
                Debug.LogWarning("[WitWeaverSaveManager] Provider is null. Call Initialize() first.");
                return;
            }
            _provider.Delete(WitWeaverKeys.GameSlot(saveSlot));
        }

        // ----- Raw Access -----

        public WitWeaverGameSnapshot GetGameSnapshot()
        {
            return AssembleGameSnapshot();
        }

        public void RestoreGameSnapshot(WitWeaverGameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[WitWeaverSaveManager] Cannot restore null snapshot.");
                return;
            }
            RestoreFromSnapshot(snapshot);
        }

        public WitWeaverSettingsSnapshot GetSettingsSnapshot()
        {
            if (SettingsState == null)
            {
                Debug.LogWarning("[WitWeaverSaveManager] SettingsState is not assigned.");
                return null;
            }
            return SettingsState.ExportSnapshot();
        }

        public void RestoreSettingsSnapshot(WitWeaverSettingsSnapshot snapshot)
        {
            if (SettingsState == null)
            {
                Debug.LogWarning("[WitWeaverSaveManager] SettingsState is not assigned.");
                return;
            }

            if (snapshot == null)
            {
                Debug.LogWarning("[WitWeaverSaveManager] Cannot restore null settings snapshot.");
                return;
            }

            SettingsState.RestoreFromSnapshot(snapshot);
        }

        // ----- Internal -----

        public int ConversationSnapshotCount => _conversationSnapshots.Count;

        private WitWeaverGameSnapshot AssembleGameSnapshot()
        {
            var snapshot = new WitWeaverGameSnapshot
            {
                SchemaVersion = WitWeaverGameSnapshot.CurrentSchemaVersion,
                SaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            if (VariableStore != null)
                snapshot.GlobalVariables = VariableStore.ExportByScope(WitWeaverVariableScope.Global);

            snapshot.Conversations = new List<ConversationSnapshot>(_conversationSnapshots);

            return snapshot;
        }

        private void RestoreFromSnapshot(WitWeaverGameSnapshot snapshot)
        {
            if (VariableStore != null)
            {
                VariableStore.ClearByScope(WitWeaverVariableScope.Global);
                if (snapshot.GlobalVariables != null)
                    VariableStore.RestoreEntries(snapshot.GlobalVariables);
            }

            _conversationSnapshots.Clear();
            if (snapshot.Conversations != null)
            {
                for (int i = 0; i < snapshot.Conversations.Count; i++)
                    _conversationSnapshots.Add(snapshot.Conversations[i]);
            }
        }
    }
}