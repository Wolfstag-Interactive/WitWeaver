using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreYamlLoader.html")]
    public static class ConvoCoreYamlLoader
    {
        public static ConvoCoreSettings Settings; // assign once at boot (or Resources.Load in code)

        // ------------------- Public entry points -------------------

        // Simple synchronous path (what your current import/init uses)
        public static string Load(ConvoCoreConversationData data)
        {
            return LoadInternalSync(data);
        }

        // Async via Task (good for menu/boot flows; await it)
        public static async Task<string> LoadAsync(ConvoCoreConversationData data)
        {
            return await LoadInternalTaskAsync(data);
        }

        // Async via Coroutine
        public static IEnumerator LoadCoroutine(ConvoCoreConversationData data, Action<string> onDone)
        {
            return LoadInternalCoroutine(data, onDone);
        }

        // ------------------- Core (Sync) -------------------

        static string LoadInternalSync(ConvoCoreConversationData data)
        {
            var order = Settings?.SourceOrder ?? new[]
            {
                TextSourceKind.AssignedTextAsset,
                TextSourceKind.Persistent,
                TextSourceKind.Addressables,
                TextSourceKind.Resources
            };

            foreach (var src in order)
            {
                switch (src)
                {
                    case TextSourceKind.AssignedTextAsset:
                        if (data.ConversationYaml && !string.IsNullOrEmpty(data.ConversationYaml.text))
                            return data.ConversationYaml.text;
                        break;

                    case TextSourceKind.Persistent:
                        if (data.AllowPersistentOverrides && TryReadPersistent(data, out var pText))
                            return pText;
                        break;

                    case TextSourceKind.Addressables:
                        if (Settings != null && Settings.AddressablesEnabled && !string.IsNullOrWhiteSpace(data.FilePath))
                        {
                            var key = KeyFromFilePath(data.FilePath);
                            var text = TryLoadFromAddressablesSync(key); // uses WaitForCompletion
                            if (!string.IsNullOrEmpty(text)) return text;
                        }
                        break;

                    case TextSourceKind.Resources:
                        if (!string.IsNullOrWhiteSpace(data.FilePath))
                        {
                            var ta = Resources.Load<TextAsset>(data.FilePath);
                            if (ta) return ta.text;
                        }
                        break;
                }
            }

            if (Settings?.VerboseLogs == true)
                Debug.LogWarning($"ConvoCore: YAML not found via [{string.Join(", ", order)}] for FilePath='{data.FilePath}'.");
            return null;
        }

        // ------------------- Core (Task) -------------------

        static async Task<string> LoadInternalTaskAsync(ConvoCoreConversationData data)
        {
            var order = Settings?.SourceOrder ?? new[]
            {
                TextSourceKind.AssignedTextAsset,
                TextSourceKind.Persistent,
                TextSourceKind.Addressables,
                TextSourceKind.Resources
            };

            foreach (var src in order)
            {
                switch (src)
                {
                    case TextSourceKind.AssignedTextAsset:
                        if (data.ConversationYaml && !string.IsNullOrEmpty(data.ConversationYaml.text))
                            return data.ConversationYaml.text;
                        break;

                    case TextSourceKind.Persistent:
                        if (data.AllowPersistentOverrides && TryReadPersistent(data, out var pText))
                            return pText;
                        break;

                    case TextSourceKind.Addressables:
                        if (Settings != null && Settings.AddressablesEnabled && !string.IsNullOrWhiteSpace(data.FilePath))
                        {
                            var key = KeyFromFilePath(data.FilePath);
                            var text = await TryLoadFromAddressablesTaskAsync(key);
                            if (!string.IsNullOrEmpty(text)) return text;
                        }
                        break;

                    case TextSourceKind.Resources:
                        if (!string.IsNullOrWhiteSpace(data.FilePath))
                        {
                            var ta = Resources.Load<TextAsset>(data.FilePath);
                            if (ta) return ta.text;
                        }
                        break;
                }
            }

            if (Settings?.VerboseLogs == true)
                Debug.LogWarning($"ConvoCore: YAML not found via [{string.Join(", ", order)}] for FilePath='{data.FilePath}'.");
            return null;
        }

        // ------------------- Core (Coroutine) -------------------

        static IEnumerator LoadInternalCoroutine(ConvoCoreConversationData data, Action<string> onDone)
        {
            string result = null;

            var order = Settings?.SourceOrder ?? new[]
            {
                TextSourceKind.AssignedTextAsset,
                TextSourceKind.Persistent,
                TextSourceKind.Addressables,
                TextSourceKind.Resources
            };

            foreach (var src in order)
            {
                if (result != null) break;

                switch (src)
                {
                    case TextSourceKind.AssignedTextAsset:
                        if (data.ConversationYaml && !string.IsNullOrEmpty(data.ConversationYaml.text))
                            result = data.ConversationYaml.text;
                        break;

                    case TextSourceKind.Persistent:
                        if (data.AllowPersistentOverrides && TryReadPersistent(data, out var pText))
                            result = pText;
                        break;

                    case TextSourceKind.Addressables:
                        if (Settings != null && Settings.AddressablesEnabled && !string.IsNullOrWhiteSpace(data.FilePath))
                        {
                            bool done = false;
                            string addrText = null;
                            yield return TryLoadFromAddressablesCoroutine(KeyFromFilePath(data.FilePath), t => { addrText = t; done = true; });
                            if (done && !string.IsNullOrEmpty(addrText)) result = addrText;
                        }
                        break;

                    case TextSourceKind.Resources:
                        if (!string.IsNullOrWhiteSpace(data.FilePath))
                        {
                            var ta = Resources.Load<TextAsset>(data.FilePath);
                            if (ta) result = ta.text;
                        }
                        break;
                }
            }

            if (result == null && Settings?.VerboseLogs == true)
                Debug.LogWarning($"ConvoCore: YAML not found via [{string.Join(", ", order)}] for FilePath='{data.FilePath}'.");

            onDone?.Invoke(result);
        }

        // ------------------- Helpers -------------------

        static string KeyFromFilePath(string filePathNoExt)
            => (Settings?.AddressablesKeyTemplate ?? "{filePath}.yml").Replace("{filePath}", filePathNoExt);

        static bool TryReadPersistent(ConvoCoreConversationData data, out string text)
        {
            string rel = (data.FilePath ?? "").Replace('/', Path.DirectorySeparatorChar);
            var baseDir = Path.Combine(Application.persistentDataPath, "ConvoCore", "Dialogue");
            var p1 = Path.Combine(baseDir, rel + ".yml");
            var p2 = Path.Combine(baseDir, rel + ".yaml");
            if (File.Exists(p1)) { text = File.ReadAllText(p1); return true; }
            if (File.Exists(p2)) { text = File.ReadAllText(p2); return true; }
            text = null; return false;
        }

        // ------------------- Addressables shims -------------------
#if CONVOCORE_ADDRESSABLES
        static string TryLoadFromAddressablesSync(string key)
        {
            try
            {
                var init = UnityEngine.AddressableAssets.Addressables.InitializeAsync();
                init.WaitForCompletion();
                var h = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<TextAsset>(key);
                var ta = h.WaitForCompletion();
                UnityEngine.AddressableAssets.Addressables.Release(h);
                return ta ? ta.text : null;
            }
            catch { return null; }
        }

        static async Task<string> TryLoadFromAddressablesTaskAsync(string key)
        {
            try
            {
                await UnityEngine.AddressableAssets.Addressables.InitializeAsync().Task;
                var h = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<TextAsset>(key);
                var ta = await h.Task;
                UnityEngine.AddressableAssets.Addressables.Release(h);
                return ta ? ta.text : null;
            }
            catch { return null; }
        }

        static IEnumerator TryLoadFromAddressablesCoroutine(string key, Action<string> onDone)
        {
            var init = UnityEngine.AddressableAssets.Addressables.InitializeAsync();
            yield return init;
            var h = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<TextAsset>(key);
            yield return h;
            var ta = h.Result as TextAsset;
            UnityEngine.AddressableAssets.Addressables.Release(h);
            onDone?.Invoke(ta ? ta.text : null);
        }
#else
        static string TryLoadFromAddressablesSync(string key) => null;
        static Task<string> TryLoadFromAddressablesTaskAsync(string key) => Task.FromResult<string>(null);
        static IEnumerator TryLoadFromAddressablesCoroutine(string key, Action<string> onDone) { onDone?.Invoke(null); yield break; }
#endif
    }
}