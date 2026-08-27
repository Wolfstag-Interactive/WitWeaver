using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{


[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreLanguageSettingsLoader.html")]
    public class ConvoCoreLanguageSettingsLoader : IConvoCoreLanguageSettingsLoader
    {
        public ConvoCoreLanguageSettings LoadLanguageSettings()
        {
            // Use the path matching your asset location in the Resources folder.
            return Resources.Load<ConvoCoreLanguageSettings>("LanguageSettings");
        }

    }

    public interface IConvoCoreLanguageSettingsLoader
    {
        ConvoCoreLanguageSettings LoadLanguageSettings();
    }
}