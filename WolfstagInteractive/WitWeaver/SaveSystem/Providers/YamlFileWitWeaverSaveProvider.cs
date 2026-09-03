// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System.IO;
using UnityEngine;
using YamlDotNet.Serialization;

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1SaveSystem_1_1YamlFileWitWeaverSaveProvider.html")]
    public class YamlFileWitWeaverSaveProvider : IWitWeaverSaveProvider
    {
        private readonly string _basePath;
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        public YamlFileWitWeaverSaveProvider(string subdirectory = "WitWeaverSaves")
        {
            _basePath = Path.Combine(Application.persistentDataPath, subdirectory);
            _serializer = new SerializerBuilder().Build();
            _deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        }

        private string GetFilePath(string key)
        {
            return Path.Combine(_basePath, key + ".witweaver.yml");
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
            var yaml = _serializer.Serialize(snapshot);
            File.WriteAllText(GetFilePath(saveSlot), yaml);
        }

        public WitWeaverGameSnapshot Load(string saveSlot)
        {
            var path = GetFilePath(saveSlot);
            if (!File.Exists(path))
                return null;

            var yaml = File.ReadAllText(path);
            return _deserializer.Deserialize<WitWeaverGameSnapshot>(yaml);
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
            var yaml = _serializer.Serialize(snapshot);
            File.WriteAllText(GetFilePath(key), yaml);
        }

        public WitWeaverSettingsSnapshot LoadSettings(string key)
        {
            var path = GetFilePath(key);
            if (!File.Exists(path))
                return null;

            var yaml = File.ReadAllText(path);
            return _deserializer.Deserialize<WitWeaverSettingsSnapshot>(yaml);
        }
    }
}