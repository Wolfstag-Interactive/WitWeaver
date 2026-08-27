using System.IO;
using UnityEngine;
using YamlDotNet.Serialization;

namespace WolfstagInteractive.ConvoCore.SaveSystem
{
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SaveSystem_1_1YamlFileConvoSaveProvider.html")]
    public class YamlFileConvoSaveProvider : IConvoSaveProvider
    {
        private readonly string _basePath;
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        public YamlFileConvoSaveProvider(string subdirectory = "ConvoCoreSaves")
        {
            _basePath = Path.Combine(Application.persistentDataPath, subdirectory);
            _serializer = new SerializerBuilder().Build();
            _deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        }

        private string GetFilePath(string key)
        {
            return Path.Combine(_basePath, key + ".convo.yml");
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
            var yaml = _serializer.Serialize(snapshot);
            File.WriteAllText(GetFilePath(saveSlot), yaml);
        }

        public ConvoCoreGameSnapshot Load(string saveSlot)
        {
            var path = GetFilePath(saveSlot);
            if (!File.Exists(path))
                return null;

            var yaml = File.ReadAllText(path);
            return _deserializer.Deserialize<ConvoCoreGameSnapshot>(yaml);
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
            var yaml = _serializer.Serialize(snapshot);
            File.WriteAllText(GetFilePath(key), yaml);
        }

        public ConvoCoreSettingsSnapshot LoadSettings(string key)
        {
            var path = GetFilePath(key);
            if (!File.Exists(path))
                return null;

            var yaml = File.ReadAllText(path);
            return _deserializer.Deserialize<ConvoCoreSettingsSnapshot>(yaml);
        }
    }
}