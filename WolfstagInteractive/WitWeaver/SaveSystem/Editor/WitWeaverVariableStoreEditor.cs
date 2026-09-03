// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using WolfstagInteractive.WitWeaver.SaveSystem;

namespace WolfstagInteractive.WitWeaver.SaveSystem.Editor
{
    [CustomEditor(typeof(WitWeaverVariableStore))]
    public class WitWeaverVariableStoreEditor : UnityEditor.Editor
    {
        // ── Authored list ──────────────────────────────────────────────────────

        private SerializedProperty _persistentEntriesProp;
        private ReorderableList    _authoredList;

        // Scope is restricted to Global and Conversation in the authored list.
        private static readonly string[] k_AuthoredScopeNames  = { "Global", "Conversation" };
        private static readonly int[]    k_AuthoredScopeValues =
        {
            (int)WitWeaverVariableScope.Global,
            (int)WitWeaverVariableScope.Conversation
        };

        // Column proportions — must sum to 1.0
        private const float k_KeyFrac   = 0.34f;
        private const float k_TypeFrac  = 0.21f;
        private const float k_ScopeFrac = 0.23f;
        private const float k_RoFrac    = 0.22f;

        // ── Filter state ───────────────────────────────────────────────────────

        private string   _textFilter      = string.Empty;
        private int      _scopeFilterIdx; // 0=All, 1=Global, 2=Conversation, 3=Session
        private static readonly string[] k_ScopeFilterLabels = { "All", "Global", "Conversation", "Session" };

        // ── Diff / authored-default tracking ──────────────────────────────────

        // Captured at ExitingEditMode; compared against live runtime values.
        private Dictionary<string, string> _authoredDefaults;

        // ── Repaint timer ──────────────────────────────────────────────────────

        private double       _lastRepaintTime;
        private const double k_RepaintInterval = 0.1;

        // ── Styles (cached) ────────────────────────────────────────────────────

        private GUIStyle _columnHeaderStyle;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            _persistentEntriesProp = serializedObject.FindProperty("_persistentEntries");
            BuildAuthoredList();

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update               += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update               -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!Application.isPlaying) return;
            if (EditorApplication.timeSinceStartup - _lastRepaintTime >= k_RepaintInterval)
            {
                _lastRepaintTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                CaptureAuthoredDefaults();
            else if (state == PlayModeStateChange.EnteredEditMode)
                _authoredDefaults = null;
        }

