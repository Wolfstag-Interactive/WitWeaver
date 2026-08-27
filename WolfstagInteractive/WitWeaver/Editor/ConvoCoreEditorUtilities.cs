using UnityEditor;
using UnityEngine;
using System.IO;

namespace WolfstagInteractive.ConvoCore.Editor
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreEditorUtilities.html")]
    public static class ConvoCoreEditorUtilities
    {
        [MenuItem("Tools/Wolfstag Interactive/ConvoCore/Create LanguageSettings")]
        public static void CreateLanguageSettingsAsset()
        {
            // Define the Resources folder path relative to the project root.
            string resourcesFolder = Path.Combine("Assets", "Resources");

            // Ensure the Resources folder exists.
            if (!Directory.Exists(resourcesFolder))
            {
                Directory.CreateDirectory(resourcesFolder);
                Debug.Log($"Created folder: {resourcesFolder}");
            }

            // Define the full asset path for LanguageSettings.
            string assetPath = Path.Combine(resourcesFolder, "LanguageSettings.asset");

            // Check if the asset already exists.
            ConvoCoreLanguageSettings existingAsset = AssetDatabase.LoadAssetAtPath<ConvoCoreLanguageSettings>(assetPath);
            if (existingAsset != null)
            {
                Debug.LogWarning($"A LanguageSettings asset already exists at: {assetPath}");
                // Optionally, you could select the asset or prompt the user to overwrite it.
                Selection.activeObject = existingAsset;
                return;
            }

            // Create an instance of the ScriptableObject.
            ConvoCoreLanguageSettings settings = ScriptableObject.CreateInstance<ConvoCoreLanguageSettings>();

            // Create the asset at the defined path.
            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Focus the Project window and select the newly created asset.
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = settings;

            Debug.Log($"LanguageSettings asset was successfully created at: {assetPath}");
        }
        [MenuItem("Tools/Wolfstag Interactive/ConvoCore/Create ConvoCore Conversation GameObject")]
        public static void CreateConvoCoreConversationGameObject()
        {
            // Create a new GameObject with the specified name
            GameObject convoObject = new GameObject("ConvoCore Conversation");
            Debug.Log("Created GameObject: ConvoCore Conversation");
            ConvoCore convoCoreComponent = convoObject.AddComponent<ConvoCore>();
            if (convoCoreComponent != null)
            {
                Debug.Log("ConvoCore component was successfully added.");
            }
            else
            {
                Debug.LogError("Failed to add ConvoCore component. Please ensure the ConvoCore script exists.");
            }
            Selection.activeGameObject = convoObject;
        }
    }
}