using System.IO;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.SaveSystem
{
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SaveSystem_1_1JsonFileConvoSaveProvider.html")]
    public class JsonFileConvoSaveProvider : IConvoSaveProvider
    {
        private readonly string _basePath;

        public JsonFileConvoSaveProvider(string subdirectory = "ConvoCoreSaves")
        {
            _basePath = Path.Combine(Application.persistentDataPath, subdirectory);
        }

        private string GetFilePath(string key)
        {
            return Path.Combine(_basePath, key + ".convo.json");
        }

        private void EnsureDirectory()
        {
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
        }

        public void Save(string saveSlot, ConvoCoreGameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[ConvoCoreSave] Cannot save null snapshot.");
                return;
            }

            EnsureDirectory();
            var json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(GetFilePath(saveSlot), json);
        }

        public ConvoCoreGameSnapshot Load(string saveSlot)
        {
            var path = GetFilePath(saveSlot);
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<ConvoCoreGameSnapshot>(json);
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

        public void SaveSettings(string key, ConvoCoreSettingsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[ConvoCoreSave] Cannot save null settings snapshot.");
                return;
            }

            EnsureDirectory();
            var json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(GetFilePath(key), json);
        }

        public ConvoCoreSettingsSnapshot LoadSettings(string key)
        {
            var path = GetFilePath(key);
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<ConvoCoreSettingsSnapshot>(json);
        }
    }
}