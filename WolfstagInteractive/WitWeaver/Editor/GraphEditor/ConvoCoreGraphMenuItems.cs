using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WolfstagInteractive.ConvoCore.Editor;

namespace WolfstagInteractive.ConvoCore.GraphEditor
{
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1GraphEditor_1_1ConvoCoreGraphMenuItems.html")]
    internal static class ConvoCoreGraphMenuItems
    {
        private const string OpenGraphMenu = "Assets/ConvoCore/Open Conversation Graph";
        private const string BakeGraphMenu = "Assets/ConvoCore/Bake Conversation Graph";
        private const string CreateWithGraphMenu = "Assets/Create/ConvoCore/Conversation With Graph";

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
            var languages = ConvoCoreGraphSchema.GetLanguages();
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
                        LineID = ConvoCoreLineID.NewLineID(),
                        LocalizedDialogue = localized
                    }
                }
            };
            File.WriteAllText(Path.GetFullPath(yamlPath), ConvoCoreYamlSerializer.Serialize(dict));
            AssetDatabase.ImportAsset(yamlPath);

            var dataPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{key}.asset");
            var data = ScriptableObject.CreateInstance<ConvoCoreConversationData>();
            data.ConversationKey = key;
            data.ConversationTitle = key;
            AssetDatabase.CreateAsset(data, dataPath);

            data.SourceYaml = AssetDatabase.LoadAssetAtPath<Object>(yamlPath);
            data.SourceYamlAssetPath = yamlPath;
            ConvoCoreYamlWatcher.TryEmbedFromPath(data, yamlPath);
            data.ConvoCoreYamlUtilities.ImportFromYamlForKey(key);
            data.ValidateAndFixDialogueLines();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
            // OpenGraphFor creates the companion graph and switches the asset to graph authoring.
            ConvoCoreGraphBridge.OpenGraphFor(data);
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
            => Selection.activeObject is ConvoCoreConversationData data &&
               data.AuthoringMode == ConvoCoreConversationData.ConversationAuthoringMode.Graph;

        [MenuItem(OpenGraphMenu)]
        private static void OpenGraph()
        {
            if (Selection.activeObject is ConvoCoreConversationData data)
                ConvoCoreGraphBridge.OpenGraphFor(data);
        }

        [MenuItem(BakeGraphMenu, isValidateFunction: true)]
        private static bool ValidateBakeGraph()
            => FindConversationForSelection() != null;

        [MenuItem(BakeGraphMenu)]
        private static void BakeGraph()
        {
            var data = FindConversationForSelection();
            if (data != null)
                ConvoCoreGraphBridge.BakeGraphFor(data, interactive: true);
        }

        private const string RefreshGraphMenu = "Assets/ConvoCore/Refresh Graph From YAML";

        [MenuItem(RefreshGraphMenu, isValidateFunction: true)]
        private static bool ValidateRefreshGraph()
            => FindConversationForSelection() != null;

        [MenuItem(RefreshGraphMenu)]
        private static void RefreshGraph()
        {
            var data = FindConversationForSelection();
            if (data != null)
                ConvoCoreGraphBridge.RefreshGraphFromYamlFor(data);
        }

        /// <summary>Accepts either a selected conversation asset or a selected .convograph asset.</summary>
        private static ConvoCoreConversationData FindConversationForSelection()
        {
            if (Selection.activeObject is ConvoCoreConversationData data)
                return data;

            var path = Selection.activeObject != null
                ? AssetDatabase.GetAssetPath(Selection.activeObject)
                : null;
            if (!string.IsNullOrEmpty(path) &&
                path.EndsWith("." + ConvoCoreConversationGraph.AssetExtension, System.StringComparison.OrdinalIgnoreCase))
                return Unity.GraphToolkit.Editor.GraphDatabase
                    .LoadGraph<ConvoCoreConversationGraph>(path)?.Conversation;

            return null;
        }
    }
}
