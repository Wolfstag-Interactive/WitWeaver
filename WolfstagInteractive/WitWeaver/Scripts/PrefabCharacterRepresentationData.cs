using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Defines how a prefab-based character is displayed in a WitWeaver conversation.
    ///
    /// A <b>shared expression pool</b> lives at the asset level. Individual
    /// <see cref="PrefabCharacterConfigurationEntry"/> items can optionally override it
    /// with entry-specific expression mappings.
    ///
    /// Each configuration entry names a prefab to use as a spawn fallback when the character
    /// is not found in the scene registry, and the <see cref="WitWeaverCharacterBehaviour"/>
    /// ScriptableObjects that govern world-space placement.
    ///
    /// At runtime, WitWeaver always checks the scene registry by character ID first before
    /// spawning from the entry prefab. No explicit source-mode flag is required.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1PrefabCharacterRepresentationData.html")]
    [CreateAssetMenu(fileName = "PrefabCharacterRepresentation",
        menuName = "WitWeaver/Character/Representation/Prefab Character Representation")]
    public class PrefabCharacterRepresentationData : CharacterRepresentationBase, IExpressionCatalogProvider
#if UNITY_EDITOR
        , IDialogueLineEditorCustomizable
#endif
    {
        [Header("Shared Expression Pool")]
        [Tooltip("Expressions shared across all configuration entries. An entry's own ExpressionOverrides take priority when the ID is found there.")]
        [FormerlySerializedAs("ExpressionMappings")]
        public List<PrefabExpressionMapping> SharedExpressionMappings = new();

        [Header("Configuration Entries")]
        [Tooltip("Named sets of prefab, presence, and optional expression overrides. Exactly one entry should be marked IsDefault.")]
        public List<PrefabCharacterConfigurationEntry> ConfigurationEntries = new();

        // ------------------------------------------------------------------
        // Entry resolution
        // ------------------------------------------------------------------

        /// <summary>
        /// Returns the entry marked <see cref="PrefabCharacterConfigurationEntry.IsDefault"/>,
        /// or the first entry if none is marked. Returns null when the list is empty.
        /// </summary>
        public PrefabCharacterConfigurationEntry GetDefaultEntry() =>
            ConfigurationEntries.FirstOrDefault(e => e.IsDefault)
            ?? ConfigurationEntries.FirstOrDefault();

        /// <summary>
        /// Returns the entry whose <see cref="PrefabCharacterConfigurationEntry.EntryName"/>
        /// matches <paramref name="entryName"/>. Falls back to <see cref="GetDefaultEntry"/>
        /// when the name is null/empty or no match is found.
        /// </summary>
        public PrefabCharacterConfigurationEntry GetEntry(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return GetDefaultEntry();
            return ConfigurationEntries.FirstOrDefault(e => e.EntryName == entryName)
                   ?? GetDefaultEntry();
        }

        // ------------------------------------------------------------------
        // Expression resolution
        // ------------------------------------------------------------------

        /// <summary>
        /// Resolves an expression mapping for the given ID.
        /// Entry-level overrides are checked first; the shared pool is the fallback.
        /// Returns false when neither source contains the ID.
        /// </summary>
        public bool TryResolveExpression(string id, string entryName, out PrefabExpressionMapping mapping)
        {
            var entry = GetEntry(entryName);
            if (entry?.ExpressionOverrides?.Count > 0)
            {
                mapping = entry.ExpressionOverrides.FirstOrDefault(m => m.ExpressionID == id);
                if (mapping != null) return true;
            }

            mapping = SharedExpressionMappings.FirstOrDefault(m => m.ExpressionID == id);
            return mapping != null;
        }

        /// <summary>Backward-compatible lookup against the shared pool only.</summary>
        public bool TryResolveById(string id, out PrefabExpressionMapping mapping) =>
            TryResolveExpression(id, null, out mapping);

        /// <summary>
        /// Returns the merged expression catalog for the default entry (entry overrides
        /// first, then shared pool). Used by editor dropdowns.
        /// </summary>
        public IReadOnlyList<(string id, string name)> GetExpressionCatalog()
        {
            var result = new List<(string, string)>();
            var seen   = new HashSet<string>();

            var defaultEntry = GetDefaultEntry();
            if (defaultEntry?.ExpressionOverrides != null)
                foreach (var m in defaultEntry.ExpressionOverrides)
                    if (seen.Add(m.ExpressionID))
                        result.Add((m.ExpressionID, m.DisplayName));

            foreach (var m in SharedExpressionMappings)
                if (seen.Add(m.ExpressionID))
                    result.Add((m.ExpressionID, m.DisplayName));

            return result;
        }

        // ------------------------------------------------------------------
        // CharacterRepresentationBase overrides
        // ------------------------------------------------------------------

        public override void ApplyExpression(string expressionId, WitWeaver runtime,
            WitWeaverConversationData conversation, int lineIndex, IWitWeaverCharacterDisplay display)
        {
            if (!TryResolveById(expressionId, out var mapping))
            {
                Debug.LogWarning($"[PrefabCharacterRepresentationData] Expression '{expressionId}' not found on '{name}'.");
                return;
            }

            var actions = mapping.ExpressionActions;
            if (actions == null || actions.Count == 0)
                return;

            var ctx = new ExpressionActionContext
            {
                Runtime        = runtime,
                Conversation   = conversation,
                LineIndex      = lineIndex,
                Representation = this,
                Display        = display,
                ExpressionId   = mapping.ExpressionID
            };

            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action != null)
                    action.ExecuteAction(ctx);
            }
        }

        /// <summary>
        /// Returns the <see cref="PrefabExpressionMapping"/> from <see cref="SharedExpressionMappings"/>
        /// whose <c>ExpressionID</c> matches, or null on a miss or null/empty GUID. Searches the
        /// shared pool only — per-entry <c>ExpressionOverrides</c> are not consulted here.
        /// </summary>
        public override object GetExpressionMappingByGuid(string expressionGuid)
        {
            if (string.IsNullOrEmpty(expressionGuid)) return null;
            return SharedExpressionMappings.FirstOrDefault(m => m.ExpressionID == expressionGuid);
        }

        /// <summary>
        /// Returns <paramref name="expressionId"/> unchanged (including null/empty), by design:
        /// prefab visuals are bound by the spawned display, not resolved here. The spawner
        /// (<c>WitWeaverPrefabRepresentationSpawner.ResolveCharacter</c>) passes the ID to
        /// <see cref="IWitWeaverCharacterDisplay.ApplyExpression"/>, which looks the GUID up on
        /// the bound representation. Consumers rendering prefab representations should route
        /// through the spawner/display path instead of calling this method.
        /// </summary>
        public override object ProcessExpression(string expressionId) => expressionId;

        /// <inheritdoc/>
        public override IReadOnlyList<string> GetConfigurationEntryNames() =>
            ConfigurationEntries.Count > 0
                ? ConfigurationEntries.ConvertAll(e => e.EntryName)
                : null;

