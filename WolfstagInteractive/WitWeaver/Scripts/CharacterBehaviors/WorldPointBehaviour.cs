using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Character behaviour type that places characters at scene-authored positions.
    ///
    /// On <see cref="OnConversationBegin"/>: resolves each entry's <see cref="WorldPointEntry.SpawnPointId"/>
    /// against <see cref="ConvoCoreSpawnPointRegistry"/> and creates a marker Transform at the
    /// matching <see cref="ConvoCoreSpawnPoint"/>'s world position and rotation.
    /// On <see cref="ResolvePresence"/>: spawns the prefab via the spawner and parents it to the
    /// marker for the character's slot index. Characters that appear on multiple lines are cached
    /// and not re-spawned.
    /// On <see cref="OnConversationEnd"/>: releases all spawned instances via the spawner and
    /// destroys the marker Transforms.
    ///
    /// Place <see cref="ConvoCoreSpawnPoint"/> components on scene GameObjects and position them
    /// visually; this asset references them by ID.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldPointBehaviour", menuName = "ConvoCore/Character Behaviour/World Point Behaviour")]
    public class WorldPointBehaviour : ConvoCoreCharacterBehaviour
    {
        [Tooltip("One entry per character slot. Index 0 is the first character to appear on any line, etc.")]
        [SerializeField] private List<WorldPointEntry> _worldPoints = new();

        [System.Serializable]
        public class WorldPointEntry
        {
            [Tooltip("ID of a ConvoCoreSpawnPoint placed in the scene. The behaviour resolves the spawn point's transform at conversation begin.")]
            public string SpawnPointId;
        }

        // Runtime state -- not serialized.
        [System.NonSerialized] private List<Transform> _markers = new();
        [System.NonSerialized] private Dictionary<string, IConvoCoreCharacterDisplay> _cachedDisplays = new();
        [System.NonSerialized] private ConvoCorePrefabRepresentationSpawner _spawner;

        public override void OnConversationBegin()
        {
            CleanupMarkers();
            _cachedDisplays.Clear();

            var registry = ConvoCoreSpawnPointRegistry.Instance;

            foreach (var entry in _worldPoints)
            {
                if (!registry.TryGet(entry.SpawnPointId, out var spawnTransform))
                {
                    Debug.LogWarning($"[WorldPointBehaviour] Spawn point ID '{entry.SpawnPointId}' not found in registry. " +
                                     $"Ensure a ConvoCoreSpawnPoint with this ID exists and is active in the scene.");
                    _markers.Add(null);
                    continue;
                }

                var go = new GameObject($"_ConvoCore_WorldPoint_{entry.SpawnPointId}");
                go.transform.SetPositionAndRotation(spawnTransform.position, spawnTransform.rotation);
                _markers.Add(go.transform);
            }
        }

        public override IConvoCoreCharacterDisplay ResolvePresence(
            PrefabCharacterRepresentationData representation,
            CharacterBehaviourContext context,
            ConvoCorePrefabRepresentationSpawner spawner)
        {
            _spawner = spawner;

            // Return cached display for characters already resolved this conversation.
            var cacheKey = !string.IsNullOrEmpty(context.CharacterId) ? context.CharacterId : representation.name;
            if (_cachedDisplays.TryGetValue(cacheKey, out var cached))
                return cached;

            if (context.CharacterIndex >= _markers.Count)
            {
                Debug.LogWarning($"[WorldPointBehaviour] No world point defined for character index {context.CharacterIndex}. " +
                                 $"Add entries to the World Points list.");
                return null;
            }

            var marker = _markers[context.CharacterIndex];
            var display = spawner.SpawnAndBind(representation, context.ConfigurationEntryName, context.CharacterId, marker);

            if (display != null)
                _cachedDisplays[cacheKey] = display;

            return display;
        }

        public override void OnConversationEnd()
        {
            _spawner?.ReleaseAll();
            CleanupMarkers();
            _cachedDisplays.Clear();
            _spawner = null;
        }

        private void CleanupMarkers()
        {
            foreach (var m in _markers)
                if (m != null)
                    Destroy(m.gameObject);
            _markers.Clear();
        }
    }
}
