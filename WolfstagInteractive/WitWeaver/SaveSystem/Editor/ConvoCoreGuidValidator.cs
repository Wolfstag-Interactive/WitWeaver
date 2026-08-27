#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.SaveSystem.Editor
{
    /// <summary>
    /// Scans all <see cref="ConvoCoreConversationData"/> assets in the project whenever
    /// the project changes and logs a warning if any two assets share the same
    /// <see cref="ConvoCoreConversationData.ConversationGuid"/>.
    ///
    /// To fix a collision: select the duplicate asset in the Project window,
    /// right-click the component header and choose "Regenerate GUID".
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SaveSystem_1_1Editor_1_1ConvoCoreGuidValidator.html")]
[InitializeOnLoad]
    public static class ConvoCoreGuidValidator
    {
        static ConvoCoreGuidValidator()
        {
            EditorApplication.projectChanged += Validate;
        }

        private static void Validate()
        {
            var assetGuids = AssetDatabase.FindAssets("t:ConvoCoreConversationData");
            if (assetGuids == null || assetGuids.Length == 0) return;

            var seen = new Dictionary<string, string>(); // conversation GUID → asset path

            foreach (var assetGuid in assetGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(assetGuid);
                var data = AssetDatabase.LoadAssetAtPath<ConvoCoreConversationData>(path);
                if (data == null) continue;

                var convGuid = data.ConversationGuid;
                if (string.IsNullOrEmpty(convGuid)) continue;

                if (seen.TryGetValue(convGuid, out var existingPath))
                {
                    Debug.LogWarning(
                        $"[ConvoCoreGuidValidator] Duplicate ConversationGuid '{convGuid}' " +
                        $"detected in:\n  '{path}'\n  '{existingPath}'\n" +
                        "Select the asset and use 'Regenerate GUID' to assign a unique identifier.",
                        data);
                }
                else
                {
                    seen[convGuid] = path;
                }
            }
        }
    }
}
#endif
