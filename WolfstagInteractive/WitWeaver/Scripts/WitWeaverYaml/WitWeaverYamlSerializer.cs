using System.Collections.Generic;
using UnityEngine;
using YamlDotNet.Serialization;

namespace WolfstagInteractive.WitWeaver
{
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverYamlSerializer.html")]
    public static class WitWeaverYamlSerializer
    {
        private static readonly ISerializer _serializer = new SerializerBuilder().Build();

        public static string Serialize(Dictionary<string, List<DialogueYamlConfig>> dict)
        {
            dict ??= new Dictionary<string, List<DialogueYamlConfig>>();
            return _serializer.Serialize(dict);
        }
    }
}