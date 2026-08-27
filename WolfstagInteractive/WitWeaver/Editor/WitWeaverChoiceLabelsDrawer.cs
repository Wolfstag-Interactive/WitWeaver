using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    /// <summary>
    /// Renders the <see cref="WitWeaverConversationData.ChoiceOption.Labels"/> list as one editable
    /// text row per language in <see cref="WitWeaverSettings.SupportedLanguages"/>, so each choice
    /// gets one button string per locale.
    ///
    /// Choice labels are inspector-owned text and never live in YAML. Entries whose language is no
    /// longer listed in settings are shown as orphans with an explicit remove button. They are
    /// never deleted automatically.
    ///
    /// All mutation goes through <see cref="SerializedProperty"/> so Undo works without the drawer
    /// touching the managed list on the target object.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverChoiceLabelsDrawer.html")]
    public static class WitWeaverChoiceLabelsDrawer
    {
        private const float k_Spacing        = 2f;
        private const float k_LangLabelWidth = 52f;
        private const float k_ButtonWidth    = 20f;
        private const float k_IconWidth      = 20f;
        private const float k_HelpBoxHeight  = 34f;

        private static readonly GUIContent GC_LabelsHeader = new("Labels");
        private static readonly GUIContent GC_OrphanHeader = new("Unused Languages",
            "These entries are stored on the choice but their language is not listed in " +
            "WitWeaver settings. They are kept until removed explicitly.");
        private static readonly GUIContent GC_OrphanIcon = new(string.Empty,
            "Language not listed in WitWeaver settings");
        private static readonly GUIContent GC_Remove = new("x", "Remove this label entry");
        private static readonly GUIContent GC_NoLanguages = new(
            "No supported languages configured. Add them under WitWeaver settings to get one label field per locale.");
        private static readonly GUIContent GC_MultiEdit = new("Labels",
            "Per-locale choice labels cannot be edited while multiple values are selected.");

        /// <summary>
        /// Returns the project-wide language list that drives the label rows.
        /// Null or empty makes the drawer fall back to legacy Language/Text pair editing.
        /// </summary>
        public static IList<string> GetSupportedLanguages()
        {
            return WitWeaverSettings.Instance?.SupportedLanguages;
        }

        /// <summary>
        /// Appends an empty entry for every supported language that has no entry yet.
        /// Existing entries are never reordered or rewritten. Matching is case-insensitive so
        /// legacy hand-authored codes such as "EN" are recognised.
        /// </summary>
        public static void SyncMissingLanguages(SerializedProperty labelsProp, IList<string> supported)
        {
            if (labelsProp == null || !labelsProp.isArray) return;
            if (supported == null || supported.Count == 0) return;
            if (labelsProp.hasMultipleDifferentValues) return;
            if (labelsProp.serializedObject.isEditingMultipleObjects) return;

            // Collect first so the array is not mutated while it is being scanned.
            List<string> missing = null;
            for (int i = 0; i < supported.Count; i++)
            {
                var lang = supported[i];
                if (string.IsNullOrWhiteSpace(lang)) continue;
                if (FindEntryIndex(labelsProp, lang) >= 0) continue;

                missing ??= new List<string>();
                missing.Add(lang);
            }

            if (missing == null) return;

            foreach (var lang in missing)
            {
                int insertAt = labelsProp.arraySize;
                labelsProp.InsertArrayElementAtIndex(insertAt);

                // InsertArrayElementAtIndex clones the preceding element, so every field has to be
                // overwritten or the new row silently inherits another language's text and clip.
                var inserted = labelsProp.GetArrayElementAtIndex(insertAt);
                var langProp = inserted.FindPropertyRelative("Language");
                var textProp = inserted.FindPropertyRelative("Text");
                var clipProp = inserted.FindPropertyRelative("Clip");

                if (langProp != null) langProp.stringValue = lang;
                if (textProp != null) textProp.stringValue = string.Empty;
                if (clipProp != null) clipProp.objectReferenceValue = null;
            }

            // Applying here (rather than mutating the managed list) is what gives these
            // structural changes Undo support.
            labelsProp.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// EditorGUILayout wrapper around <see cref="Draw"/>, for inspectors that use layout mode
        /// rather than manual rects. Reserves exactly the height the rect-based draw needs so both
        /// paths share one implementation.
        /// </summary>
        /// <param name="drawHeader">
        /// False when the surrounding inspector already labels this block, so the built-in
        /// "Labels" header would just repeat it.
        /// </param>
        public static void DrawLayout(SerializedProperty labelsProp, IList<string> supported,
            bool drawHeader = true)
        {
            float height = GetHeight(labelsProp, supported, drawHeader);
            var rect = EditorGUILayout.GetControlRect(false, height);
            Draw(rect, labelsProp, supported, drawHeader);
        }

        /// <summary>
        /// Draws the label rows and returns the advanced rect. Mirrors <see cref="GetHeight"/>.
        /// </summary>
        public static Rect Draw(Rect rect, SerializedProperty labelsProp, IList<string> supported,
            bool drawHeader = true)
        {
            float line = EditorGUIUtility.singleLineHeight;

            if (labelsProp == null || !labelsProp.isArray)
            {
                EditorGUI.LabelField(rect, GC_LabelsHeader, new GUIContent("Not available."));
                rect.y += line + k_Spacing;
                return rect;
            }

            // Multi-object edits get a disabled placeholder and no sync writes.
            if (labelsProp.hasMultipleDifferentValues || labelsProp.serializedObject.isEditingMultipleObjects)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.LabelField(rect, GC_MultiEdit, new GUIContent("-"));
                rect.y += line + k_Spacing;
                return rect;
            }

            if (drawHeader)
            {
                EditorGUI.LabelField(rect, GC_LabelsHeader, EditorStyles.boldLabel);
                rect.y += line + k_Spacing;
            }

            bool hasSupported = supported != null && supported.Count > 0;

            if (!hasSupported)
            {
                var helpRect = new Rect(rect.x, rect.y, rect.width, k_HelpBoxHeight);
                EditorGUI.HelpBox(helpRect, GC_NoLanguages.text, MessageType.Info);
                rect.y += k_HelpBoxHeight + k_Spacing;

                // Legacy behaviour: everything the choice already stores stays editable, including
                // the language code, so authors are not locked out before settings are configured.
                for (int i = 0; i < labelsProp.arraySize; i++)
                {
                    var entry = labelsProp.GetArrayElementAtIndex(i);
                    rect = DrawLegacyRow(rect, entry);
                }

                return rect;
            }

            SyncMissingLanguages(labelsProp, supported);

            for (int i = 0; i < supported.Count; i++)
            {
                var lang = supported[i];
                if (string.IsNullOrWhiteSpace(lang)) continue;

                var rowRect   = EditorGUI.IndentedRect(new Rect(rect.x, rect.y, rect.width, line));
                var labelRect = new Rect(rowRect.x, rowRect.y, k_LangLabelWidth, line);
                var fieldRect = new Rect(rowRect.x + k_LangLabelWidth + k_Spacing, rowRect.y,
                    Mathf.Max(20f, rowRect.width - k_LangLabelWidth - k_Spacing), line);

                int index = FindEntryIndex(labelsProp, lang);
                var textProp = index >= 0
                    ? labelsProp.GetArrayElementAtIndex(index).FindPropertyRelative("Text")
                    : null;

                // IndentedRect already applied the indent, and LabelField/TextField would apply it
                // a second time, pushing the language code out of its column. Zero it for the row.
                int prevIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                EditorGUI.LabelField(labelRect, lang.ToUpperInvariant());

                if (textProp != null)
                {
                    textProp.stringValue = EditorGUI.TextField(fieldRect, textProp.stringValue);
                }
                else
                {
                    // Sync could not run this frame. Reserve the row so the drawn layout keeps
                    // matching GetHeight; the field becomes editable on the next repaint.
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUI.TextField(fieldRect, string.Empty);
                }
                EditorGUI.indentLevel = prevIndent;

                rect.y += line + k_Spacing;
            }

            var orphans = CollectOrphanIndices(labelsProp, supported);
            if (orphans.Count > 0)
            {
                EditorGUI.LabelField(rect, GC_OrphanHeader, EditorStyles.miniBoldLabel);
                rect.y += line + k_Spacing;

                // Walk backwards so a removal does not shift the indices still to be drawn.
                bool removedThisFrame = false;
                for (int i = orphans.Count - 1; i >= 0; i--)
                {
                    if (removedThisFrame)
                    {
                        // The array changed underneath us. Consume the remaining rows without
                        // drawing them so the section still occupies the height already reserved;
                        // they render correctly on the next repaint.
                        rect.y += line + k_Spacing;
                        continue;
                    }

                    removedThisFrame = DrawOrphanRow(ref rect, labelsProp, orphans[i]);
                }
            }

            return rect;
        }

        /// <summary>
        /// Height consumed by <see cref="Draw"/>. Computed from the supported-language count rather
        /// than the stored entry count so the height stays stable on the frame where sync runs.
        /// </summary>
        public static float GetHeight(SerializedProperty labelsProp, IList<string> supported,
            bool drawHeader = true)
        {
            float line = EditorGUIUtility.singleLineHeight;

            if (labelsProp == null || !labelsProp.isArray)
                return line + k_Spacing;

            if (labelsProp.hasMultipleDifferentValues || labelsProp.serializedObject.isEditingMultipleObjects)
                return line + k_Spacing;

            float h = drawHeader ? line + k_Spacing : 0f;

            bool hasSupported = supported != null && supported.Count > 0;

            if (!hasSupported)
            {
                h += k_HelpBoxHeight + k_Spacing;
                h += labelsProp.arraySize * (line + k_Spacing);
                return h;
            }

            for (int i = 0; i < supported.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(supported[i])) continue;
                h += line + k_Spacing;
            }

            int orphanCount = CountOrphans(labelsProp, supported);
            if (orphanCount > 0)
                h += (orphanCount + 1) * (line + k_Spacing); // Orphan rows plus their header

            return h;
        }

        // --------------------------------------------------
        // Helpers
        // --------------------------------------------------

        private static Rect DrawLegacyRow(Rect rect, SerializedProperty entry)
        {
            float line = EditorGUIUtility.singleLineHeight;

            var langProp = entry.FindPropertyRelative("Language");
            var textProp = entry.FindPropertyRelative("Text");

            var rowRect   = EditorGUI.IndentedRect(new Rect(rect.x, rect.y, rect.width, line));
            float codeWidth = k_LangLabelWidth + 12f;
            var codeRect  = new Rect(rowRect.x, rowRect.y, codeWidth, line);
            var textRect  = new Rect(rowRect.x + codeWidth + k_Spacing, rowRect.y,
                Mathf.Max(20f, rowRect.width - codeWidth - k_Spacing), line);

            int prevIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            if (langProp != null) langProp.stringValue = EditorGUI.TextField(codeRect, langProp.stringValue);
            if (textProp != null) textProp.stringValue = EditorGUI.TextField(textRect, textProp.stringValue);
            EditorGUI.indentLevel = prevIndent;

            rect.y += line + k_Spacing;
            return rect;
        }

        /// <summary>
        /// Draws one orphaned entry. Returns true when the entry was removed, which invalidates
        /// the caller's index list for the rest of this frame.
        /// </summary>
        private static bool DrawOrphanRow(ref Rect rect, SerializedProperty labelsProp, int index)
        {
            float line = EditorGUIUtility.singleLineHeight;

            var entry    = labelsProp.GetArrayElementAtIndex(index);
            var langProp = entry.FindPropertyRelative("Language");
            var textProp = entry.FindPropertyRelative("Text");

            var rowRect = EditorGUI.IndentedRect(new Rect(rect.x, rect.y, rect.width, line));

            var iconRect   = new Rect(rowRect.x, rowRect.y, k_IconWidth, line);
            var labelRect  = new Rect(iconRect.xMax, rowRect.y, k_LangLabelWidth, line);
            var buttonRect = new Rect(rowRect.xMax - k_ButtonWidth, rowRect.y, k_ButtonWidth, line);
            var fieldRect  = new Rect(labelRect.xMax + k_Spacing, rowRect.y,
                Mathf.Max(20f, buttonRect.x - labelRect.xMax - (k_Spacing * 2f)), line);

            int prevIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var warnIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
            GC_OrphanIcon.image = warnIcon != null ? warnIcon.image : null;
            EditorGUI.LabelField(iconRect, GC_OrphanIcon);

            string code = string.IsNullOrEmpty(langProp?.stringValue) ? "(none)" : langProp.stringValue;
            EditorGUI.LabelField(labelRect, new GUIContent(code, "Language not listed in WitWeaver settings"));

            if (textProp != null) textProp.stringValue = EditorGUI.TextField(fieldRect, textProp.stringValue);
            EditorGUI.indentLevel = prevIndent;

            bool removed = false;
            if (GUI.Button(buttonRect, GC_Remove, EditorStyles.miniButton))
            {
                labelsProp.DeleteArrayElementAtIndex(index);
                labelsProp.serializedObject.ApplyModifiedProperties();
                removed = true;
            }

            rect.y += line + k_Spacing;
            return removed;
        }

        private static int FindEntryIndex(SerializedProperty labelsProp, string language)
        {
            for (int i = 0; i < labelsProp.arraySize; i++)
            {
                var langProp = labelsProp.GetArrayElementAtIndex(i).FindPropertyRelative("Language");
                if (langProp == null) continue;
                if (string.Equals(langProp.stringValue, language, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static bool IsSupported(IList<string> supported, string language)
        {
            for (int i = 0; i < supported.Count; i++)
            {
                if (string.Equals(supported[i], language, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static List<int> CollectOrphanIndices(SerializedProperty labelsProp, IList<string> supported)
        {
            var result = new List<int>();
            for (int i = 0; i < labelsProp.arraySize; i++)
            {
                var langProp = labelsProp.GetArrayElementAtIndex(i).FindPropertyRelative("Language");
                string lang = langProp != null ? langProp.stringValue : null;
                if (!IsSupported(supported, lang ?? string.Empty))
                    result.Add(i);
            }
            return result;
        }

        private static int CountOrphans(SerializedProperty labelsProp, IList<string> supported)
        {
            int count = 0;
            for (int i = 0; i < labelsProp.arraySize; i++)
            {
                var langProp = labelsProp.GetArrayElementAtIndex(i).FindPropertyRelative("Language");
                string lang = langProp != null ? langProp.stringValue : null;
                if (!IsSupported(supported, lang ?? string.Empty))
                    count++;
            }
            return count;
        }
    }
}
