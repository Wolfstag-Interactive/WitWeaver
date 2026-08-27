using System.IO;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1SaveSystem_1_1JsonFileWitWeaverSaveProvider.html")]
    public class JsonFileWitWeaverSaveProvider : IWitWeaverSaveProvider
    {
        private readonly string _basePath;

        public JsonFileWitWeaverSaveProvider(string subdirectory = "WitWeaverSaves")
        {
            _basePath = Path.Combine(Application.persistentDataPath, subdirectory);
        }

        private string GetFilePath(string key)
        {
            return Path.Combine(_basePath, key + ".witweaver.json");
        }

        private void EnsureDirectory()
        {
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
        }

        public void Save(string saveSlot, WitWeaverGameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[WitWeaverSave] Cannot save null snapshot.");
                return;
            }

            EnsureDirectory();
            var json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(GetFilePath(saveSlot), json);
        }

        public WitWeaverGameSnapshot Load(string saveSlot)
        {
            var path = GetFilePath(saveSlot);
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<WitWeaverGameSnapshot>(json);
        }

        public bool HasSave(string saveSlot)
        {
            return File.Exists(GetFilePath(saveSlot));
        }

        public void Delete(string saveSlot)
        {
            var path = GetFilePath(saveSlot);
            if (File.Exists(path))
                File.Delete(path);
        }

        public void SaveSettings(string key, WitWeaverSettingsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[WitWeaverSave] Cannot save null settings snapshot.");
                return;
            }

            EnsureDirectory();
            var json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(GetFilePath(key), json);
        }

        public WitWeaverSettingsSnapshot LoadSettings(string key)
        {
            var path = GetFilePath(key);
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<WitWeaverSettingsSnapshot>(json);
        }
    }
}