        private void CaptureAuthoredDefaults()
        {
            _authoredDefaults = new Dictionary<string, string>();
            var store   = (WitWeaverVariableStore)target;
            var entries = store.GetRawEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e?.CoreVariable?.Key != null)
                    _authoredDefaults[e.CoreVariable.Key] = e.CoreVariable.AsString();
            }

            // Collections diff by dirty flag, not content — start the playthrough clean.
            // (Matters when domain reload is disabled in Enter Play Mode Options.)
            store.ClearAllCollectionDirtyFlags();
        }

        private static bool IsCollectionType(WitWeaverVariableType type)
        {
            return type == WitWeaverVariableType.CollectionInt || type == WitWeaverVariableType.CollectionString;
        }

        private static string PairsPropertyName(WitWeaverVariableType type)
        {
            return type == WitWeaverVariableType.CollectionInt
                ? "CoreVariable.CollectionIntPairs"
                : "CoreVariable.CollectionStringPairs";
        }

        // ── ReorderableList construction ───────────────────────────────────────

        private void BuildAuthoredList()
        {
            _authoredList = new ReorderableList(
                serializedObject,
                _persistentEntriesProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true);

            // Two-row header: section title + column labels
            _authoredList.headerHeight        = EditorGUIUtility.singleLineHeight * 2f + 6f;
            _authoredList.drawHeaderCallback  = DrawAuthoredHeader;

            // Two rows per element
            _authoredList.elementHeightCallback = GetAuthoredElementHeight;
            _authoredList.drawElementCallback   = DrawAuthoredElement;

            _authoredList.onAddCallback = OnAddEntry;
        }

        private float GetAuthoredElementHeight(int index)
        {
            float lineH = EditorGUIUtility.singleLineHeight;
            float baseH = lineH * 2f + 10f;

            if (index < 0 || index >= _persistentEntriesProp.arraySize) return baseH;

            var element  = _persistentEntriesProp.GetArrayElementAtIndex(index);
            var typeProp = element.FindPropertyRelative("CoreVariable.Type");
            var type     = (WitWeaverVariableType)typeProp.enumValueIndex;

            // In Play Mode Collection rows show a one-line summary (base height). In edit
            // mode the pairs list is drawn by Unity's default reorderable array field.
            if (!IsCollectionType(type) || Application.isPlaying) return baseH;

            var pairsProp = element.FindPropertyRelative(PairsPropertyName(type));
            float pairsH  = pairsProp != null ? EditorGUI.GetPropertyHeight(pairsProp, true) : lineH;
            return lineH + pairsH + 14f;
        }

        private bool MatchesFilter(int index)
        {
            if (index < 0 || index >= _persistentEntriesProp.arraySize) return true;

            var element = _persistentEntriesProp.GetArrayElementAtIndex(index);

            // Scope filter
            if (_scopeFilterIdx > 0)
            {
                // Session (idx 3) — persistent entries never carry Session scope, so none match.
                if (_scopeFilterIdx == 3) return false;

                int target = _scopeFilterIdx == 1
                    ? (int)WitWeaverVariableScope.Global
                    : (int)WitWeaverVariableScope.Conversation;
                var scopeProp = element.FindPropertyRelative("Scope");
                if (scopeProp != null && scopeProp.enumValueIndex != target) return false;
            }

            // Text filter
            if (!string.IsNullOrEmpty(_textFilter))
            {
                var keyProp = element.FindPropertyRelative("CoreVariable.Key");
                string key = keyProp?.stringValue ?? string.Empty;
                if (key.IndexOf(_textFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            return true;
        }

        private void DrawAuthoredHeader(Rect rect)
        {
            EnsureStyles();

            float lineH = EditorGUIUtility.singleLineHeight;
            float pad   = 2f;

            // Row 1 — section title
            EditorGUI.LabelField(
                new Rect(rect.x, rect.y + pad, rect.width, lineH),
                "Authored Variables",
                EditorStyles.boldLabel);

            // Row 2 — column labels, inset by drag-handle width (~20 px)
            float indent = 20f;
            float colX   = rect.x + indent;
            float colW   = rect.width - indent;
            float y      = rect.y + lineH + pad * 2f;

            EditorGUI.LabelField(new Rect(colX,                                          y, colW * k_KeyFrac   - 2f, lineH), "Key",   _columnHeaderStyle);
            EditorGUI.LabelField(new Rect(colX + colW * k_KeyFrac,                       y, colW * k_TypeFrac  - 2f, lineH), "Type",  _columnHeaderStyle);
            EditorGUI.LabelField(new Rect(colX + colW * (k_KeyFrac + k_TypeFrac),        y, colW * k_ScopeFrac - 2f, lineH), "Scope", _columnHeaderStyle);
            EditorGUI.LabelField(new Rect(colX + colW * (k_KeyFrac + k_TypeFrac + k_ScopeFrac), y, colW * k_RoFrac, lineH), "Read Only", _columnHeaderStyle);
        }

        private void DrawAuthoredElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = _persistentEntriesProp.GetArrayElementAtIndex(index);

            var keyProp        = element.FindPropertyRelative("CoreVariable.Key");
            var typeProp       = element.FindPropertyRelative("CoreVariable.Type");
            var scopeProp      = element.FindPropertyRelative("Scope");
            var isReadOnlyProp = element.FindPropertyRelative("IsReadOnly");

            float lineH = EditorGUIUtility.singleLineHeight;
            float pad   = 3f;
            float x     = rect.x;
            float w     = rect.width;

            // ── Row 1: Key | Type | Scope | Read Only ────────────────────────
            float y1 = rect.y + pad;

            float keyW   = w * k_KeyFrac;
            float typeW  = w * k_TypeFrac;
            float scopeW = w * k_ScopeFrac;
            float roW    = w * k_RoFrac;

            // Key
            EditorGUI.PropertyField(
                new Rect(x, y1, keyW - 2f, lineH),
                keyProp,
                new GUIContent(string.Empty, "Unique key used to read and write this variable at runtime."));
            x += keyW;

            // Type
            EditorGUI.PropertyField(new Rect(x, y1, typeW - 2f, lineH), typeProp, GUIContent.none);
            x += typeW;

            // Scope (restricted popup — Session is never shown for authored entries)
            int currentRaw = scopeProp.enumValueIndex;
            int popupIdx   = 0;
            for (int i = 0; i < k_AuthoredScopeValues.Length; i++)
            {
                if (k_AuthoredScopeValues[i] == currentRaw) { popupIdx = i; break; }
            }
            int newPopupIdx = EditorGUI.Popup(new Rect(x, y1, scopeW - 2f, lineH), popupIdx, k_AuthoredScopeNames);
            scopeProp.enumValueIndex = k_AuthoredScopeValues[newPopupIdx];
            x += scopeW;

            // Read-Only toggle
            float roToggleW = 16f;
            float roLabelW  = roW - roToggleW - 2f;
            EditorGUI.LabelField(
                new Rect(x, y1, roLabelW, lineH),
                new GUIContent("Read Only", "When enabled, runtime write calls to this variable are silently rejected."));
            EditorGUI.PropertyField(
                new Rect(x + roLabelW + 1f, y1, roToggleW, lineH),
                isReadOnlyProp, GUIContent.none);

            // ── Row 2: Default value (edit mode) / authored → current (play mode) ─
            float y2 = rect.y + lineH + pad * 2f + 2f;

            var type = (WitWeaverVariableType)typeProp.enumValueIndex;

            if (IsCollectionType(type))
            {
                if (Application.isPlaying)
                    DrawCollectionRuntimeSummary(new Rect(rect.x, y2, w, lineH), keyProp.stringValue, index);
                else
                {
                    var pairsProp = element.FindPropertyRelative(PairsPropertyName(type));
                    if (pairsProp != null)
                        EditorGUI.PropertyField(
                            new Rect(rect.x + 10f, y2, w - 10f, rect.yMax - y2),
                            pairsProp,
                            new GUIContent("Collection Defaults", "Authored sub-entries. Sub-keys must be unique and non-empty; duplicates keep the first occurrence at runtime."),
                            includeChildren: true);
                }
            }
            else if (Application.isPlaying && _authoredDefaults != null)
            {
                string key      = keyProp.stringValue;
                string authored = _authoredDefaults.TryGetValue(key, out var a) ? a : string.Empty;
                string current  = GetCurrentValueString(element, type);

                if (authored != current)
                {
                    var highlightRect = new Rect(rect.x, y2 - 1f, w, lineH + 2f);
                    EditorGUI.DrawRect(highlightRect, new Color(1f, 0.65f, 0f, 0.18f));
                }

                // Draw: "Default" label  authored-value  →  current-value
                const float labelW = 54f;
                const float gap    = 3f;
                const float arrowW = 18f;
                float areaX = rect.x + labelW + gap;
                float areaW = w - labelW - gap;
                float halfW = (areaW - arrowW) / 2f;

                EditorGUI.LabelField(new Rect(rect.x,            y2, labelW, lineH),
                    new GUIContent("Default", "Authored default → current runtime value."));
                EditorGUI.LabelField(new Rect(areaX,             y2, halfW,  lineH), authored);
                EditorGUI.LabelField(new Rect(areaX + halfW,     y2, arrowW, lineH), "→");
                EditorGUI.LabelField(new Rect(areaX + halfW + arrowW, y2, halfW, lineH), current);
            }
            else
            {
                DrawDefaultValueRow(new Rect(rect.x, y2, w, lineH), element, type);
            }
        }

        // Play Mode: one-line read-only summary of the Collection's current runtime state
        // (session copy if copied-on-write, authored otherwise). The row highlights orange
        // while the entry's dirty flag is set — the flag is the diff, no content comparison.
        private void DrawCollectionRuntimeSummary(Rect rect, string key, int index)
        {
            var store = (WitWeaverVariableStore)target;

            bool dirty = false;
            var raw = store.GetRawEntries();
            if (index >= 0 && index < raw.Count && raw[index] != null)
                dirty = raw[index].IsDirtySinceSnapshot;

            if (dirty)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y - 1f, rect.width, rect.height + 2f),
                    new Color(1f, 0.65f, 0f, 0.18f));

            int count = store.GetCollectionCount(key);
            EditorGUI.LabelField(rect,
                new GUIContent($"Collection - {count} {(count == 1 ? "entry" : "entries")}",
                    "Current runtime sub-entry count. Orange = modified this playthrough."));
        }

        private static string GetCurrentValueString(SerializedProperty element, WitWeaverVariableType type)
        {
            switch (type)
            {
                case WitWeaverVariableType.String:
                    return element.FindPropertyRelative("CoreVariable._stringValue")?.stringValue ?? string.Empty;
                case WitWeaverVariableType.Int:
                    return (element.FindPropertyRelative("CoreVariable._intValue")?.intValue ?? 0).ToString();
                case WitWeaverVariableType.Float:
                    return (element.FindPropertyRelative("CoreVariable._floatValue")?.floatValue ?? 0f).ToString();
                case WitWeaverVariableType.Bool:
                    return (element.FindPropertyRelative("CoreVariable._boolValue")?.boolValue ?? false).ToString();
                default:
                    return string.Empty;
            }
        }

        private static void DrawDefaultValueRow(Rect rect, SerializedProperty element, WitWeaverVariableType type)
        {
            const float labelW = 54f;
            const float gap    = 3f;

            EditorGUI.LabelField(
                new Rect(rect.x, rect.y, labelW, rect.height),
                new GUIContent("Default", "Initial value when the asset is first loaded or reset."));

            var fieldRect = new Rect(rect.x + labelW + gap, rect.y, rect.width - labelW - gap, rect.height);

            switch (type)
            {
                case WitWeaverVariableType.String:
                    EditorGUI.PropertyField(fieldRect, element.FindPropertyRelative("CoreVariable._stringValue"), GUIContent.none);
                    break;
                case WitWeaverVariableType.Int:
                    EditorGUI.PropertyField(fieldRect, element.FindPropertyRelative("CoreVariable._intValue"), GUIContent.none);
                    break;
                case WitWeaverVariableType.Float:
                    EditorGUI.PropertyField(fieldRect, element.FindPropertyRelative("CoreVariable._floatValue"), GUIContent.none);
                    break;
                case WitWeaverVariableType.Bool:
                    EditorGUI.PropertyField(fieldRect, element.FindPropertyRelative("CoreVariable._boolValue"), GUIContent.none);
                    break;
            }
        }

        private void OnAddEntry(ReorderableList list)
        {
            ReorderableList.defaultBehaviours.DoAddButton(list);
            var newElement = _persistentEntriesProp.GetArrayElementAtIndex(_persistentEntriesProp.arraySize - 1);
            newElement.FindPropertyRelative("Scope").enumValueIndex = (int)WitWeaverVariableScope.Global;
            var keyProp = newElement.FindPropertyRelative("CoreVariable.Key");
            if (keyProp != null) keyProp.stringValue = string.Empty;
        }

        // ── Main inspector ─────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            EnsureStyles();
            serializedObject.Update();

            // ── Filter toolbar ────────────────────────────────────────────────
            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Filter", GUILayout.Width(36f));
                _textFilter = EditorGUILayout.TextField(_textFilter);
                GUILayout.Space(8f);
                _scopeFilterIdx = GUILayout.Toolbar(_scopeFilterIdx, k_ScopeFilterLabels, GUILayout.ExpandWidth(false));
            }

            EditorGUILayout.Space(2f);
            // Persistent entries are read-only at runtime — values shown as authored → current.
            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            bool isFiltered = _scopeFilterIdx > 0 || !string.IsNullOrEmpty(_textFilter);
            if (isFiltered)
                DrawFilteredAuthoredList();
            else
                _authoredList.DoLayoutList();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(6f);
            DrawSessionSection();

            serializedObject.ApplyModifiedProperties();
        }

        // ── Filtered authored list (manual draw, bypasses ReorderableList) ─────

        private void DrawFilteredAuthoredList()
        {
            EnsureStyles();

            float lineH = EditorGUIUtility.singleLineHeight;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                // Section title
                EditorGUILayout.LabelField("Authored Variables", EditorStyles.boldLabel);

                // Column headers (no drag-handle indent — there are no handles in filtered view)
                var colRect = GUILayoutUtility.GetRect(0, lineH, GUILayout.ExpandWidth(true));
                float colW = colRect.width;
                float colX = colRect.x;

                EditorGUI.LabelField(new Rect(colX,                                                        colRect.y, colW * k_KeyFrac   - 2f, lineH), "Key",   _columnHeaderStyle);
                EditorGUI.LabelField(new Rect(colX + colW * k_KeyFrac,                                    colRect.y, colW * k_TypeFrac  - 2f, lineH), "Type",  _columnHeaderStyle);
                EditorGUI.LabelField(new Rect(colX + colW * (k_KeyFrac + k_TypeFrac),                     colRect.y, colW * k_ScopeFrac - 2f, lineH), "Scope", _columnHeaderStyle);
                EditorGUI.LabelField(new Rect(colX + colW * (k_KeyFrac + k_TypeFrac + k_ScopeFrac),       colRect.y, colW * k_RoFrac,         lineH), "Read Only", _columnHeaderStyle);

                EditorGUI.DrawRect(new Rect(colRect.x, colRect.yMax, colRect.width, 1f),
                    new Color(0.3f, 0.3f, 0.3f, 0.4f));

                // Matching entries
                int matchCount = 0;
                for (int i = 0; i < _persistentEntriesProp.arraySize; i++)
                {
                    if (!MatchesFilter(i)) continue;

                    var rect = GUILayoutUtility.GetRect(0, GetAuthoredElementHeight(i), GUILayout.ExpandWidth(true));
                    if (matchCount % 2 == 0)
                        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.04f));

                    DrawAuthoredElement(rect, i, false, false);
                    matchCount++;
                }

                if (matchCount == 0)
                    EditorGUILayout.LabelField(
                        "No variables match the current filter.",
                        EditorStyles.centeredGreyMiniLabel);

                // Add button — clears filter so the new entry is immediately visible
                EditorGUILayout.Space(2f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("+ Add Variable", GUILayout.Width(100f)))
                    {
                        OnAddEntry(_authoredList);
                        _textFilter     = string.Empty;
                        _scopeFilterIdx = 0;
                    }
                }
            }
        }

        // ── Session Variables section ──────────────────────────────────────────

        private void DrawSessionSection()
        {
            EditorGUILayout.LabelField("Session Variables", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Session variables exist only at runtime. They are set by game code, " +
                    "never saved to disk, and are not visible outside Play Mode.",
                    MessageType.None);
                return;
            }

            var store          = (WitWeaverVariableStore)target;
            var sessionEntries = store.GetSessionEntries();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.LabelField(
                    GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight),
                    "read only — set at runtime only",
                    EditorStyles.centeredGreyMiniLabel);

                if (sessionEntries == null || sessionEntries.Count == 0)
                {
                    EditorGUILayout.LabelField("No session variables have been set yet.", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Key",   EditorStyles.miniBoldLabel, GUILayout.Width(160f));
                        EditorGUILayout.LabelField("Type",  EditorStyles.miniBoldLabel, GUILayout.Width(60f));
                        EditorGUILayout.LabelField("Value", EditorStyles.miniBoldLabel);
                    }

                    DrawHorizontalRule();

                    using (new EditorGUI.DisabledScope(true))
                    {
                        for (int i = 0; i < sessionEntries.Count; i++)
                        {
                            var entry = sessionEntries[i];
                            if (entry?.CoreVariable == null) continue;

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.LabelField(entry.CoreVariable.Key ?? "(null)", GUILayout.Width(160f));
                                EditorGUILayout.LabelField(entry.CoreVariable.Type.ToString(),  GUILayout.Width(60f));
                                EditorGUILayout.LabelField(entry.CoreVariable.AsString());
                            }
                        }
                    }
                }
            }
            // Repaint is now handled by the interval-based OnEditorUpdate instead of every frame
        }

        private static void DrawHorizontalRule()
        {
            var r = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            r.x     += 2f;
            r.width -= 4f;
            EditorGUI.DrawRect(r, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            GUILayout.Space(2f);
        }

        // ── Style helpers ──────────────────────────────────────────────────────

        private void EnsureStyles()
        {
            if (_columnHeaderStyle == null)
            {
                _columnHeaderStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontStyle = FontStyle.Bold
                };
            }
        }
    }
}