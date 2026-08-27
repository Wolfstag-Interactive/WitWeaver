using UnityEditor;
using UnityEngine;
using System.IO;

namespace WolfstagInteractive.WitWeaver.Editor
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverEditorUtilities.html")]
    public static class WitWeaverEditorUtilities
    {
        [MenuItem("Tools/Wolfstag Interactive/WitWeaver/Create LanguageSettings")]
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
            WitWeaverLanguageSettings existingAsset = AssetDatabase.LoadAssetAtPath<WitWeaverLanguageSettings>(assetPath);
            if (existingAsset != null)
            {
                Debug.LogWarning($"A LanguageSettings asset already exists at: {assetPath}");
                // Optionally, you could select the asset or prompt the user to overwrite it.
                Selection.activeObject = existingAsset;
                return;
            }

            // Create an instance of the ScriptableObject.
            WitWeaverLanguageSettings settings = ScriptableObject.CreateInstance<WitWeaverLanguageSettings>();

            // Create the asset at the defined path.
            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Focus the Project window and select the newly created asset.
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = settings;

            Debug.Log($"LanguageSettings asset was successfully created at: {assetPath}");
        }
        [MenuItem("Tools/Wolfstag Interactive/WitWeaver/Create WitWeaver Conversation GameObject")]
        public static void CreateWitWeaverConversationGameObject()
        {
            // Create a new GameObject with the specified name
            GameObject convoObject = new GameObject("WitWeaver Conversation");
            Debug.Log("Created GameObject: WitWeaver Conversation");
            WitWeaver witWeaverComponent = convoObject.AddComponent<WitWeaver>();
            if (witWeaverComponent != null)
            {
                Debug.Log("WitWeaver component was successfully added.");
            }
            else
            {
                Debug.LogError("Failed to add WitWeaver component. Please ensure the WitWeaver script exists.");
            }
            Selection.activeGameObject = convoObject;
        }
    }
}