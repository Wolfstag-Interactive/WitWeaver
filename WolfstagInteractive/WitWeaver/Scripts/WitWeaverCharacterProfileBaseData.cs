// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// ScriptableObject that defines a character's identity within WitWeaver: their display name,
    /// CharacterID (matched against YAML), name colour, and the collection of
    /// <see cref="CharacterRepresentationBase"/> assets that map expression IDs to visuals.
    /// Representations are referenced by their stable <see cref="RepresentationPair.RepresentationID"/>;
    /// the display name is presentation-only and safe to rename at any time.
    /// Set <c>IsPlayerCharacter</c> on exactly one profile to enable <c>{PlayerName}</c> substitution.
    /// </summary>
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverCharacterProfileBaseData.html")]
[CreateAssetMenu(fileName = "NewCharacterProfile", menuName = "WitWeaver/Character Profile")]
    public class WitWeaverCharacterProfileBaseData : ScriptableObject
    {
        // Basic character information
        public bool IsPlayerCharacter;
        public string CharacterName;
        public string PlayerPlaceholder;
        public string CharacterID;
        public Color CharacterNameColor = Color.grey;
        public string CharacterDescription;

        [Tooltip("Each element maps a representation asset to a display name (e.g., 'Sprites', 'Battle Model'). " +
                 "Dialogue lines reference entries by their stable ID, so renaming is always safe.")]
        public List<RepresentationPair> Representations = new List<RepresentationPair>();

        // One warning per requested-but-missing id per session; resets on domain reload.
        [System.NonSerialized] private HashSet<string> _warnedMissingIds;

        /// <summary>
        /// Lenient representation lookup. Resolves the stable representation ID
        /// (<see cref="RepresentationPair.RepresentationID"/>); a null/empty ID means "default" and
        /// returns the first representation. During the migration window a legacy display-name match
        /// is also accepted.
        ///
        /// Never returns null while any representation exists: an unresolvable ID logs a warning
        /// (once per ID per session) and substitutes the first representation, so a stale reference
        /// degrades visibly instead of crashing mid-conversation. Use
        /// <see cref="TryGetRepresentation"/> to detect misses without fallback or logging.
        /// </summary>
        /// <param name="representationId">Stable representation ID, or null/empty for the default.</param>
        /// <returns>The matching representation, the first representation as fallback, or null only
        /// when the profile has no representations at all (logged as an error).</returns>
        public CharacterRepresentationBase GetRepresentation(string representationId)
        {
            if (Representations == null || Representations.Count == 0)
            {
                Debug.LogError($"No representations are defined for character '{CharacterName}'.", this);
                return null;
            }

            // Null/empty means "default" by contract (auto-fix and primary auto-assign rely on it).
            if (string.IsNullOrEmpty(representationId))
                return Representations[0]?.CharacterRepresentationType;

            var pair = FindPair(representationId);
            if (pair != null)
                return pair.CharacterRepresentationType;

            _warnedMissingIds ??= new HashSet<string>();
            if (_warnedMissingIds.Add(representationId))
            {
                Debug.LogWarning(
                    $"[WitWeaver] Profile '{CharacterName}' ({name}): representation id '{representationId}' not found; " +
                    $"substituting '{Representations[0]?.CharacterRepresentationName}'. A representation referenced by " +
                    "this conversation was deleted, or the asset was replaced.", this);
            }

            return Representations[0]?.CharacterRepresentationType;
        }

        /// <summary>
        /// Strict representation lookup by stable ID. No fallback, no logging.
        /// Returns false when the id is null/empty, no representation carries that id, or the
        /// matching entry has no representation asset assigned. Unlike
        /// <see cref="GetRepresentation"/>, an empty id is a miss here, not "default", and legacy
        /// display names are not accepted.
        /// </summary>
        public bool TryGetRepresentation(string representationId, out CharacterRepresentationBase representation)
        {
            representation = null;
            if (string.IsNullOrEmpty(representationId) || Representations == null)
                return false;

            foreach (var pair in Representations)
            {
                if (pair == null || pair.RepresentationID != representationId)
                    continue;
                representation = pair.CharacterRepresentationType;
                return representation != null;
            }

            return false;
        }

        /// <summary>
        /// Exact display-name lookup returning the pair's stable ID. Used by editor validation and
        /// the representation-ID migration pass to upgrade legacy name references; not a runtime
        /// resolution path.
        /// </summary>
        public bool TryGetRepresentationIdByName(string displayName, out string representationId)
        {
            representationId = null;
            if (string.IsNullOrEmpty(displayName) || Representations == null)
                return false;

            foreach (var pair in Representations)
            {
                if (pair == null || pair.CharacterRepresentationName != displayName)
                    continue;
                representationId = pair.RepresentationID;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns (id, displayName) pairs for editor dropdowns, mirroring
        /// <see cref="IExpressionCatalogProvider.GetExpressionCatalog"/>: the popup shows the name
        /// and stores the stable ID.
        /// </summary>
        public IReadOnlyList<(string id, string name)> GetRepresentationCatalog()
        {
            var catalog = new List<(string id, string name)>(Representations?.Count ?? 0);
            if (Representations == null)
                return catalog;

            foreach (var pair in Representations)
            {
                if (pair != null)
                    catalog.Add((pair.RepresentationID, pair.CharacterRepresentationName));
            }

            return catalog;
        }

        // Resolves an identifier to a pair: stable ID first, then legacy display name.
        // The name branch is the migration shim keeping pre-ID assets and callers working;
        // it is removed together with the line-data SelectedRepresentationName field.
        private RepresentationPair FindPair(string identifier)
        {
            foreach (var pair in Representations)
            {
                if (pair != null && pair.RepresentationID == identifier)
                    return pair;
            }

            foreach (var pair in Representations)
            {
                if (pair != null && pair.CharacterRepresentationName == identifier)
                    return pair;
            }

            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Representations == null) return;

            var usedIds = new HashSet<string>();
            var usedNames = new HashSet<string>();
            bool changed = false;

            foreach (var pair in Representations)
            {
                if (pair == null) continue;

                string before = pair.RepresentationID;
                pair.EnsureValidId(usedIds);
                changed |= before != pair.RepresentationID;

                if (!string.IsNullOrEmpty(pair.CharacterRepresentationName) &&
                    !usedNames.Add(pair.CharacterRepresentationName))
                {
                    Debug.LogWarning(
                        $"[WitWeaver] Profile '{name}': two representations share the display name " +
                        $"'{pair.CharacterRepresentationName}'. Lines still resolve correctly (they reference " +
                        "stable IDs), but consider renaming one for clarity.", this);
                }
            }

            // Dirty only when an ID was actually created or regenerated, so loading an already
            // stamped asset never churns version control.
            if (changed)
                UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    [System.Serializable]
    public class RepresentationPair
    {
        [SerializeField, Tooltip("Stable unique ID (GUID). Non-editable.")]
        private string representationID = System.Guid.NewGuid().ToString("N");

        /// <summary>Stable unique ID (GUID) that dialogue lines reference. Never changes on rename.</summary>
        public string RepresentationID => representationID;

        [Tooltip("Human-readable name shown in dropdowns and inspector list headers. Display-only; renaming never breaks references.")]
        public string CharacterRepresentationName;

        [Tooltip("The representation asset implementing the representation system (e.g., sprite or prefab based).")]
        public CharacterRepresentationBase CharacterRepresentationType;

        /// <summary>
        /// Regenerates the ID when it is blank or collides with an ID already in
        /// <paramref name="used"/> (e.g. after the inspector duplicates a list element).
        /// </summary>
        public void EnsureValidId(HashSet<string> used)
        {
            if (string.IsNullOrWhiteSpace(representationID) || !used.Add(representationID))
                representationID = System.Guid.NewGuid().ToString("N");
        }
    }
}
