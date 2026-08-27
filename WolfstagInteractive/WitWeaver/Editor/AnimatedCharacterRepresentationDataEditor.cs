#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.Editor
{
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1AnimatedCharacterRepresentationDataEditor.html")]
[CustomEditor(typeof(AnimatedCharacterRepresentationData))]
    public class AnimatedCharacterRepresentationDataEditor : UnityEditor.Editor
    {
        private ReorderableList _list;
        private static Type[] _payloadTypes;
        private static string[] _payloadTypeNames;

        private const float PreviewHeight = 96f;
        private const double PreviewLoopSeconds = 2.0;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EnsureGuidsIfMissing(serializedObject);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("UseUnscaledTime"));
            EditorGUILayout.Space(4f);

            var duplicateNames = GetDuplicateDisplayNames(serializedObject.FindProperty("ExpressionMappings"));
            if (duplicateNames.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Warning: Duplicate expression names detected: {string.Join(", ", duplicateNames)}. Each expression should have a unique Display Name.",
                    MessageType.Warning);
            }

            if (_list == null)
            {
                BuildList();
            }
            _list.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
            // Continuous repaint keeps the flipbook previews animating.
            Repaint();
        }

        private void OnEnable() => BuildList();

        private static void EnsurePayloadTypes()
        {
            if (_payloadTypes != null) return;

            _payloadTypes = TypeCache.GetTypesDerivedFrom<AnimatedExpressionPayload>()
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
                .OrderBy(t => t.Name)
                .ToArray();

            _payloadTypeNames = new string[_payloadTypes.Length + 1];
            _payloadTypeNames[0] = "(None)";
            for (int i = 0; i < _payloadTypes.Length; i++)
            {
                var nice = ObjectNames.NicifyVariableName(_payloadTypes[i].Name);
                const string suffix = " Animation Payload";
                if (nice.EndsWith(suffix, StringComparison.Ordinal))
                    nice = nice.Substring(0, nice.Length - suffix.Length);
                _payloadTypeNames[i + 1] = nice;
            }
        }

        private static void EnsureGuidsIfMissing(SerializedObject so)
        {
            var listProp = so.FindProperty("ExpressionMappings");
            if (listProp == null || !listProp.isArray) return;

            bool changed = false;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var el = listProp.GetArrayElementAtIndex(i);
                var guidProp = el.FindPropertyRelative("expressionID");

                if (guidProp != null && string.IsNullOrEmpty(guidProp.stringValue))
                {
                    guidProp.stringValue = Guid.NewGuid().ToString("N");
                    changed = true;
                }
            }

            if (changed) so.ApplyModifiedProperties();
        }

        private static HashSet<string> GetDuplicateDisplayNames(SerializedProperty listProp)
        {
            var duplicates = new HashSet<string>();
            if (listProp == null || !listProp.isArray) return duplicates;

            var seen = new Dictionary<string, int>();

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var el = listProp.GetArrayElementAtIndex(i);
                var nameProp = el.FindPropertyRelative("DisplayName");
                if (nameProp == null) continue;

                string name = nameProp.stringValue;
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (seen.ContainsKey(name))
                {
                    duplicates.Add(name);
                }
                else
                {
                    seen[name] = i;
                }
            }

            return duplicates;
        }

        private void BuildList()
        {
            var listProp = serializedObject.FindProperty("ExpressionMappings");
            if (listProp == null) return;

            EnsurePayloadTypes();

            _list = new ReorderableList(serializedObject, listProp, true, true, true, true);

            _list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Expression Mappings");

            _list.elementHeightCallback = i =>
            {
                const float pad = 6f;
                float headerH = EditorGUIUtility.singleLineHeight + 4f;

                var el = listProp.GetArrayElementAtIndex(i);
                float columnsH = Mathf.Max(
                    GetPayloadColumnHeight(el.FindPropertyRelative("PortraitAnimation")),
                    GetPayloadColumnHeight(el.FindPropertyRelative("FullBodyAnimation")));

                var optsProp = el.FindPropertyRelative("DisplayOptions");
                float optsH = optsProp != null ? GetDisplayOptionsHeightExcludingSlot(optsProp) : 0f;

                var actionsProp = el.FindPropertyRelative("ExpressionActions");
                float actionsH = actionsProp != null ? EditorGUI.GetPropertyHeight(actionsProp, true) : 0f;

                return headerH
                       + columnsH
                       + 4f
                       + optsH
                       + (actionsH > 0f ? 4f + actionsH : 0f)
                       + pad * 2f
                       + 6f;
            };

            _list.drawElementCallback = (rect, index, active, focused) =>
            {
                var el = listProp.GetArrayElementAtIndex(index);

                var nameProp = el.FindPropertyRelative("DisplayName");
                var guidProp = el.FindPropertyRelative("expressionID");
                var portraitProp = el.FindPropertyRelative("PortraitAnimation");
                var fullBodyProp = el.FindPropertyRelative("FullBodyAnimation");
                var optsProp = el.FindPropertyRelative("DisplayOptions");
                var actionsProp = el.FindPropertyRelative("ExpressionActions");

                const float pad = 6f;
                rect = new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, rect.height - pad * 2f);

                // Duplicate detection
                bool isDuplicate = false;
                string currentName = nameProp.stringValue;
                if (!string.IsNullOrWhiteSpace(currentName))
                {
                    for (int i = 0; i < listProp.arraySize; i++)
                    {
                        if (i == index) continue;
                        var otherName = listProp.GetArrayElementAtIndex(i).FindPropertyRelative("DisplayName");
                        if (otherName != null && otherName.stringValue == currentName)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                }

                // Header row
                var headerRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);

                float warningIconWidth = isDuplicate ? 20f : 0f;
                float nameWidth = (headerRect.width - warningIconWidth) * 0.60f;

                if (isDuplicate)
                {
                    var warningRect = new Rect(headerRect.x, headerRect.y, warningIconWidth, headerRect.height);
                    var warningContent = new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml"))
                    {
                        tooltip = "Duplicate name detected! Each expression should have a unique Display Name."
                    };
                    EditorGUI.LabelField(warningRect, warningContent);
                }

                var nameRect = new Rect(headerRect.x + warningIconWidth, headerRect.y, nameWidth, headerRect.height);
                var guidLabelRect = new Rect(nameRect.xMax + 8f, headerRect.y, 44f, headerRect.height);
                var guidRect = new Rect(guidLabelRect.xMax + 4f, headerRect.y,
                    headerRect.xMax - (guidLabelRect.xMax + 4f), headerRect.height);

                if (isDuplicate)
                {
                    var oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.8f, 0.6f);
                    nameProp.stringValue = EditorGUI.TextField(nameRect, "Display Name", nameProp.stringValue);
                    GUI.backgroundColor = oldColor;
                }
                else
                {
                    nameProp.stringValue = EditorGUI.TextField(nameRect, "Display Name", nameProp.stringValue);
                }

                EditorGUI.LabelField(guidLabelRect, "GUID");
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.SelectableLabel(guidRect, guidProp != null ? guidProp.stringValue : "(missing)");
                }

                // Two payload columns
                float colGap = 12f;
                float colWidth = (rect.width - colGap) * 0.5f;
                float topY = headerRect.yMax + 6f;
                float columnsH = Mathf.Max(GetPayloadColumnHeight(portraitProp), GetPayloadColumnHeight(fullBodyProp));

                var portraitRect = new Rect(rect.x, topY, colWidth, columnsH);
                var fullBodyRect = new Rect(rect.x + colWidth + colGap, topY, colWidth, columnsH);

                float oldLW = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 80f;
                DrawPayloadColumn(portraitRect, portraitProp, "Portrait");
                DrawPayloadColumn(fullBodyRect, fullBodyProp, "Full Body");
                EditorGUIUtility.labelWidth = oldLW;

                // Display Options
                float currentY = topY + columnsH + 4f;
                if (optsProp != null)
                {
                    float optsH = GetDisplayOptionsHeightExcludingSlot(optsProp);
                    var optsRect = new Rect(rect.x, currentY, rect.width, optsH);
                    DrawDisplayOptionsExcludingSlot(optsRect, optsProp, new GUIContent("Default Display Options"));
                    currentY += optsH + 4f;
                }
                if (actionsProp != null)
                {
                    float actionsH = EditorGUI.GetPropertyHeight(actionsProp, true);
                    var actionsRect = new Rect(rect.x, currentY, rect.width, actionsH);
                    EditorGUI.PropertyField(actionsRect, actionsProp, new GUIContent("Expression Actions"), true);
                }
            };
        }

        // ------------------------------------------------------------------
        // Payload columns
        // ------------------------------------------------------------------

        private static float GetPayloadColumnHeight(SerializedProperty payloadProp)
        {
            // backend dropdown + payload fields + preview box
            float h = EditorGUIUtility.singleLineHeight + 2f;
            if (payloadProp != null && payloadProp.managedReferenceValue != null)
                h += EditorGUI.GetPropertyHeight(payloadProp, true) + 2f;
            return h + PreviewHeight;
        }

        private void DrawPayloadColumn(Rect rect, SerializedProperty payloadProp, string label)
        {
            if (payloadProp == null) return;

            EnsurePayloadTypes();

            var dropdownRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);

            var current = payloadProp.managedReferenceValue as AnimatedExpressionPayload;
            int currentIndex = 0;
            if (current != null)
            {
                int found = Array.IndexOf(_payloadTypes, current.GetType());
                if (found >= 0) currentIndex = found + 1;
            }

            int newIndex = EditorGUI.Popup(dropdownRect, label, currentIndex, _payloadTypeNames);
            if (newIndex != currentIndex)
            {
                payloadProp.managedReferenceValue =
                    newIndex == 0 ? null : Activator.CreateInstance(_payloadTypes[newIndex - 1]);
                current = payloadProp.managedReferenceValue as AnimatedExpressionPayload;
            }

            float y = dropdownRect.yMax + 2f;

            if (payloadProp.managedReferenceValue != null)
            {
                float fieldsH = EditorGUI.GetPropertyHeight(payloadProp, true);
                var fieldsRect = new Rect(rect.x, y, rect.width, fieldsH);
                EditorGUI.PropertyField(fieldsRect, payloadProp, new GUIContent("Settings"), true);
                y += fieldsH + 2f;
            }

            var previewRect = new Rect(rect.x, y, rect.width, Mathf.Max(0f, rect.yMax - y));
            EditorGUI.DrawRect(previewRect, new Color(0f, 0f, 0f, 0.04f));

            // Animated preview: loops normalized time so flipbooks play in the inspector.
            float previewTime = (float)(EditorApplication.timeSinceStartup % PreviewLoopSeconds / PreviewLoopSeconds);
            var sprite = current?.GetPreviewSprite(previewTime);
            if (sprite == null || sprite.texture == null) return;

            var tex = sprite.texture;
            var r = sprite.rect;
            var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);

            float aspect = r.width / r.height;
            float targetW = previewRect.height * aspect;
            float targetH = previewRect.height;

            if (targetW > previewRect.width)
            {
                targetW = previewRect.width;
                targetH = previewRect.width / aspect;
            }

            var fit = new Rect(
                previewRect.x + (previewRect.width - targetW) * 0.5f,
                previewRect.y + (previewRect.height - targetH) * 0.5f,
                targetW, targetH
            );

            GUI.DrawTextureWithTexCoords(fit, tex, uv, true);
        }

        // ------------------------------------------------------------------
        // Display options (DisplaySlot is a per-line concern, hidden here)
        // ------------------------------------------------------------------

        private static float GetDisplayOptionsHeightExcludingSlot(SerializedProperty optsProp)
        {
            float h = EditorGUIUtility.singleLineHeight; // foldout header
            if (!optsProp.isExpanded) return h;

            var iter = optsProp.Copy();
            var end = optsProp.GetEndProperty();
            if (iter.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iter, end)) break;
                    if (iter.name == "DisplaySlot") continue;
                    h += EditorGUI.GetPropertyHeight(iter, true) + EditorGUIUtility.standardVerticalSpacing;
                }
                while (iter.NextVisible(false));
            }
            return h;
        }

        private static void DrawDisplayOptionsExcludingSlot(Rect rect, SerializedProperty optsProp, GUIContent label)
        {
            var foldRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            optsProp.isExpanded = EditorGUI.Foldout(foldRect, optsProp.isExpanded, label, true);
            if (!optsProp.isExpanded) return;

            EditorGUI.indentLevel++;
            float y = foldRect.yMax + EditorGUIUtility.standardVerticalSpacing;

            var iter = optsProp.Copy();
            var end = optsProp.GetEndProperty();
            if (iter.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iter, end)) break;
                    if (iter.name == "DisplaySlot") continue;
                    float propH = EditorGUI.GetPropertyHeight(iter, true);
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, propH), iter, true);
                    y += propH + EditorGUIUtility.standardVerticalSpacing;
                }
                while (iter.NextVisible(false));
            }
            EditorGUI.indentLevel--;
        }
    }
}
#endif
