// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1PostCompileActionAssetCreator.html")]
[InitializeOnLoad]
    public static class PostCompileActionAssetCreator 
    {
        static PostCompileActionAssetCreator()
        {
            EditorApplication.update += TryCreatePendingAsset;
        }

        private static void TryCreatePendingAsset()
        {
            if (!EditorPrefs.HasKey("WitWeaver_PendingActionName"))
                return;

            string actionName = EditorPrefs.GetString("WitWeaver_PendingActionName");
            string assetPath = EditorPrefs.GetString("WitWeaver_PendingAssetPath", "Assets/DialogueActions");
            string fullPath = $"{assetPath}/{actionName}.asset";

            Type type = GetTypeByName(actionName);
            if (type == null)
            {
                //Debug.LogWarning($"Type '{actionName}' not found. Retrying in next update.");
                // Wait for next update frame.
                return;
            }

            ScriptableObject asset = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created ScriptableObject asset: {fullPath}");

            EditorPrefs.DeleteKey("WitWeaver_PendingActionName");
            EditorPrefs.DeleteKey("WitWeaver_PendingAssetPath");

            EditorApplication.update -= TryCreatePendingAsset;
        }

        private static Type GetTypeByName(string typeName)
        {
            // First try the fully qualified name lookup
            Type type = Type.GetType(typeName);
            if (type != null)
                return type;

            // If not found, iterate through all loaded assemblies
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                type = assembly.GetType(typeName);
                if (type != null)
                    return type;
            }
            return null;
        }


    }
}