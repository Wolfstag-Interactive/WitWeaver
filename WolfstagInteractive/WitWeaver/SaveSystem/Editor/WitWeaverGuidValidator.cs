#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.SaveSystem.Editor
{
    /// <summary>
    /// Scans all <see cref="WitWeaverConversationData"/> assets in the project whenever
    /// the project changes and logs a warning if any two assets share the same
    /// <see cref="WitWeaverConversationData.ConversationGuid"/>.
    ///
    /// To fix a collision: select the duplicate asset in the Project window,
    /// right-click the component header and choose "Regenerate GUID".
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1SaveSystem_1_1Editor_1_1WitWeaverGuidValidator.html")]
[InitializeOnLoad]
    public static class WitWeaverGuidValidator
    {
        static WitWeaverGuidValidator()
        {
            EditorApplication.projectChanged += Validate;
        }

        private static void Validate()
        {
            var assetGuids = AssetDatabase.FindAssets("t:WitWeaverConversationData");
            if (assetGuids == null || assetGuids.Length == 0) return;

            var seen = new Dictionary<string, string>(); // conversation GUID → asset path

            foreach (var assetGuid in assetGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(assetGuid);
                var data = AssetDatabase.LoadAssetAtPath<WitWeaverConversationData>(path);
                if (data == null) continue;

                var convGuid = data.ConversationGuid;
                if (string.IsNullOrEmpty(convGuid)) continue;

                if (seen.TryGetValue(convGuid, out var existingPath))
                {
                    Debug.LogWarning(
                        $"[WitWeaverGuidValidator] Duplicate ConversationGuid '{convGuid}' " +
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
