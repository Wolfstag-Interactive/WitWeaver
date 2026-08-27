using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace WolfstagInteractive.ConvoCore
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1PrefabDialogueHistoryOutput.html")]
    public class PrefabDialogueHistoryOutput : IDialogueHistoryOutputPrefab
    {
        private readonly Transform _contentRoot;
        private readonly GameObject _entryPrefab;
        private readonly ScrollRect _scroll;
        private readonly int _maxEntries;
        private readonly List<GameObject> _spawned = new();

        public PrefabDialogueHistoryOutput(Transform contentRoot, GameObject entryPrefab, ScrollRect scroll, int maxEntries)
        {
            _contentRoot = contentRoot;
            _entryPrefab = entryPrefab;
            _scroll = scroll;
            _maxEntries = maxEntries;
        }

        public void Clear()
        {
            foreach (var go in _spawned)
                if (go != null) Object.Destroy(go);
            _spawned.Clear();
        }

        public void Append(string line)
        {
            // Not used for prefab outputs
        }

        public void SpawnEntry(DialogueHistoryEntry entry)
        {
            if (_entryPrefab == null || _contentRoot == null)
            {
                Debug.LogWarning("[PrefabDialogueHistoryOutput] Missing prefab or content root.");
                return;
            }

            var instance = Object.Instantiate(_entryPrefab, _contentRoot);
            _spawned.Add(instance);

            // Enforce maximum entry count
            if (_spawned.Count > _maxEntries)
            {
                Object.Destroy(_spawned[0]);
                _spawned.RemoveAt(0);
            }

            // Assign text components (TMP or legacy)
            var tmpTexts = instance.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in tmpTexts)
            {
                var name = t.name.ToLowerInvariant();
                if (name.Contains("speaker"))
                    t.text = entry.Speaker;
                else if (name.Contains("line") || name.Contains("text"))
                    t.text = entry.Text;
            }

            RefreshView();
        }

        public void RefreshView()
        {
            if (_scroll == null) return;
            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = 0f;
        }
    }
}