// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{


    [HelpURL(
        "https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverYamlAssetCreator.html")]
    /// <summary>
    /// Creates a new .yml file in the Project window from the Assets/Create menu,
    /// similar to how Unity creates a new C# script.
    /// </summary>
    public static class WitWeaverYamlAssetCreator
    {
        private const string MenuPath = "Assets/Create/WitWeaver/Conversation YAML";
        private const string DefaultFileName = "NewConversation.yml";

        private const string DefaultYamlTemplate =
            @"# WitWeaver Conversation YAML Dialogue Line Format
# ConversationID: The top-level key identifying this dialogue sequence. 
There can be multiple conversations within a single file but they must be uniquely identifiable keys.
# CharacterID: The ID of the speaker. Character profiles in the editor will use this to decide who is speaking the line.
# LineID: Unique identifier for the line (Leave this field alone as tooling will normaly auto-generate one if blank).
# LocalizedDialogue: a parent identifier for all the dialogue line localizations.

ConversationExample:
- CharacterID: John
  LineID: 
  LocalizedDialogue:
    en: ""I am speaking!""
    fr: ""Je parle !""
    es: ""¡Estoy hablando!""
- CharacterID: John
  LineID: 
  LocalizedDialogue:
    en: ""I AM SPEAKING LOUDER IN CASE YOU COULD NOT HEAR ME""
    fr: ""JE PARLE PLUS FORT AU CAS OÙ TU NE M'ENTENDRAIS PAS""
    es: ""ESTOY HABLANDO MÁS FUERTE POR SI NO PODÍAS OÍRME""
- CharacterID: Alex
  LineID: 
  LocalizedDialogue:
    en: ""WHY ARE WE YELLING??!""
    fr: ""POURQUOI EST-CE QU'ON CRIE ??!""
    es: ""¡¿POR QUÉ ESTAMOS GRITANDO??!""
";

        [MenuItem(MenuPath, priority = 10)]
        public static void CreateYamlFile()
        {
            var icon = EditorGUIUtility.IconContent("TextAsset Icon").image as Texture2D;

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                EntityId.None,
                ScriptableObject.CreateInstance<CreateYamlAssetCreationEndAction>(),
                DefaultFileName,
                icon,
                DefaultYamlTemplate
            );
        }

        private sealed class CreateYamlAssetCreationEndAction : AssetCreationEndAction
        {
            public override void Action(EntityId entityId, string pathName, string resourceFile)
            {
                try
                {
                    var fullPath = Path.GetFullPath(pathName);

                    var dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    File.WriteAllText(fullPath, resourceFile);

                    AssetDatabase.ImportAsset(pathName);
                    var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(pathName);

                    if (asset != null)
                    {
                        ProjectWindowUtil.ShowCreatedAsset(asset);
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to create YAML file at '{pathName}'.\n{ex}");
                }
            }
        }
    }
}
#endif