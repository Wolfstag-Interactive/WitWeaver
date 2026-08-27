using System;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    /// <summary>
    /// ScriptableObject that bridges <see cref="WitWeaverLanguageManager"/> with the save system.
    /// Exports the current language setting to a <see cref="WitWeaverSettingsSnapshot"/> on save,
    /// and restores it on load via <see cref="WitWeaverSaveManager"/>.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1SaveSystem_1_1WitWeaverSettingsState.html")]
[CreateAssetMenu(fileName = "NewWitWeaverSettingsState", menuName = "WitWeaver/Runtime/WitWeaver Settings State")]
    public class WitWeaverSettingsState : ScriptableObject
    {
        public WitWeaverSettingsSnapshot ExportSnapshot()
        {
            var snapshot = new WitWeaverSettingsSnapshot
            {
                SchemaVersion = "1.0",
                SaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var langManager = WitWeaverLanguageManager.Instance;
            if (langManager != null)
                snapshot.SelectedLanguage = langManager.CurrentLanguage;

            return snapshot;
        }

        public void RestoreFromSnapshot(WitWeaverSettingsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[WitWeaverSettingsState] Cannot restore from null snapshot.");
                return;
            }

            if (!string.IsNullOrEmpty(snapshot.SelectedLanguage))
            {
                var langManager = WitWeaverLanguageManager.Instance;
                if (langManager != null)
                    langManager.SetLanguage(snapshot.SelectedLanguage);
                else
                    Debug.LogWarning("[WitWeaverSettingsState] WitWeaverLanguageManager instance not available.");
            }
        }

        public void ResetToDefaults()
        {
            var langManager = WitWeaverLanguageManager.Instance;
            if (langManager != null)
            {
                var supported = langManager.GetSupportedLanguages();
                if (supported != null && supported.Count > 0)
                    langManager.SetLanguage(supported[0]);
            }
        }
    }
}