#if UNITY_EDITOR
        public override float GetPreviewHeight()
        {
            var entry = GetDefaultEntry();
            return entry?.CharacterPrefab ? 80f : 0f;
        }

        public override void DrawInlineEditorPreview(object mappingData, Rect position)
        {
            var entry = GetDefaultEntry();
            if (entry?.CharacterPrefab == null)
            {
                UnityEditor.EditorGUI.LabelField(position, "No Prefab (assign one in a Configuration Entry)");
                return;
            }

            var tex = UnityEditor.AssetPreview.GetAssetPreview(entry.CharacterPrefab)
                      ?? UnityEditor.AssetPreview.GetMiniThumbnail(entry.CharacterPrefab);

            if (!tex)
            {
                if (Event.current.type == EventType.Repaint)
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                UnityEditor.EditorGUI.LabelField(position, "Generating preview...");
                return;
            }

            Rect fit = FitRectPreserveAspect(position, tex.width, tex.height, 2f);
            GUI.DrawTexture(fit, tex, ScaleMode.ScaleToFit, true);
        }

        private static Rect FitRectPreserveAspect(Rect container, float w, float h, float pad)
        {
            var inner = new Rect(container.x + pad, container.y + pad,
                container.width - 2 * pad, container.height - 2 * pad);

            if (w <= 0f || h <= 0f) return inner;

            float ar      = w / h;
            float targetW = inner.width;
            float targetH = targetW / ar;
            if (targetH > inner.height)
            {
                targetH = inner.height;
                targetW = targetH * ar;
            }

            float x = inner.x + (inner.width  - targetW) * 0.5f;
            float y = inner.y + (inner.height - targetH) * 0.5f;
            return new Rect(x, y, targetW, targetH);
        }

        public Rect DrawDialogueLineOptions(Rect rect, string expressionID,
            UnityEditor.SerializedProperty displayOptionsProperty, float spacing) => rect;

        public float GetDialogueLineOptionsHeight(string expressionID,
            UnityEditor.SerializedProperty displayOptionsProperty) => 0f;
