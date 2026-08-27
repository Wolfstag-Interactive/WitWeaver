using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WolfstagInteractive.WitWeaver.Editor;

namespace WolfstagInteractive.WitWeaver.GraphEditor
{
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1GraphEditor_1_1WitWeaverGraphMenuItems.html")]
    internal static class WitWeaverGraphMenuItems
    {
        private const string OpenGraphMenu = "Assets/WitWeaver/Open Conversation Graph";
        private const string BakeGraphMenu = "Assets/WitWeaver/Bake Conversation Graph";
        private const string CreateWithGraphMenu = "Assets/Create/WitWeaver/Conversation With Graph";

        /// <summary>
        /// One-step setup for graph-first authoring: creates a starter YAML file and a linked
        /// conversation asset in the selected folder, then generates and opens its graph.
        /// </summary>
        [MenuItem(CreateWithGraphMenu, priority = 11)]
        private static void CreateConversationWithGraph()
        {
            var folder = GetTargetFolder();
            var yamlPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/NewConversation.yml");
            var key = Path.GetFileNameWithoutExtension(yamlPath);

            // Starter YAML: one line, LineID pre-assigned so no writeback churn on first import.
            var languages = WitWeaverGraphSchema.GetLanguages();
            var localized = new Dictionary<string, string>();
            foreach (var language in languages)
                localized[language] = "New line";
            var dict = new Dictionary<string, List<DialogueYamlConfig>>
            {
                [key] = new()
                {
                    new DialogueYamlConfig
                    {
                        CharacterID = "Speaker",
                        LineID = WitWeaverLineID.NewLineID(),
                        LocalizedDialogue = localized
                    }
                }
            };
            File.WriteAllText(Path.GetFullPath(yamlPath), WitWeaverYamlSerializer.Serialize(dict));
            AssetDatabase.ImportAsset(yamlPath);

            var dataPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{key}.asset");
            var data = ScriptableObject.CreateInstance<WitWeaverConversationData>();
            data.ConversationKey = key;
            data.ConversationTitle = key;
            AssetDatabase.CreateAsset(data, dataPath);

            data.SourceYaml = AssetDatabase.LoadAssetAtPath<Object>(yamlPath);
            data.SourceYamlAssetPath = yamlPath;
            WitWeaverYamlWatcher.TryEmbedFromPath(data, yamlPath);
            data.WitWeaverYamlUtilities.ImportFromYamlForKey(key);
            data.ValidateAndFixDialogueLines();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
            // OpenGraphFor creates the companion graph and switches the asset to graph authoring.
            WitWeaverGraphBridge.OpenGraphFor(data);
        }

        private static string GetTargetFolder()
        {
            var obj = Selection.activeObject;
            var path = obj != null ? AssetDatabase.GetAssetPath(obj) : null;
            if (string.IsNullOrEmpty(path)) return "Assets";
            if (AssetDatabase.IsValidFolder(path)) return path;
            var dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
            return string.IsNullOrEmpty(dir) ? "Assets" : dir;
        }

        [MenuItem(OpenGraphMenu, isValidateFunction: true)]
        private static bool ValidateOpenGraph()
            => Selection.activeObject is WitWeaverConversationData data &&
               data.AuthoringMode == WitWeaverConversationData.ConversationAuthoringMode.Graph;

        [MenuItem(OpenGraphMenu)]
        private static void OpenGraph()
        {
            if (Selection.activeObject is WitWeaverConversationData data)
                WitWeaverGraphBridge.OpenGraphFor(data);
        }

        [MenuItem(BakeGraphMenu, isValidateFunction: true)]
        private static bool ValidateBakeGraph()
            => FindConversationForSelection() != null;

        [MenuItem(BakeGraphMenu)]
        private static void BakeGraph()
        {
            var data = FindConversationForSelection();
            if (data != null)
                WitWeaverGraphBridge.BakeGraphFor(data, interactive: true);
        }

        private const string RefreshGraphMenu = "Assets/WitWeaver/Refresh Graph From YAML";

        [MenuItem(RefreshGraphMenu, isValidateFunction: true)]
        private static bool ValidateRefreshGraph()
            => FindConversationForSelection() != null;

        [MenuItem(RefreshGraphMenu)]
        private static void RefreshGraph()
        {
            var data = FindConversationForSelection();
            if (data != null)
                WitWeaverGraphBridge.RefreshGraphFromYamlFor(data);
        }

        /// <summary>Accepts either a selected conversation asset or a selected graph asset.</summary>
        private static WitWeaverConversationData FindConversationForSelection()
        {
            if (Selection.activeObject is WitWeaverConversationData data)
                return data;

            var path = Selection.activeObject != null
                ? AssetDatabase.GetAssetPath(Selection.activeObject)
                : null;
            if (!string.IsNullOrEmpty(path) &&
                path.EndsWith("." + WitWeaverConversationGraph.AssetExtension, System.StringComparison.OrdinalIgnoreCase))
                return Unity.GraphToolkit.Editor.GraphDatabase
                    .LoadGraph<WitWeaverConversationGraph>(path)?.Conversation;

            return null;
        }
    }
}
