// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace WolfstagInteractive.WitWeaver.Editor
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1PagedListUtility.html")]
    public static class PagedListUtility
    {
        private const string PREF_KEY_PREFIX = "PagedListUtility_";
        private const string PAGE_SIZE_KEY_SUFFIX = "_PageSize";

        private static readonly Dictionary<string, bool> FoldoutStates = new();
        private static readonly Dictionary<string, (float height, int hash)> HeightCache = new();

        private static readonly GUIStyle FoldoutStyle = new(EditorStyles.foldout);
        private static readonly GUIStyle BoxStyle = new(EditorStyles.helpBox);
        private static readonly GUIStyle MiniLabel = new(EditorStyles.miniLabel);

        private static readonly List<SerializedProperty> TempVisibleProps = new();

        public static void DrawPagedList(SerializedProperty listProp, int defaultPageSize = 20)
        {
            if (listProp == null || !listProp.isArray)
            {
                EditorGUILayout.PropertyField(listProp, true);
                return;
            }

            string uniqueKey = PREF_KEY_PREFIX +
                               listProp.serializedObject.targetObject.GetEntityId() + "_" +
                               listProp.propertyPath;

            int pageSize = Mathf.Max(1, EditorPrefs.GetInt(uniqueKey + PAGE_SIZE_KEY_SUFFIX, defaultPageSize));
            int total = listProp.arraySize;
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(total / (float)pageSize));
            int currentPage = Mathf.Clamp(SessionState.GetInt(uniqueKey, 0), 0, totalPages - 1);

            listProp.isExpanded = EditorGUILayout.Foldout(
                listProp.isExpanded,
                $"{ObjectNames.NicifyVariableName(listProp.displayName)} ({total})",
                true);

            if (!listProp.isExpanded)
                return;

            EditorGUI.indentLevel++;
            DrawToolbar(ref currentPage, ref pageSize, total, totalPages, uniqueKey);

            // Compute visible range
            int start = Mathf.Clamp(currentPage * pageSize, 0, Mathf.Max(0, total - 1));
            int end = Mathf.Min(start + pageSize, total);

            // Cache only visible range
            TempVisibleProps.Clear();
            for (int i = start; i < end; i++)
                TempVisibleProps.Add(listProp.GetArrayElementAtIndex(i));

            EditorGUILayout.BeginVertical(BoxStyle);

            foreach (var element in TempVisibleProps)
                DrawElement(element);

            EditorGUILayout.EndVertical();

            // Footer
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Page {currentPage + 1}/{totalPages}  •  Total: {total}", MiniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            SessionState.SetInt(uniqueKey, currentPage);
            EditorGUI.indentLevel--;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        private static void DrawToolbar(ref int currentPage, ref int pageSize, int total, int totalPages, string uniqueKey)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("First", EditorStyles.toolbarButton, GUILayout.Width(45))) currentPage = 0;
            if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(25))) currentPage = Mathf.Max(currentPage - 1, 0);
            GUILayout.Label($"{currentPage + 1}/{totalPages}", GUILayout.Width(60));
            if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(25))) currentPage = Mathf.Min(currentPage + 1, totalPages - 1);
            if (GUILayout.Button("Last", EditorStyles.toolbarButton, GUILayout.Width(45))) currentPage = totalPages - 1;

            GUILayout.Space(10);
            int start = Mathf.Clamp(currentPage * pageSize, 0, Mathf.Max(0, total - 1));
            int end = Mathf.Min(start + pageSize, total);
            GUILayout.Label($"Showing {start + 1}–{end} of {total}", MiniLabel);
            GUILayout.FlexibleSpace();

            GUILayout.Label("Items per page", EditorStyles.miniLabel, GUILayout.Width(90));
            int newPageSize = EditorGUILayout.DelayedIntField(pageSize, GUILayout.Width(45));
            if (newPageSize != pageSize && newPageSize > 0)
            {
                pageSize = newPageSize;
                EditorPrefs.SetInt(uniqueKey + PAGE_SIZE_KEY_SUFFIX, pageSize);
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(3);
        }

        // ─────────────────────────────────────────────────────────────────────────────
       

        private static float GetCachedHeight(SerializedProperty prop)
        {
            if (prop == null)
                return EditorGUIUtility.singleLineHeight;

            string key = prop.propertyPath;

            // Compute a simple hash of structure (child count + expansion state)
            int hash = prop.hasVisibleChildren
                ? prop.Copy().CountRemaining() ^ (prop.isExpanded ? 1 : 0)
                : (prop.isExpanded ? 17 : 13);

            if (HeightCache.TryGetValue(key, out var entry))
            {
                if (entry.hash == hash)
                    return entry.height; // still valid
            }

            float computed = EditorGUI.GetPropertyHeight(prop, true);
            HeightCache[key] = (computed, hash);
            return computed;
        }
        private static void DrawElement(SerializedProperty element)
        {
            string id = element.propertyPath;
            if (!FoldoutStates.ContainsKey(id))
                FoldoutStates[id] = false;

            string preview = TryGetPreviewText(element);
            string index = GetIndex(element);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            FoldoutStates[id] = EditorGUILayout.Foldout(FoldoutStates[id], $"Line {index}", true);
            GUILayout.FlexibleSpace();
            GUILayout.Label(preview, MiniLabel, GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();

            if (FoldoutStates[id])
            {
                EditorGUI.indentLevel++;

                // Instead of fixed-height rect, just let Unity layout it dynamically.
                // Cache only for future layout passes — don’t constrain height now.
                float newHeight = EditorGUI.GetPropertyHeight(element, true);
                if (!HeightCache.TryGetValue(id, out var entry) || Mathf.Abs(entry.height - newHeight) > 1f)
                    HeightCache[id] = (newHeight, entry.hash);

                // Draw the actual property with full layout awareness
                EditorGUILayout.PropertyField(element, GUIContent.none, true);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }



        // ─────────────────────────────────────────────────────────────────────────────
        private static string TryGetPreviewText(SerializedProperty prop)
        {
            var locProp = prop.FindPropertyRelative("LocalizedDialogues");
            if (locProp == null || !locProp.isArray || locProp.arraySize == 0)
                return "(no text)";

            string lang = WitWeaverLanguageManager.Instance?.CurrentLanguage ?? "EN";
            for (int i = 0; i < locProp.arraySize; i++)
            {
                var el = locProp.GetArrayElementAtIndex(i);
                var langProp = el.FindPropertyRelative("Language");
                var textProp = el.FindPropertyRelative("Text");
                if (langProp != null && textProp != null &&
                    string.Equals(langProp.stringValue, lang, System.StringComparison.OrdinalIgnoreCase))
                {
                    return TrimPreview(textProp.stringValue);
                }
            }
            var fallback = locProp.GetArrayElementAtIndex(0)?.FindPropertyRelative("Text")?.stringValue ?? "(no text)";
            return TrimPreview(fallback);
        }

        private static string TrimPreview(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(no text)";
            return s.Length > 60 ? s.Substring(0, 60) + "..." : s;
        }

        private static string GetIndex(SerializedProperty prop)
        {
            string path = prop.propertyPath;
            int start = path.LastIndexOf('[') + 1;
            int end = path.LastIndexOf(']');
            return (start >= 0 && end > start) ? path[start..end] : "?";
        }
    }
}
#endif