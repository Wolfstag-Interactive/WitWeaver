using UnityEngine;

namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SaveSystem_1_1ConvoCoreSaveManagerBootstrapper.html")]
[DefaultExecutionOrder(-100)]
    public class ConvoCoreSaveManagerBootstrapper : MonoBehaviour
    {
        [Header("References")]
        public ConvoCoreSaveManager SaveManager;

        [Header("Initialization")]
        public bool InitializeOnAwake = true;
        public bool LoadSettingsOnAwake = true;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (SaveManager == null)
            {
                Debug.LogWarning("[ConvoCoreSaveManagerBootstrapper] SaveManager is not assigned.");
                return;
            }

            if (InitializeOnAwake)
                SaveManager.Initialize();

            if (LoadSettingsOnAwake)
            {
                if (!SaveManager.IsInitialized)
                    Debug.LogWarning("[ConvoCoreSaveManagerBootstrapper] LoadSettingsOnAwake is true but SaveManager is not initialized. Settings will not load.");
                else
                    SaveManager.LoadSettings();
            }
        }
    }
}