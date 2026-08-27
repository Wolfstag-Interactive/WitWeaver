#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.Editor
{
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1SpriteCharacterRepresentationDataEditor.html")]
    [CustomEditor(typeof(SpriteCharacterRepresentationData))]
    public class SpriteCharacterRepresentationDataEditor : UnityEditor.Editor
    {
        private ReorderableList _list;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EnsureGuidsIfMissing(serializedObject);
            
            // Check for duplicate names and show warning at the top
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
            Repaint();
        }

        private void OnEnable() => BuildList();

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
                    guidProp.stringValue = System.Guid.NewGuid().ToString("N");
                    changed = true;
                }
            }

            if (changed) so.ApplyModifiedProperties();
        }

        private static System.Collections.Generic.HashSet<string> GetDuplicateDisplayNames(SerializedProperty listProp)
        {
            var duplicates = new System.Collections.Generic.HashSet<string>();
            if (listProp == null || !listProp.isArray) return duplicates;

            var seen = new System.Collections.Generic.Dictionary<string, int>();
            
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var el = listProp.GetArrayElementAtIndex(i);
                var nameProp = el.FindPropertyRelative("DisplayName") ?? el.FindPropertyRelative("Name");
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

            _list = new ReorderableList(serializedObject, listProp, true, true, true, true);

            _list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Expression Mappings");

            _list.elementHeightCallback = i =>
            {
                const float pad = 6f;
                float headerH = EditorGUIUtility.singleLineHeight + 4f;
                float objFieldH = EditorGUIUtility.singleLineHeight;
                float previewH = 96f; // a bit taller for better visibility

                var el = listProp.GetArrayElementAtIndex(i);
                var optsProp = el.FindPropertyRelative("DisplayOptions");
                float optsH = optsProp != null ? GetDisplayOptionsHeightExcludingPosition(optsProp) : 0f;

                // NEW: account for ExpressionActions height
                var actionsProp = el.FindPropertyRelative("ExpressionActions");
                float actionsH = actionsProp != null ? EditorGUI.GetPropertyHeight(actionsProp, true) : 0f;

                // header + (object field + preview) + spacing + display options + spacing + actions + padding
                return headerH
                       + (objFieldH + previewH)
                       + 4f
                       + optsH
                       + (actionsH > 0f ? 4f + actionsH : 0f)
                       + pad * 2f
                       + 6f;
            };

            _list.drawElementCallback = (rect, index, active, focused) =>
            {
                var el = listProp.GetArrayElementAtIndex(index);

                var nameProp  = el.FindPropertyRelative("DisplayName") ?? el.FindPropertyRelative("Name");
                var guidProp  = el.FindPropertyRelative("expressionID");
                var portProp  = el.FindPropertyRelative("PortraitSprite");
                var fullProp  = el.FindPropertyRelative("FullBodySprite");
                var optsProp  = el.FindPropertyRelative("DisplayOptions");
                var actionsProp = el.FindPropertyRelative("ExpressionActions"); // NEW

                const float pad = 6f;
                rect = new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, rect.height - pad * 2f);

                // Duplicate detection
                bool isDuplicate = false;
                string currentName = nameProp.stringValue;
                if (!string.IsNullOrWhiteSpace(currentName))
                {
                    int duplicateCount = 0;
                    for (int i = 0; i < listProp.arraySize; i++)
                    {
                        if (i == index) continue;
                        var otherEl = listProp.GetArrayElementAtIndex(i);
                        var otherName = otherEl.FindPropertyRelative("DisplayName") ?? otherEl.FindPropertyRelative("Name");
                        if (otherName != null && otherName.stringValue == currentName)
                        {
                            duplicateCount++;
                        }
                    }
                    isDuplicate = duplicateCount > 0;
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

                // Two columns (object field + preview)
                float colGap = 12f;
                float colWidth = (rect.width - colGap) * 0.5f;
                float topY = headerRect.yMax + 6f;
                float totalColH = EditorGUIUtility.singleLineHeight + 96f;

                var portRect = new Rect(rect.x, topY, colWidth, totalColH);
                var fullRect = new Rect(rect.x + colWidth + colGap, topY, colWidth, totalColH);

                float oldLW = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 80f;
                DrawSpriteFieldWithPreview(portRect, portProp, "Portrait");
                DrawSpriteFieldWithPreview(fullRect, fullProp, "Full Body");
                EditorGUIUtility.labelWidth = oldLW;

                // Display Options
                float currentY = topY + totalColH + 4f;
                if (optsProp != null)
                {
                    float optsH = GetDisplayOptionsHeightExcludingPosition(optsProp);
                    var optsRect = new Rect(rect.x, currentY, rect.width, optsH);
                    DrawDisplayOptionsExcludingPosition(optsRect, optsProp, new GUIContent("Default Display Options"));
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

        private static float GetDisplayOptionsHeightExcludingPosition(SerializedProperty optsProp)
        {
            float h = EditorGUIUtility.singleLineHeight; // foldout header
            if (!optsProp.isExpanded) return h;

            var iter = optsProp.Copy();
            var end  = optsProp.GetEndProperty();
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

        private static void DrawDisplayOptionsExcludingPosition(Rect rect, SerializedProperty optsProp, GUIContent label)
        {
            var foldRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            optsProp.isExpanded = EditorGUI.Foldout(foldRect, optsProp.isExpanded, label, true);
            if (!optsProp.isExpanded) return;

            EditorGUI.indentLevel++;
            float y = foldRect.yMax + EditorGUIUtility.standardVerticalSpacing;

            var iter = optsProp.Copy();
            var end  = optsProp.GetEndProperty();
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

        private static void DrawSpriteFieldWithPreview(Rect rect, SerializedProperty spriteProp, string label)
        {
            var fieldRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(fieldRect, spriteProp, new GUIContent(label));

            var previewRect = new Rect(rect.x, fieldRect.yMax + 2f, rect.width,
                Mathf.Max(0f, rect.height - (EditorGUIUtility.singleLineHeight + 2f)));

            var sprite = spriteProp != null ? spriteProp.objectReferenceValue as Sprite : null;

            EditorGUI.DrawRect(previewRect, new Color(0f, 0f, 0f, 0.04f));

            if (sprite == null || sprite.texture == null) return;

            var tex = sprite.texture;
            var r   = sprite.rect;
            var uv  = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);

            float aspect  = r.width / r.height;
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
    }
}
#endif