#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverMenuItems.html")]
    public static class WitWeaverMenuItems
    {
        [MenuItem("Tools/Wolfstag Interactive/WitWeaver/Open Settings", false, 1)]
        public static void OpenSettings()
        {
            WitWeaverSettings settings = FindOrCreateSettings();
            
            if (settings != null)
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
        }

        [MenuItem("Tools/Wolfstag Interactive/WitWeaver/Create Settings (if missing)", false, 2)]
        public static void CreateSettingsIfMissing()
        {
            var existing = FindSettings();
            if (existing != null)
            {
                Debug.Log($"WitWeaverSettings already exists at: {AssetDatabase.GetAssetPath(existing)}");
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            CreateNewSettings();
        }

        private static WitWeaverSettings FindSettings()
        {
            // Try Resources folder
            var resourceSettings = Resources.Load<WitWeaverSettings>("WitWeaverSettings");
            if (resourceSettings != null)
                return resourceSettings;

            // Search entire project
            var guids = AssetDatabase.FindAssets("t:WitWeaverSettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<WitWeaverSettings>(path);
            }

            return null;
        }

        private static WitWeaverSettings FindOrCreateSettings()
        {
            var existing = FindSettings();
            if (existing != null)
                return existing;

            return CreateNewSettings();
        }

        private static WitWeaverSettings CreateNewSettings()
        {
            var settings = ScriptableObject.CreateInstance<WitWeaverSettings>();
            
            // Ensure Resources folder exists
            string resourcesPath = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesPath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            string assetPath = "Assets/Resources/WitWeaverSettings.asset";
            
            // If file exists at this path, find alternative name
            if (AssetDatabase.LoadAssetAtPath<WitWeaverSettings>(assetPath) != null)
            {
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            }

            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created WitWeaverSettings at: {assetPath}");

            // Auto-assign to loader
            WitWeaverYamlLoader.Settings = settings;

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);

            return settings;
        }
    }
}
#endif