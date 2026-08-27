using System;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    /// <summary>
    /// ScriptableObject that bridges <see cref="ConvoCoreLanguageManager"/> with the save system.
    /// Exports the current language setting to a <see cref="ConvoCoreSettingsSnapshot"/> on save,
    /// and restores it on load via <see cref="ConvoCoreSaveManager"/>.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SaveSystem_1_1ConvoSettingsState.html")]
[CreateAssetMenu(fileName = "NewConvoSettingsState", menuName = "ConvoCore/Runtime/Convo Settings State")]
    public class ConvoSettingsState : ScriptableObject
    {
        public ConvoCoreSettingsSnapshot ExportSnapshot()
        {
            var snapshot = new ConvoCoreSettingsSnapshot
            {
                SchemaVersion = "1.0",
                SaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var langManager = ConvoCoreLanguageManager.Instance;
            if (langManager != null)
                snapshot.SelectedLanguage = langManager.CurrentLanguage;

            return snapshot;
        }

        public void RestoreFromSnapshot(ConvoCoreSettingsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[ConvoSettingsState] Cannot restore from null snapshot.");
                return;
            }

            if (!string.IsNullOrEmpty(snapshot.SelectedLanguage))
            {
                var langManager = ConvoCoreLanguageManager.Instance;
                if (langManager != null)
                    langManager.SetLanguage(snapshot.SelectedLanguage);
                else
                    Debug.LogWarning("[ConvoSettingsState] ConvoCoreLanguageManager instance not available.");
            }
        }

        public void ResetToDefaults()
        {
            var langManager = ConvoCoreLanguageManager.Instance;
            if (langManager != null)
            {
                var supported = langManager.GetSupportedLanguages();
                if (supported != null && supported.Count > 0)
                    langManager.SetLanguage(supported[0]);
            }
        }
    }
}