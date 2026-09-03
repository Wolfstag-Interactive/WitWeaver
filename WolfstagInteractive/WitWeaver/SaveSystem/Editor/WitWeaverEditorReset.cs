// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.SaveSystem.Editor
{
    [InitializeOnLoad]
    public static class WitWeaverEditorReset
    {
        static WitWeaverEditorReset()
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
            // Reset WitWeaverLanguageManager singleton instance
            var langManagerType = typeof(WitWeaverLanguageManager);
            var instanceField = langManagerType.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (instanceField != null)
                instanceField.SetValue(null, null);
        }
    }
}
#endif