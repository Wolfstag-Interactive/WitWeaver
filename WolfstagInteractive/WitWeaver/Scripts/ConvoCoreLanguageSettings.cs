using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreLanguageSettings.html")]
    public class ConvoCoreLanguageSettings : ScriptableObject
    {
        [Tooltip("List of available language codes (e.g., EN, FR, ES).")]
        public List<string> SupportedLanguages;
    }
}