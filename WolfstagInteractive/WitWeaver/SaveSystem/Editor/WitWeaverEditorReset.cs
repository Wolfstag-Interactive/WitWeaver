#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.SaveSystem.Editor
{
    [InitializeOnLoad]
    public static class ConvoCoreEditorReset
    {
        static ConvoCoreEditorReset()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                ResetStaticState();
            }
        }

        private static void ResetStaticState()
        {
            // Reset ConvoCoreLanguageManager singleton instance
            var langManagerType = typeof(ConvoCoreLanguageManager);
            var instanceField = langManagerType.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (instanceField != null)
                instanceField.SetValue(null, null);
        }
    }
}
#endif