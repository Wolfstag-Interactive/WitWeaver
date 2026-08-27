using System;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    /// <summary>
    /// Serializable snapshot of the player's settings state — currently the selected language code.
    /// Assembled and restored by <see cref="ConvoCoreSaveManager"/> via <see cref="IConvoSaveProvider"/>.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SaveSystem_1_1ConvoCoreSettingsSnapshot.html")]
[Serializable]
    public class ConvoCoreSettingsSnapshot
    {
        public string SchemaVersion = "1.0";
        public string SelectedLanguage;
        public long SaveTimestamp;
    }
}