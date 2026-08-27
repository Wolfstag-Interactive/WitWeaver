using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.Editor
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1PostCompileActionAssetCreator.html")]
[InitializeOnLoad]
    public static class PostCompileActionAssetCreator 
    {
        static PostCompileActionAssetCreator()
        {
            EditorApplication.update += TryCreatePendingAsset;
        }

        private static void TryCreatePendingAsset()
        {
            if (!EditorPrefs.HasKey("ConvoCore_PendingActionName"))
                return;

            string actionName = EditorPrefs.GetString("ConvoCore_PendingActionName");
            string assetPath = EditorPrefs.GetString("ConvoCore_PendingAssetPath", "Assets/DialogueActions");
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

            EditorPrefs.DeleteKey("ConvoCore_PendingActionName");
            EditorPrefs.DeleteKey("ConvoCore_PendingAssetPath");

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