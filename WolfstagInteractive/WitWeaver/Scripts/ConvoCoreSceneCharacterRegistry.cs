using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Scene-level registry that maps developer-assigned string IDs to
    /// <see cref="IConvoCoreCharacterDisplay"/> instances that already exist in the scene.
    ///
    /// Place one instance anywhere in the scene. Characters register themselves automatically
    /// via <see cref="ConvoCoreSceneCharacterRegistrant"/>.
    ///
    /// The first instance to awake is accessible via <see cref="Instance"/>. Multiple registries
    /// can coexist; only the first-awake one is reachable through the static property.
    /// Assign the registry directly on <see cref="ConvoCorePrefabRepresentationSpawner"/> when
    /// you need a specific instance.
    /// </summary>
    public class ConvoCoreSceneCharacterRegistry : MonoBehaviour
    {
        /// <summary>
        /// The first registry to awake in the scene. Used as a fallback by
        /// <see cref="ConvoCorePrefabRepresentationSpawner"/> when no registry is explicitly assigned.
        /// </summary>
        public static ConvoCoreSceneCharacterRegistry Instance { get; private set; }

        private readonly Dictionary<string, IConvoCoreCharacterDisplay> _registered = new();

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Debug.LogWarning("[ConvoCoreSceneCharacterRegistry] Multiple instances detected. Only the first will be used as the static Instance.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Registers a display under the given ID. Overwrites any existing entry with the same ID.
        /// Called automatically by <see cref="ConvoCoreSceneCharacterRegistrant"/> on OnEnable.
        /// </summary>
        public void Register(string id, IConvoCoreCharacterDisplay display)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[ConvoCoreSceneCharacterRegistry] Attempted to register a character with a null or empty ID. Skipping.");
                return;
            }

            if (display == null)
            {
                Debug.LogWarning($"[ConvoCoreSceneCharacterRegistry] Attempted to register null display for ID '{id}'. Skipping.");
                return;
            }

            if (_registered.ContainsKey(id))
                Debug.LogWarning($"[ConvoCoreSceneCharacterRegistry] ID '{id}' is already registered. Overwriting. Check for duplicate registrants in the scene.");

            _registered[id] = display;
        }

        /// <summary>
        /// Removes the registration for the given ID.
        /// Called automatically by <see cref="ConvoCoreSceneCharacterRegistrant"/> on OnDisable.
        /// </summary>
        public void Unregister(string id)
        {
            if (!string.IsNullOrEmpty(id))
                _registered.Remove(id);
        }

        /// <summary>
        /// Attempts to retrieve a registered display by ID.
        /// </summary>
        /// <returns>True if a display was found for the given ID.</returns>
        public bool TryGet(string id, out IConvoCoreCharacterDisplay display)
        {
            display = null;
            if (string.IsNullOrEmpty(id)) return false;
            return _registered.TryGetValue(id, out display);
        }

        /// <summary>
        /// Returns whether the given ID is currently registered.
        /// </summary>
        public bool IsRegistered(string id) =>
            !string.IsNullOrEmpty(id) && _registered.ContainsKey(id);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            foreach (var kvp in _registered)
                Debug.Log($"[ConvoCoreSceneCharacterRegistry] Registered: '{kvp.Key}' -> {kvp.Value}");
        }
#endif
    }
}