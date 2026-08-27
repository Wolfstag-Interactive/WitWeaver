using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Scene singleton that tracks all <see cref="ConvoCoreSpawnPoint"/> instances by their ID.
    ///
    /// Spawn points self-register on <c>OnEnable</c> and self-unregister on <c>OnDisable</c>.
    /// <see cref="WorldPointBehaviour"/> queries this registry at conversation begin to resolve
    /// named spawn point IDs to world transforms.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api")]
    public class ConvoCoreSpawnPointRegistry : MonoBehaviour
    {
        /// <summary>The first registry that awoke in the scene.</summary>
        public static ConvoCoreSpawnPointRegistry Instance { get; private set; }

        private readonly Dictionary<string, ConvoCoreSpawnPoint> _points = new();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning($"[ConvoCoreSpawnPointRegistry] Multiple registries detected. " +
                                 $"Only one is supported per scene. Ignoring '{gameObject.name}'.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Register a spawn point under the given ID.</summary>
        public void Register(string id, ConvoCoreSpawnPoint point)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_points.ContainsKey(id))
                Debug.LogWarning($"[ConvoCoreSpawnPointRegistry] Duplicate spawn point ID '{id}'. Overwriting previous entry.");
            _points[id] = point;
        }

        /// <summary>Unregister the spawn point with the given ID.</summary>
        public void Unregister(string id)
        {
            if (!string.IsNullOrEmpty(id))
                _points.Remove(id);
        }

        /// <summary>Try to get the spawn point Transform for the given ID.</summary>
        public bool TryGet(string id, out Transform result)
        {
            if (!string.IsNullOrEmpty(id) && _points.TryGetValue(id, out var point) && point != null)
            {
                result = point.transform;
                return true;
            }
            result = null;
            return false;
        }

        /// <summary>Returns true if a spawn point with the given ID is currently registered.</summary>
        public bool Contains(string id) =>
            !string.IsNullOrEmpty(id) && _points.TryGetValue(id, out var p) && p != null;
    }
}
