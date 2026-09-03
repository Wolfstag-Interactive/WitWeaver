// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Resolves, tracks, and releases prefab-based character displays for a single UI instance.
    ///
    /// Resolution order for each character:
    /// <list type="number">
    ///   <item>Scene registry lookup by <c>characterId</c> — if a registrant is found the scene
    ///       instance is used. WitWeaver never spawns, pools, or destroys scene-resident characters.</item>
    ///   <item>Prefab spawn — the configuration entry's <c>CharacterPrefab</c> is drawn from
    ///       <see cref="WitWeaverPrefabPool"/> and returned to it on release.</item>
    /// </list>
    ///
    /// A console message is emitted for whichever path is taken so behavior is transparent without
    /// requiring manual source-mode declarations on the representation asset.
    ///
    /// Active entries are tracked by the <see cref="Transform"/> passed as the slot anchor.
    /// Passing the same Transform again releases the previous occupant before placing a new one.
    /// Scene-resident characters are not tracked by slot and are never pooled or destroyed here.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverPrefabRepresentationSpawner.html")]
    public class WitWeaverPrefabRepresentationSpawner : MonoBehaviour
    {
        [Tooltip("Optional. When assigned this registry is preferred over the static WitWeaverSceneCharacterRegistry.Instance.")]
        [SerializeField] private WitWeaverSceneCharacterRegistry sceneCharacterRegistry;

        // Active spawned entries keyed by the slot anchor Transform.
        private readonly Dictionary<Transform, ActiveCharacterEntry> _activeEntries = new();

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Full-service canvas UI path. Resolves the character display (registry-first, then prefab
        /// spawn), binds the representation, applies expression and display options, and triggers
        /// a fade-in if the instance implements <see cref="IWitWeaverFadeIn"/>.
        /// Passing the same <paramref name="slotTransform"/> releases the previous occupant first.
        /// Returns null if resolution fails.
        /// </summary>
        public IWitWeaverCharacterDisplay ResolveCharacter(
            PrefabCharacterRepresentationData representation,
            string expressionID,
            DialogueLineDisplayOptions displayOptions,
            Transform slotTransform)
        {
            var display = ResolveDisplayCore(representation, null, null, slotTransform);
            if (display == null) return null;

            if (!string.IsNullOrEmpty(expressionID))
                display.ApplyExpression(expressionID);

            if (displayOptions != null)
                display.ApplyDisplayOptions(displayOptions);

            TryTriggerFadeIn(slotTransform);
            return display;
        }

        /// <summary>
        /// Presence path. Resolves (registry-first, then prefab spawn) and binds the representation
        /// without applying expression or display options. The caller applies those after receiving
        /// the display. Returns null if resolution fails.
        /// </summary>
        public IWitWeaverCharacterDisplay SpawnAndBind(
            PrefabCharacterRepresentationData representation,
            Transform slotTransform)
        {
            return ResolveDisplayCore(representation, null, null, slotTransform);
        }

        /// <summary>
        /// Presence path with explicit entry selection and registry-first lookup.
        /// Resolves by scene registry (<paramref name="characterId"/>) first; falls back to spawning
        /// from <paramref name="entryName"/> on the representation.
        /// Returns null if resolution fails.
        /// </summary>
        public IWitWeaverCharacterDisplay SpawnAndBind(
            PrefabCharacterRepresentationData representation,
            string entryName,
            string characterId,
            Transform slotTransform)
        {
            return ResolveDisplayCore(representation, entryName, characterId, slotTransform);
        }

        /// <summary>
        /// Looks up a scene-resident display by ID without tracking it in the active-entry
        /// dictionary. Falls back to <see cref="WitWeaverSceneCharacterRegistry.Instance"/> if no
        /// registry is assigned to this spawner.
        /// The caller owns the display; WitWeaver will not pool or destroy it.
        /// </summary>
        public bool TryGetSceneResident(string sceneCharacterId, out IWitWeaverCharacterDisplay display)
        {
            display = null;
            if (string.IsNullOrEmpty(sceneCharacterId)) return false;

            var registry = sceneCharacterRegistry ?? WitWeaverSceneCharacterRegistry.Instance;
            if (registry == null)
            {
                Debug.LogWarning($"[{nameof(WitWeaverPrefabRepresentationSpawner)}] No WitWeaverSceneCharacterRegistry available. " +
                                 $"Cannot look up scene-resident character '{sceneCharacterId}'.");
                return false;
            }
            return registry.TryGet(sceneCharacterId, out display);
        }

        /// <summary>
        /// Releases all active tracked entries. Scene-resident characters are removed from
        /// tracking only and are never pooled or destroyed.
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var anchor in new List<Transform>(_activeEntries.Keys))
                ReleaseSlot(anchor);

            _activeEntries.Clear();
        }

        // ------------------------------------------------------------------
        // Core resolution
        // ------------------------------------------------------------------

        private IWitWeaverCharacterDisplay ResolveDisplayCore(
            PrefabCharacterRepresentationData representation,
            string entryName,
            string characterId,
            Transform slotTransform)
        {
            if (slotTransform != null)
                ReleaseSlot(slotTransform);

            // 1. Scene-registry path ─ check by characterId first.
            if (!string.IsNullOrEmpty(characterId))
            {
                var registry = sceneCharacterRegistry ?? WitWeaverSceneCharacterRegistry.Instance;
                if (registry != null && registry.TryGet(characterId, out var sceneDisplay))
                {
                    Debug.Log($"[{nameof(WitWeaverPrefabRepresentationSpawner)}] '{characterId}': using scene-resident instance.");
                    sceneDisplay.BindRepresentation(representation);
                    var sceneEntry = new ActiveCharacterEntry(sceneDisplay, isSceneResident: true, sourcePrefab: null);
                    if (slotTransform != null)
                        _activeEntries[slotTransform] = sceneEntry;
                    return sceneDisplay;
                }
            }

            // 2. Prefab-spawn path.
            var configEntry = representation.GetEntry(entryName);
            if (configEntry == null)
            {
                Debug.LogWarning($"[{nameof(WitWeaverPrefabRepresentationSpawner)}] No configuration entry found on '{representation.name}' " +
                                 $"(entryName='{entryName}').");
                return null;
            }

            Debug.Log($"[{nameof(WitWeaverPrefabRepresentationSpawner)}] '{(!string.IsNullOrEmpty(characterId) ? characterId : representation.name)}': " +
                      $"spawning from prefab '{configEntry.CharacterPrefab?.name}'.");

            var instance = SpawnFromPool(configEntry, slotTransform);
            if (instance == null) return null;

            var display = instance.GetComponentInChildren<IWitWeaverCharacterDisplay>();
            if (display == null)
            {
                Debug.LogWarning($"[{nameof(WitWeaverPrefabRepresentationSpawner)}] Prefab '{configEntry.CharacterPrefab.name}' " +
                                 $"has no IWitWeaverCharacterDisplay component.");
                WitWeaverPrefabPool.Instance.Release(configEntry.CharacterPrefab, instance);
                return null;
            }

            display.BindRepresentation(representation);
            var entry = new ActiveCharacterEntry(display, isSceneResident: false,
                sourcePrefab: configEntry.CharacterPrefab, sourceInstance: instance);

            if (slotTransform != null)
                _activeEntries[slotTransform] = entry;

            return display;
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private GameObject SpawnFromPool(PrefabCharacterConfigurationEntry configEntry, Transform parent)
        {
            if (configEntry.CharacterPrefab == null)
            {
                Debug.LogWarning($"[{nameof(WitWeaverPrefabRepresentationSpawner)}] Configuration entry '{configEntry.EntryName}' has no CharacterPrefab assigned.");
                return null;
            }

            if (WitWeaverPrefabPool.Instance == null)
            {
                Debug.LogWarning($"[{nameof(WitWeaverPrefabRepresentationSpawner)}] No WitWeaverPrefabPool found in the scene. Cannot spawn prefab character.");
                return null;
            }

            var instance = WitWeaverPrefabPool.Instance.Spawn(configEntry.CharacterPrefab, parent);
            instance.name = $"{configEntry.CharacterPrefab.name}_{System.Guid.NewGuid().ToString("N").Substring(0, 6)}";
            return instance;
        }

        private void ReleaseSlot(Transform anchor)
        {
            if (!_activeEntries.TryGetValue(anchor, out var entry))
                return;

            _activeEntries.Remove(anchor);

            // Scene-resident characters are never pooled or destroyed by WitWeaver.
            if (entry.IsSceneResident)
                return;

            if (entry.SourceInstance == null || entry.SourcePrefab == null)
                return;

            var instance = entry.SourceInstance;
            var prefab   = entry.SourcePrefab;

            if (instance.TryGetComponent<IWitWeaverFadeOut>(out var fadeOut))
            {
                fadeOut.FadeOutAndRelease(() =>
                {
                    if (WitWeaverPrefabPool.Instance != null)
                        WitWeaverPrefabPool.Instance.Release(prefab, instance);
                    else
                        Destroy(instance);
                });
            }
            else
            {
                if (WitWeaverPrefabPool.Instance != null)
                    WitWeaverPrefabPool.Instance.Release(prefab, instance);
                else
                    Destroy(instance);
            }
        }

        private void TryTriggerFadeIn(Transform slotTransform)
        {
            if (slotTransform != null &&
                _activeEntries.TryGetValue(slotTransform, out var entry) &&
                entry.SourceInstance != null &&
                entry.SourceInstance.TryGetComponent<IWitWeaverFadeIn>(out var fadeIn))
                fadeIn.FadeIn();
        }

        // ------------------------------------------------------------------
        // Entry tracking
        // ------------------------------------------------------------------

        private readonly struct ActiveCharacterEntry
        {
            public readonly IWitWeaverCharacterDisplay Display;
            public readonly bool IsSceneResident;
            public readonly GameObject SourcePrefab;
            public readonly GameObject SourceInstance;

            public ActiveCharacterEntry(
                IWitWeaverCharacterDisplay display,
                bool isSceneResident,
                GameObject sourcePrefab,
                GameObject sourceInstance = null)
            {
                Display        = display;
                IsSceneResident = isSceneResident;
                SourcePrefab   = sourcePrefab;
                SourceInstance = sourceInstance;
            }
        }
    }

    public interface IWitWeaverFadeOut
    {
        /// <summary>
        /// Called when a spawned character is being released from a slot.
        /// Invoke <paramref name="onComplete"/> when the fade is finished so the
        /// instance can be returned to the pool. Not called for scene-resident characters.
        /// </summary>
        void FadeOutAndRelease(System.Action onComplete);
    }

    public interface IWitWeaverFadeIn
    {
        /// <summary>
        /// Called immediately after a spawned character appears in a slot.
        /// Not called for scene-resident characters unless the developer explicitly triggers it.
        /// </summary>
        void FadeIn(System.Action onComplete = null);
    }
}
