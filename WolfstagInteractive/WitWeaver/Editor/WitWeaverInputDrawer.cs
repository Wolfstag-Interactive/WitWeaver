// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverInputPropertyDrawer.html")]
[CustomPropertyDrawer(typeof(IWitWeaverInput), true)]
    public class WitWeaverInputPropertyDrawer : PropertyDrawer
    {
        private static readonly System.Type[] s_Types =
        {
            typeof(SingleConversationInput),
            typeof(ContainerInput),
            typeof(GraphConversationInput)
        };
        private static readonly string[] s_Tabs = { "Single", "Container", "Graph" };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Extra lines for header + toolbar
            float h = EditorGUIUtility.singleLineHeight * 2 + 4f;

            // Height of the concrete object children
            var copy = property.Copy();
            var end  = copy.GetEndProperty();
            while (copy.NextVisible(true) && !SerializedProperty.EqualContents(copy, end))
            {
                if (copy.name == "managedReferenceFullTypename" || copy.name == "managedReferenceData")
                    continue;
                h += EditorGUI.GetPropertyHeight(copy, true) + 2f;
            }

            // Graph tab: extra rows for the graph asset field and the Open Graph button.
            if (GetManagedType(property) == typeof(GraphConversationInput))
                h += (EditorGUIUtility.singleLineHeight + 2f) * 2;

            return h + 2f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Header
            var header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(header, "Conversation Input", EditorStyles.boldLabel);

            // Toolbar
            var bar = new Rect(position.x, header.yMax + 2, position.width, EditorGUIUtility.singleLineHeight);
            int idx = GetTypeIndex(property);
            int newIdx = GUI.Toolbar(bar, Mathf.Max(0, idx), s_Tabs);
            if (newIdx != idx)
            {
                SetManagedReferenceType(property, s_Types[newIdx]);
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            }

            // Body rect
            var body = new Rect(position.x, bar.yMax + 2, position.width, position.yMax - (bar.yMax + 2));

            // Draw children inline
            EditorGUI.indentLevel++;
            float bodyEnd = DrawChildrenInline(body, property);
            if (GetManagedType(property) == typeof(GraphConversationInput))
                DrawGraphInputExtras(new Rect(body.x, bodyEnd, body.width, position.yMax - bodyEnd), property);
            EditorGUI.indentLevel--;

            // Handle drag-and-drop onto the whole drawer
            HandleDragAndDrop(position, property);
        }

        private static float DrawChildrenInline(Rect rect, SerializedProperty property)
        {
            var copy = property.Copy();
            var end  = copy.GetEndProperty();
            float y = rect.y;

            while (copy.NextVisible(true) && !SerializedProperty.EqualContents(copy, end))
            {
                if (copy.name == "managedReferenceFullTypename" || copy.name == "managedReferenceData")
                    continue;

                float h = EditorGUI.GetPropertyHeight(copy, true);
                var line = new Rect(rect.x, y, rect.width, h);
                EditorGUI.PropertyField(line, copy, true);
                y += h + 2f;
            }
            return y;
        }

        /// <summary>
        /// Graph tab extras below the Conversation field: a status hint and an Open Graph button.
        /// The user assigns only the conversation asset — its companion graph is resolved and
        /// managed automatically by the graph tooling.
        /// </summary>
        private static void DrawGraphInputExtras(Rect rect, SerializedProperty root)
        {
            float line = EditorGUIUtility.singleLineHeight;
            var convProp = root.FindPropertyRelative("Conversation");
            if (convProp == null) return;

            bool toolingAvailable = !string.IsNullOrEmpty(WitWeaverConversationInspectorHooks.GraphAssetExtension);
            var data = convProp.objectReferenceValue as WitWeaverConversationData;

            // Row 1: status hint.
            var hintRect = new Rect(rect.x, rect.y, rect.width, line);
            if (!toolingAvailable)
                EditorGUI.LabelField(hintRect, "Graph tooling unavailable (requires Unity 6000.4+).", EditorStyles.miniLabel);
            else if (data != null &&
                     data.AuthoringMode != WitWeaverConversationData.ConversationAuthoringMode.Graph)
                EditorGUI.LabelField(hintRect,
                    "This conversation is not graph-authored — convert it from its inspector.",
                    EditorStyles.miniLabel);
            else if (data == null)
                EditorGUI.LabelField(hintRect, "Assign a graph-authored conversation asset.", EditorStyles.miniLabel);

            // Row 2: open button.
            var buttonRect = new Rect(rect.x, rect.y + line + 2f, rect.width, line);
            using (new EditorGUI.DisabledScope(!toolingAvailable || data == null))
            {
                if (GUI.Button(buttonRect, "Open Graph") && data != null)
                    WitWeaverConversationInspectorHooks.OpenGraphForConversation?.Invoke(data);
            }
        }

        private static int GetTypeIndex(SerializedProperty prop)
        {
            var t = GetManagedType(prop);
            for (int i = 0; i < s_Types.Length; i++) if (t == s_Types[i]) return i;
            return 0;
        }

        private static System.Type GetManagedType(SerializedProperty prop)
        {
            var full = prop.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(full)) return null;
            var parts = full.Split(' ');
            return System.Type.GetType($"{parts[1]}, {parts[0]}");
        }

        private static void SetManagedReferenceType(SerializedProperty prop, System.Type t)
        {
            prop.managedReferenceValue = System.Activator.CreateInstance(t);
        }

        private static void HandleDragAndDrop(Rect dropRect, SerializedProperty root)
        {
            var evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition)) return;

            if (evt.type == EventType.DragUpdated)
            {
                if (CanAcceptDrag())
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                }
            }
            else if (evt.type == EventType.DragPerform)
            {
                if (!CanAcceptDrag()) return;

                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    var draggedPath = AssetDatabase.GetAssetPath(obj);
                    if (WitWeaverConversationInspectorHooks.IsGraphAssetPath(draggedPath))
                    {
                        // Switch to Graph and resolve the bound conversation
                        SetManagedReferenceType(root, typeof(GraphConversationInput));
                        root.serializedObject.ApplyModifiedProperties();
                        root.serializedObject.Update();

                        var convProp = root.FindPropertyRelative("Conversation");
                        if (convProp != null)
                            convProp.objectReferenceValue =
                                WitWeaverConversationInspectorHooks.ResolveGraphConversationByPath?.Invoke(draggedPath);
                        root.serializedObject.ApplyModifiedProperties();
                        Event.current.Use();
                        break;
                    }
                    if (obj is WitWeaverConversationData conversation)
                    {
                        // Graph-authored conversations select the Graph tab; others go to Single.
                        var inputType = conversation.AuthoringMode ==
                                        WitWeaverConversationData.ConversationAuthoringMode.Graph
                            ? typeof(GraphConversationInput)
                            : typeof(SingleConversationInput);
                        SetManagedReferenceType(root, inputType);
                        root.serializedObject.ApplyModifiedProperties();
                        root.serializedObject.Update();

                        var convProp = root.FindPropertyRelative("Conversation");
                        if (convProp != null)
                        {
                            convProp.objectReferenceValue = conversation;
                            root.serializedObject.ApplyModifiedProperties();
                        }
                        Event.current.Use();
                        break;
                    }
                    if (obj is ConversationContainer container)
                    {
                        // Switch to Container and assign Container
                        SetManagedReferenceType(root, typeof(ContainerInput));
                        root.serializedObject.ApplyModifiedProperties();
                        root.serializedObject.Update();

                        var contProp = root.FindPropertyRelative("Container");
                        if (contProp != null)
                        {
                            contProp.objectReferenceValue = container;
                            root.serializedObject.ApplyModifiedProperties();
                        }
                        Event.current.Use();
                        break;
                    }
                }
            }

            static bool CanAcceptDrag()
            {
                foreach (var o in DragAndDrop.objectReferences)
                {
                    if (o is WitWeaverConversationData || o is ConversationContainer)
                        return true;
                    if (WitWeaverConversationInspectorHooks.IsGraphAssetPath(AssetDatabase.GetAssetPath(o)))
                        return true;
                }
                return false;
            }
        }
    }
}
#endif