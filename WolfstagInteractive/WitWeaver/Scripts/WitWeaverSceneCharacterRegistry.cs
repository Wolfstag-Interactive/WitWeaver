// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Scene-level registry that maps developer-assigned string IDs to
    /// <see cref="IWitWeaverCharacterDisplay"/> instances that already exist in the scene.
    ///
    /// Place one instance anywhere in the scene. Characters register themselves automatically
    /// via <see cref="WitWeaverSceneCharacterRegistrant"/>.
    ///
    /// The first instance to awake is accessible via <see cref="Instance"/>. Multiple registries
    /// can coexist; only the first-awake one is reachable through the static property.
    /// Assign the registry directly on <see cref="WitWeaverPrefabRepresentationSpawner"/> when
    /// you need a specific instance.
    /// </summary>
    public class WitWeaverSceneCharacterRegistry : MonoBehaviour
    {
        /// <summary>
        /// The first registry to awake in the scene. Used as a fallback by
        /// <see cref="WitWeaverPrefabRepresentationSpawner"/> when no registry is explicitly assigned.
        /// </summary>
        public static WitWeaverSceneCharacterRegistry Instance { get; private set; }

        private readonly Dictionary<string, IWitWeaverCharacterDisplay> _registered = new();

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Debug.LogWarning("[WitWeaverSceneCharacterRegistry] Multiple instances detected. Only the first will be used as the static Instance.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Registers a display under the given ID. Overwrites any existing entry with the same ID.
        /// Called automatically by <see cref="WitWeaverSceneCharacterRegistrant"/> on OnEnable.
        /// </summary>
        public void Register(string id, IWitWeaverCharacterDisplay display)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[WitWeaverSceneCharacterRegistry] Attempted to register a character with a null or empty ID. Skipping.");
                return;
            }

            if (display == null)
            {
                Debug.LogWarning($"[WitWeaverSceneCharacterRegistry] Attempted to register null display for ID '{id}'. Skipping.");
                return;
            }

            if (_registered.ContainsKey(id))
                Debug.LogWarning($"[WitWeaverSceneCharacterRegistry] ID '{id}' is already registered. Overwriting. Check for duplicate registrants in the scene.");

            _registered[id] = display;
        }

        /// <summary>
        /// Removes the registration for the given ID.
        /// Called automatically by <see cref="WitWeaverSceneCharacterRegistrant"/> on OnDisable.
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
        public bool TryGet(string id, out IWitWeaverCharacterDisplay display)
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
                Debug.Log($"[WitWeaverSceneCharacterRegistry] Registered: '{kvp.Key}' -> {kvp.Value}");
        }
#endif
    }
}