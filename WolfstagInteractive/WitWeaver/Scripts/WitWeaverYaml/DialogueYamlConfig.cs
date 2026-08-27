using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace WolfstagInteractive.WitWeaver
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1DialogueYamlConfig.html")]
    public class DialogueYamlConfig 
    {
        [YamlMember(Alias = "CharacterID",Order = 1)]
        public string CharacterID { get; set; } 
        [YamlMember(Alias = "LineID",Order = 2)]
        public string LineID { get; set; }
        [YamlMember(Alias = "LocalizedDialogue",Order = 3)]
        public Dictionary<string, string> LocalizedDialogue { get; set; } = new Dictionary<string, string>();
    }
}