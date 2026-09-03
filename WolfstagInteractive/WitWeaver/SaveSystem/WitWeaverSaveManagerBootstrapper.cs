// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1SaveSystem_1_1WitWeaverSaveManagerBootstrapper.html")]
[DefaultExecutionOrder(-100)]
    public class WitWeaverSaveManagerBootstrapper : MonoBehaviour
    {
        [Header("References")]
        public WitWeaverSaveManager SaveManager;

        [Header("Initialization")]
        public bool InitializeOnAwake = true;
        public bool LoadSettingsOnAwake = true;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (SaveManager == null)
            {
                Debug.LogWarning("[WitWeaverSaveManagerBootstrapper] SaveManager is not assigned.");
                return;
            }

            if (InitializeOnAwake)
                SaveManager.Initialize();

            if (LoadSettingsOnAwake)
            {
                if (!SaveManager.IsInitialized)
                    Debug.LogWarning("[WitWeaverSaveManagerBootstrapper] LoadSettingsOnAwake is true but SaveManager is not initialized. Settings will not load.");
                else
                    SaveManager.LoadSettings();
            }
        }
    }
}