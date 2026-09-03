// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverSceneCharacterRegistrantEditor.html")]
[CustomEditor(typeof(WitWeaverSceneCharacterRegistrant))]
    public class WitWeaverSceneCharacterRegistrantEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var registrant = (WitWeaverSceneCharacterRegistrant)target;

            // Check whether a registry exists in the scene.
            bool hasRegistry = FindAnyObjectByType<WitWeaverSceneCharacterRegistry>() != null;

            if (!hasRegistry)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(
                    "No WitWeaverSceneCharacterRegistry found in the scene. " +
                    "Characters will not be registered at runtime. Add a registry or assign one directly.",
                    MessageType.Warning);

                if (GUILayout.Button("Add Registry to Scene"))
                {
                    var go = new GameObject("WitWeaverSceneCharacterRegistry");
                    go.AddComponent<WitWeaverSceneCharacterRegistry>();
                    Undo.RegisterCreatedObjectUndo(go, "Add WitWeaverSceneCharacterRegistry");
                    Selection.activeGameObject = go;
                }
            }
        }
    }
}
#endif
