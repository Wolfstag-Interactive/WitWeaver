using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SaveSystem_1_1ConvoCoreSaveManager.html")]
[CreateAssetMenu(fileName = "NewSaveManager", menuName = "ConvoCore/Runtime/Save Manager")]
    public class ConvoCoreSaveManager : ScriptableObject
    {
        public ConvoVariableStore VariableStore;
        public ConvoSettingsState SettingsState;

        [Header("Provider")]
        [SerializeField] private bool _useYaml = true;

        [Header("Defaults")]
        [SerializeField] private string _defaultSlot = "default";

        private IConvoSaveProvider _provider;
        private List<ConversationSnapshot> _conversationSnapshots = new List<ConversationSnapshot>();

        public bool IsInitialized { get; private set; }
        public IConvoSaveProvider Provider => _provider;

        // ----- Events -----

        public Action OnInitialized;
        public Action<string> OnSaveCompleted;
        public Action<string> OnLoadCompleted;
        public Action OnSettingsSaved;
        public Action OnSettingsLoaded;
        public Action<string, ConvoCoreGameSnapshot> OnSnapshotAssembled;

        // ----- Initialization -----

        public void Initialize()
        {
            if (_provider == null)
            {
                _provider = _useYaml
                    ? (IConvoSaveProvider)new YamlFileConvoSaveProvider()
                    : new JsonFileConvoSaveProvider();
            }

            IsInitialized = true;
            OnInitialized?.Invoke();
        }

        public void SetProvider(IConvoSaveProvider provider)
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
                Debug.LogWarning("[ConvoCoreSaveManager] Provider is null. Call Initialize() first.");
                return;
            }

            if (SettingsState == null)
            {
                Debug.LogWarning("[ConvoCoreSaveManager] SettingsState is not assigned.");
                return;
            }

            var snapshot = SettingsState.ExportSnapshot();
            _provider.SaveSettings(ConvoCoreKeys.Settings, snapshot);
            OnSettingsSaved?.Invoke();
        }

        public void LoadSettings()
        {
            if (_provider == null)
            {
                Debug.LogWarning("[ConvoCoreSaveManager] Provider is null. Call Initialize() first.");
                return;
            }

            if (SettingsState == null)
            {
                Debug.LogWarning("[ConvoCoreSaveManager] SettingsState is not assigned.");
                return;
            }

            var snapshot = _provider.LoadSettings(ConvoCoreKeys.Settings);
            if (snapshot != null)
            {
                snapshot = ConvoCoreSnapshotMigrator.Migrate(snapshot);
                SettingsState.RestoreFromSnapshot(snapshot);
            }

            OnSettingsLoaded?.Invoke();
        }

        public void InitializeSettings()
        {
            LoadSettings();

            // If no settings were saved, fall back to first supported language
            var langManager = ConvoCoreLanguageManager.Instance;
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
                Debug.LogWarning("[ConvoCoreSaveManager] Provider is null. Call Initialize() first.");
                return;
            }

            var settingsKey = ConvoCoreKeys.Settings;
            if (saveSlot == settingsKey || ConvoCoreKeys.GameSlot(saveSlot) == settingsKey)
            {
                Debug.LogWarning($"[ConvoCoreSaveManager] Save slot '{saveSlot}' conflicts with the reserved settings key. Aborting save.");
                return;
            }

            var snapshot = AssembleGameSnapshot();
            var key = ConvoCoreKeys.GameSlot(saveSlot);
            OnSnapshotAssembled?.Invoke(saveSlot, snapshot);
            _provider.Save(key, snapshot);
            OnSaveCompleted?.Invoke(saveSlot);
        }

        public void Load(string saveSlot)
        {
            if (_provider == null)
            {
                Debug.LogWarning("[ConvoCoreSaveManager] Provider is null. Call Initialize() first.");
                return;
            }

            var key = ConvoCoreKeys.GameSlot(saveSlot);
            var snapshot = _provider.Load(key);
            if (snapshot == null)
            {
                Debug.LogWarning($"[ConvoCoreSaveManager] No save found for slot '{saveSlot}'.");
                return;
            }

            snapshot = ConvoCoreSnapshotMigrator.Migrate(snapshot);
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
                Debug.LogWarning("[ConvoCoreSaveManager] Provider is null. Call Initialize() first.");
                return false;
            }
            return _provider.HasSave(ConvoCoreKeys.GameSlot(saveSlot));
        }

        public void DeleteSave(string saveSlot)
        {
            if (_provider == null)
            {
                Debug.LogWarning("[ConvoCoreSaveManager] Provider is null. Call Initialize() first.");
                return;
            }
            _provider.Delete(ConvoCoreKeys.GameSlot(saveSlot));
        }

        // ----- Raw Access -----

        public ConvoCoreGameSnapshot GetGameSnapshot()
        {
            return AssembleGameSnapshot();
        }

        public void RestoreGameSnapshot(ConvoCoreGameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[ConvoCoreSaveManager] Cannot restore null snapshot.");
                return;
            }
            RestoreFromSnapshot(snapshot);
        }

        public ConvoCoreSettingsSnapshot GetSettingsSnapshot()
        {
            if (SettingsState == null)
            {
                Debug.LogWarning("[ConvoCoreSaveManager] SettingsState is not assigned.");
                return null;
            }
            return SettingsState.ExportSnapshot();
        }

        public void RestoreSettingsSnapshot(ConvoCoreSettingsSnapshot snapshot)
        {
            if (SettingsState == null)
            {
                Debug.LogWarning("[ConvoCoreSaveManager] SettingsState is not assigned.");
                return;
            }

            if (snapshot == null)
            {
                Debug.LogWarning("[ConvoCoreSaveManager] Cannot restore null settings snapshot.");
                return;
            }

            SettingsState.RestoreFromSnapshot(snapshot);
        }

        // ----- Internal -----

        public int ConversationSnapshotCount => _conversationSnapshots.Count;

        private ConvoCoreGameSnapshot AssembleGameSnapshot()
        {
            var snapshot = new ConvoCoreGameSnapshot
            {
                SchemaVersion = ConvoCoreGameSnapshot.CurrentSchemaVersion,
                SaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            if (VariableStore != null)
                snapshot.GlobalVariables = VariableStore.ExportByScope(ConvoVariableScope.Global);

            snapshot.Conversations = new List<ConversationSnapshot>(_conversationSnapshots);

            return snapshot;
        }

        private void RestoreFromSnapshot(ConvoCoreGameSnapshot snapshot)
        {
            if (VariableStore != null)
            {
                VariableStore.ClearByScope(ConvoVariableScope.Global);
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