#endif

        private void OnValidate()
        {
            // Validate shared expression pool
            var used = new HashSet<string>();
            foreach (var m in SharedExpressionMappings)
            {
                if (m == null) continue;
                m.EnsureValidId(used);
                m.EnsureValidBasics();
            }

            // Validate each entry's overrides and enforce a single default
            bool foundDefault = false;
            foreach (var entry in ConfigurationEntries)
            {
                if (entry == null) continue;

                if (string.IsNullOrEmpty(entry.EntryName))
                    entry.EntryName = "Unnamed Entry";

                var entryUsed = new HashSet<string>();
                foreach (var m in entry.ExpressionOverrides)
                {
                    if (m == null) continue;
                    m.EnsureValidId(entryUsed);
                    m.EnsureValidBasics();
                }

                if (entry.IsDefault)
                {
                    if (foundDefault)
                    {
                        entry.IsDefault = false;
#if UNITY_EDITOR
                        Debug.LogWarning($"[PrefabCharacterRepresentationData] '{name}' has multiple default entries. Only the first will be treated as default.");
#endif
                    }
                    else
                    {
                        foundDefault = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// A named configuration for a prefab character: the prefab to spawn as a fallback when the
    /// character is not found in the scene registry, the character behavior ScriptableObjects that
    /// control world-space placement, and any entry-specific expression overrides.
    /// </summary>
    [System.Serializable]
    public class PrefabCharacterConfigurationEntry
    {
        [Tooltip("Unique human-readable name for this configuration (shown in dropdowns and inspector selectors).")]
        public string EntryName = "Default";

        [Tooltip("Marks this as the default entry. Only one entry per asset should have this enabled; if none is marked, the first entry is used as the default.")]
        public bool IsDefault;

        [Tooltip("Prefab spawned when the character is not found in the scene registry. Must have an IWitWeaverCharacterDisplay component on it or a child.")]
        public GameObject CharacterPrefab;

        [Tooltip("Character Behaviour ScriptableObjects that control how and where this character is placed in 3D world-space. Behaviours are applied in list order.")]
        public List<WitWeaverCharacterBehaviour> CharacterBehaviours = new();

        [Tooltip("Entry-specific expression mappings. These take priority over SharedExpressionMappings on the representation asset. When empty or no match is found, the shared pool is used.")]
        public List<PrefabExpressionMapping> ExpressionOverrides = new();
    }

    [System.Serializable]
    public sealed class PrefabExpressionMapping
    {
        [SerializeField, Tooltip("Stable unique ID (GUID). Non-editable.")]
        private string expressionID = System.Guid.NewGuid().ToString("N");
        public string ExpressionID => expressionID;

        [Tooltip("Human-readable name shown in dropdowns and inspector list headers.")]
        public string DisplayName;

        [Tooltip("ScriptableObject actions executed when this expression is applied. Evaluated in list order.")]
        public List<BaseExpressionAction> ExpressionActions = new();

        public void EnsureValidId(HashSet<string> usedIds)
        {
            if (string.IsNullOrEmpty(expressionID) || !usedIds.Add(expressionID))
                expressionID = System.Guid.NewGuid().ToString("N");
        }

        public void EnsureValidBasics()
        {
            if (string.IsNullOrEmpty(DisplayName))
                DisplayName = "Unnamed Expression";
        }
    }
}