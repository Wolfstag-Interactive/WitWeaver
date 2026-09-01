using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverYamlLoader.html")]
    public static class WitWeaverYamlLoader
    {
        private static WitWeaverSettings _settings;

        /// <summary>
        /// The active settings used to resolve the source order. Resolves lazily through
        /// WitWeaverSettings.Instance (Resources.Load in builds) when not assigned.
        /// Assign at boot to override, e.g. with settings delivered as downloadable content.
        /// </summary>
        public static WitWeaverSettings Settings
        {
            get => _settings != null ? _settings : WitWeaverSettings.Instance;
            set => _settings = value;
        }

        // ------------------- Public entry points -------------------

        // Simple synchronous path (what import/init uses)
        public static string Load(WitWeaverConversationData data)
        {
            return LoadInternal(data);
        }

        // Task wrapper kept for API compatibility; both sources resolve synchronously
        public static Task<string> LoadAsync(WitWeaverConversationData data)
        {
            return Task.FromResult(LoadInternal(data));
        }

        // Coroutine wrapper kept for API compatibility; both sources resolve synchronously
        public static IEnumerator LoadCoroutine(WitWeaverConversationData data, Action<string> onDone)
        {
            onDone?.Invoke(LoadInternal(data));
            yield break;
        }

        // ------------------- Core -------------------

        static string LoadInternal(WitWeaverConversationData data)
        {
            var settings = Settings;
            var order = settings?.SourceOrder ?? new[]
            {
                TextSourceKind.AssignedTextAsset,
                TextSourceKind.Persistent
            };

            foreach (var src in order)
            {
                // Explicit comparisons rather than a switch: assets serialized before the
                // Addressables/Resources kinds were removed may hold out-of-range enum values,
                // which are skipped here.
                if (src == TextSourceKind.AssignedTextAsset)
                {
                    if (data.ConversationYaml && !string.IsNullOrEmpty(data.ConversationYaml.text))
                        return data.ConversationYaml.text;
                }
                else if (src == TextSourceKind.Persistent)
                {
                    if (data.AllowPersistentOverrides && TryReadPersistent(data, out var pText))
                        return pText;
                }
            }

            if (settings?.VerboseLogs == true)
                Debug.LogWarning($"WitWeaver: YAML not found via [{string.Join(", ", order)}] for FilePath='{data.FilePath}'.");
            return null;
        }

        // ------------------- Helpers -------------------

        static bool TryReadPersistent(WitWeaverConversationData data, out string text)
        {
            string rel = (data.FilePath ?? "").Replace('/', Path.DirectorySeparatorChar);
            var baseDir = Path.Combine(Application.persistentDataPath, "WitWeaver", "Dialogue");
            var p1 = Path.Combine(baseDir, rel + ".yml");
            var p2 = Path.Combine(baseDir, rel + ".yaml");
            if (File.Exists(p1)) { text = File.ReadAllText(p1); return true; }
            if (File.Exists(p2)) { text = File.ReadAllText(p2); return true; }
            text = null; return false;
        }
    }
}
