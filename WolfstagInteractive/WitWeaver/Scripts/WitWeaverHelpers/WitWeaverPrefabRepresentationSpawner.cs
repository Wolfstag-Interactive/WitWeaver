using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Resolves, tracks, and releases prefab-based character displays for a single UI instance.
    ///
    /// Resolution order for each character:
    /// <list type="number">
    ///   <item>Scene registry lookup by <c>characterId</c> — if a registrant is found the scene
    ///       instance is used. ConvoCore never spawns, pools, or destroys scene-resident characters.</item>
    ///   <item>Prefab spawn — the configuration entry's <c>CharacterPrefab</c> is drawn from
    ///       <see cref="ConvoCorePrefabPool"/> and returned to it on release.</item>
    /// </list>
    ///
    /// A console message is emitted for whichever path is taken so behavior is transparent without
    /// requiring manual source-mode declarations on the representation asset.
    ///
    /// Active entries are tracked by the <see cref="Transform"/> passed as the slot anchor.
    /// Passing the same Transform again releases the previous occupant before placing a new one.
    /// Scene-resident characters are not tracked by slot and are never pooled or destroyed here.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCorePrefabRepresentationSpawner.html")]
    public class ConvoCorePrefabRepresentationSpawner : MonoBehaviour
    {
        [Tooltip("Optional. When assigned this registry is preferred over the static ConvoCoreSceneCharacterRegistry.Instance.")]
        [SerializeField] private ConvoCoreSceneCharacterRegistry sceneCharacterRegistry;

        // Active spawned entries keyed by the slot anchor Transform.
        private readonly Dictionary<Transform, ActiveCharacterEntry> _activeEntries = new();

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Full-service canvas UI path. Resolves the character display (registry-first, then prefab
        /// spawn), binds the representation, applies expression and display options, and triggers
        /// a fade-in if the instance implements <see cref="IConvoCoreFadeIn"/>.
        /// Passing the same <paramref name="slotTransform"/> releases the previous occupant first.
        /// Returns null if resolution fails.
        /// </summary>
        public IConvoCoreCharacterDisplay ResolveCharacter(
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
        public IConvoCoreCharacterDisplay SpawnAndBind(
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
        public IConvoCoreCharacterDisplay SpawnAndBind(
            PrefabCharacterRepresentationData representation,
            string entryName,
            string characterId,
            Transform slotTransform)
        {
            return ResolveDisplayCore(representation, entryName, characterId, slotTransform);
        }

        /// <summary>
        /// Looks up a scene-resident display by ID without tracking it in the active-entry
        /// dictionary. Falls back to <see cref="ConvoCoreSceneCharacterRegistry.Instance"/> if no
        /// registry is assigned to this spawner.
        /// The caller owns the display; ConvoCore will not pool or destroy it.
        /// </summary>
        public bool TryGetSceneResident(string sceneCharacterId, out IConvoCoreCharacterDisplay display)
        {
            display = null;
            if (string.IsNullOrEmpty(sceneCharacterId)) return false;

            var registry = sceneCharacterRegistry ?? ConvoCoreSceneCharacterRegistry.Instance;
            if (registry == null)
            {
                Debug.LogWarning($"[{nameof(ConvoCorePrefabRepresentationSpawner)}] No ConvoCoreSceneCharacterRegistry available. " +
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

        private IConvoCoreCharacterDisplay ResolveDisplayCore(
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
                var registry = sceneCharacterRegistry ?? ConvoCoreSceneCharacterRegistry.Instance;
                if (registry != null && registry.TryGet(characterId, out var sceneDisplay))
                {
                    Debug.Log($"[{nameof(ConvoCorePrefabRepresentationSpawner)}] '{characterId}': using scene-resident instance.");
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
                Debug.LogWarning($"[{nameof(ConvoCorePrefabRepresentationSpawner)}] No configuration entry found on '{representation.name}' " +
                                 $"(entryName='{entryName}').");
                return null;
            }

            Debug.Log($"[{nameof(ConvoCorePrefabRepresentationSpawner)}] '{(!string.IsNullOrEmpty(characterId) ? characterId : representation.name)}': " +
                      $"spawning from prefab '{configEntry.CharacterPrefab?.name}'.");

            var instance = SpawnFromPool(configEntry, slotTransform);
            if (instance == null) return null;

            var display = instance.GetComponentInChildren<IConvoCoreCharacterDisplay>();
            if (display == null)
            {
                Debug.LogWarning($"[{nameof(ConvoCorePrefabRepresentationSpawner)}] Prefab '{configEntry.CharacterPrefab.name}' " +
                                 $"has no IConvoCoreCharacterDisplay component.");
                ConvoCorePrefabPool.Instance.Release(configEntry.CharacterPrefab, instance);
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
                Debug.LogWarning($"[{nameof(ConvoCorePrefabRepresentationSpawner)}] Configuration entry '{configEntry.EntryName}' has no CharacterPrefab assigned.");
                return null;
            }

            if (ConvoCorePrefabPool.Instance == null)
            {
                Debug.LogWarning($"[{nameof(ConvoCorePrefabRepresentationSpawner)}] No ConvoCorePrefabPool found in the scene. Cannot spawn prefab character.");
                return null;
            }

            var instance = ConvoCorePrefabPool.Instance.Spawn(configEntry.CharacterPrefab, parent);
            instance.name = $"{configEntry.CharacterPrefab.name}_{System.Guid.NewGuid().ToString("N").Substring(0, 6)}";
            return instance;
        }

        private void ReleaseSlot(Transform anchor)
        {
            if (!_activeEntries.TryGetValue(anchor, out var entry))
                return;

            _activeEntries.Remove(anchor);

            // Scene-resident characters are never pooled or destroyed by ConvoCore.
            if (entry.IsSceneResident)
                return;

            if (entry.SourceInstance == null || entry.SourcePrefab == null)
                return;

            var instance = entry.SourceInstance;
            var prefab   = entry.SourcePrefab;

            if (instance.TryGetComponent<IConvoCoreFadeOut>(out var fadeOut))
            {
                fadeOut.FadeOutAndRelease(() =>
                {
                    if (ConvoCorePrefabPool.Instance != null)
                        ConvoCorePrefabPool.Instance.Release(prefab, instance);
                    else
                        Destroy(instance);
                });
            }
            else
            {
                if (ConvoCorePrefabPool.Instance != null)
                    ConvoCorePrefabPool.Instance.Release(prefab, instance);
                else
                    Destroy(instance);
            }
        }

        private void TryTriggerFadeIn(Transform slotTransform)
        {
            if (slotTransform != null &&
                _activeEntries.TryGetValue(slotTransform, out var entry) &&
                entry.SourceInstance != null &&
                entry.SourceInstance.TryGetComponent<IConvoCoreFadeIn>(out var fadeIn))
                fadeIn.FadeIn();
        }

        // ------------------------------------------------------------------
        // Entry tracking
        // ------------------------------------------------------------------

        private readonly struct ActiveCharacterEntry
        {
            public readonly IConvoCoreCharacterDisplay Display;
            public readonly bool IsSceneResident;
            public readonly GameObject SourcePrefab;
            public readonly GameObject SourceInstance;

            public ActiveCharacterEntry(
                IConvoCoreCharacterDisplay display,
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

    public interface IConvoCoreFadeOut
    {
        /// <summary>
        /// Called when a spawned character is being released from a slot.
        /// Invoke <paramref name="onComplete"/> when the fade is finished so the
        /// instance can be returned to the pool. Not called for scene-resident characters.
        /// </summary>
        void FadeOutAndRelease(System.Action onComplete);
    }

    public interface IConvoCoreFadeIn
    {
        /// <summary>
        /// Called immediately after a spawned character appears in a slot.
        /// Not called for scene-resident characters unless the developer explicitly triggers it.
        /// </summary>
        void FadeIn(System.Action onComplete = null);
    }
}
