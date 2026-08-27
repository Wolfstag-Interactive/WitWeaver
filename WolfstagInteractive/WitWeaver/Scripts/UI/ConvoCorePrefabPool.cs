using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCorePrefabPool.html")]
    public class ConvoCorePrefabPool : MonoBehaviour
    {
        public static ConvoCorePrefabPool Instance { get; private set; }

        private readonly Dictionary<GameObject, Stack<GameObject>> _pool = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var kvp in _pool)
            {
                var clean = new Stack<GameObject>();
                foreach (var go in kvp.Value)
                {
                    if (go != null)
                        clean.Push(go);
                }
                _pool[kvp.Key] = clean;
            }
        }

        public GameObject Spawn(GameObject prefab, Transform parent = null)
        {
            if (!_pool.TryGetValue(prefab, out var stack))
            {
                stack = new Stack<GameObject>();
                _pool[prefab] = stack;
            }

            GameObject instance = null;

            // Drain stale entries before using one
            while (stack.Count > 0)
            {
                var candidate = stack.Pop();
                if (candidate != null)
                {
                    instance = candidate;
                    break;
                }
                // candidate was destroyed externally -- discard and keep draining
            }

            if (instance == null)
                instance = Instantiate(prefab);

            instance.SetActive(true);

            if (parent != null)
                instance.transform.SetParent(parent, worldPositionStays: false);

            return instance;
        }

        public void Clear()
        {
            foreach (var kvp in _pool)
            {
                foreach (var go in kvp.Value)
                {
                    if (go != null)
                        Destroy(go);
                }
            }

            _pool.Clear();
        }

        public void Preload(GameObject prefab, int count)
        {
            if (!_pool.TryGetValue(prefab, out var stack))
                _pool[prefab] = stack = new Stack<GameObject>();

            for (int i = 0; i < count; i++)
            {
                var instance = Instantiate(prefab);
                instance.SetActive(false);
                stack.Push(instance);
            }
        }


        public void Release(GameObject prefab, GameObject instance)
        {
            if (instance == null) return;

            if (!_pool.TryGetValue(prefab, out var stack))
                _pool[prefab] = stack = new Stack<GameObject>();

            if (stack.Contains(instance))
            {
                Debug.LogWarning($"[PrefabPool] Attempted to release {instance.name} twice.");
                return;
            }

            instance.SetActive(false);
            stack.Push(instance);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            foreach (var kvp in _pool)
            {
                Debug.Log($"[PrefabPool] Prefab: {kvp.Key.name} | Pooled: {kvp.Value.Count}");
            }
        }
#endif
    }
}