#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.Editor
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreMenuItems.html")]
    public static class ConvoCoreMenuItems
    {
        [MenuItem("Tools/Wolfstag Interactive/ConvoCore/Open Settings", false, 1)]
        public static void OpenSettings()
        {
            ConvoCoreSettings settings = FindOrCreateSettings();
            
            if (settings != null)
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
        }

        [MenuItem("Tools/Wolfstag Interactive/ConvoCore/Create Settings (if missing)", false, 2)]
        public static void CreateSettingsIfMissing()
        {
            var existing = FindSettings();
            if (existing != null)
            {
                Debug.Log($"ConvoCoreSettings already exists at: {AssetDatabase.GetAssetPath(existing)}");
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            CreateNewSettings();
        }

        private static ConvoCoreSettings FindSettings()
        {
            // First check if already assigned to loader
            if (ConvoCoreYamlLoader.Settings != null)
                return ConvoCoreYamlLoader.Settings;

            // Try Resources folder
            var resourceSettings = Resources.Load<ConvoCoreSettings>("ConvoCoreSettings");
            if (resourceSettings != null)
                return resourceSettings;

            // Search entire project
            var guids = AssetDatabase.FindAssets("t:ConvoCoreSettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<ConvoCoreSettings>(path);
            }

            return null;
        }

        private static ConvoCoreSettings FindOrCreateSettings()
        {
            var existing = FindSettings();
            if (existing != null)
            {
                // Auto-assign to loader if not already
                if (ConvoCoreYamlLoader.Settings == null)
                {
                    ConvoCoreYamlLoader.Settings = existing;
                }
                return existing;
            }

            return CreateNewSettings();
        }

        private static ConvoCoreSettings CreateNewSettings()
        {
            var settings = ScriptableObject.CreateInstance<ConvoCoreSettings>();
            
            // Ensure Resources folder exists
            string resourcesPath = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesPath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            string assetPath = "Assets/Resources/ConvoCoreSettings.asset";
            
            // If file exists at this path, find alternative name
            if (AssetDatabase.LoadAssetAtPath<ConvoCoreSettings>(assetPath) != null)
            {
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            }

            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created ConvoCoreSettings at: {assetPath}");

            // Auto-assign to loader
            ConvoCoreYamlLoader.Settings = settings;

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);

            return settings;
        }
    }
}
#endif