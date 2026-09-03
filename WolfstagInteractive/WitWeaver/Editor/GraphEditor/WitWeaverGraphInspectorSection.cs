// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEditor;
using UnityEngine;
using WolfstagInteractive.WitWeaver.Editor;

namespace WolfstagInteractive.WitWeaver.GraphEditor
{
    /// <summary>
    /// Registers the "Conversation Graph" section into the conversation inspector via
    /// <see cref="WitWeaverConversationInspectorHooks"/>.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1GraphEditor_1_1WitWeaverGraphInspectorSection.html")]
[InitializeOnLoad]
    internal static class WitWeaverGraphInspectorSection
    {
        static WitWeaverGraphInspectorSection()
        {
            WitWeaverConversationInspectorHooks.DrawExtraSection += Draw;

            // Advertise graph tooling to the core editor assembly (Graph input tab, drag-and-drop).
            WitWeaverConversationInspectorHooks.GraphAssetExtension = WitWeaverConversationGraph.AssetExtension;
            WitWeaverConversationInspectorHooks.ResolveGraphConversationByPath = path =>
                Unity.GraphToolkit.Editor.GraphDatabase
                    .LoadGraph<WitWeaverConversationGraph>(path)?.Conversation;
            WitWeaverConversationInspectorHooks.OpenGraphForConversation = WitWeaverGraphBridge.OpenGraphFor;
        }

        private static void Draw(WitWeaverConversationData data)
        {
            if (data == null) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Conversation Graph (Experimental)", EditorStyles.boldLabel);

            if (data.AuthoringMode != WitWeaverConversationData.ConversationAuthoringMode.Graph)
            {
                EditorGUILayout.HelpBox(
                    "Author this conversation visually as a node graph: lines, choices, jumps and " +
                    "branches. Converting makes the graph the sole editing surface for lines — " +
                    "the list below is replaced by the graph until you revert.",
                    MessageType.Info);

                if (GUILayout.Button("Convert to Graph Authoring", GUILayout.Height(24)) &&
                    EditorUtility.DisplayDialog(
                        "Convert to Graph Authoring",
                        $"Author '{data.name}' as a node graph?\n\n" +
                        "A companion graph is generated from the current lines and managed " +
                        "automatically with this asset (renamed, moved and deleted together). " +
                        "Line editing moves into the graph; everything else (participants, audio, " +
                        "YAML linking) stays in this inspector. You can revert at any time.",
                        "Convert", "Cancel"))
                {
                    WitWeaverGraphBridge.OpenGraphFor(data);
                }
            }
            else
            {
                if (WitWeaverGraphBridge.IsGraphStaleRelativeToYaml(data))
                {
                    EditorGUILayout.HelpBox(
                        "The YAML changed after the graph was last synced — baking is blocked until you " +
                        "Refresh Graph From YAML.",
                        MessageType.Warning);
                }

                if (WitWeaverGraphBridge.IsGraphDirtyRelativeToConversation(data))
                {
                    EditorGUILayout.HelpBox(
                        "The graph has unbaked changes. Bake to apply them to this conversation (and its YAML).",
                        MessageType.Warning);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Open Graph", GUILayout.Height(24)))
                    WitWeaverGraphBridge.OpenGraphFor(data);
                if (GUILayout.Button("Bake Graph → Conversation", GUILayout.Height(24)))
                    WitWeaverGraphBridge.BakeGraphFor(data, interactive: true);
                EditorGUILayout.EndHorizontal();

                // Always available: pulls YAML edits into the graph and restores deleted line
                // nodes (the fix the missing-node bake error points users at).
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh Graph From YAML", GUILayout.Height(22)))
                    WitWeaverGraphBridge.RefreshGraphFromYamlFor(data);
                if (GUILayout.Button(new GUIContent("Rebuild Graph",
                        "Regenerate every node and wire from the last-baked flow and current YAML. " +
                        "Repairs fragmented graphs; node positions reset."), GUILayout.Height(22)))
                    WitWeaverGraphBridge.RebuildGraphFor(data, interactive: true);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Revert to Linear Authoring", GUILayout.Height(18)))
                {
                    int choice = EditorUtility.DisplayDialogComplex(
                        "Revert to Linear Authoring",
                        $"Stop authoring '{data.name}' as a graph?\n\n" +
                        "The baked lines stay intact and become editable in the inspector again. " +
                        "You can keep the graph asset (to convert back later) or delete it.",
                        "Revert, Keep Graph", "Cancel", "Revert, Delete Graph");
                    if (choice == 0)
                        WitWeaverGraphBridge.ConvertToLinear(data, deleteGraphAsset: false);
                    else if (choice == 2)
                        WitWeaverGraphBridge.ConvertToLinear(data, deleteGraphAsset: true);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
    }
}
