using System.Collections.Generic;
using UnityEngine;
using YamlDotNet.Serialization;

namespace WolfstagInteractive.ConvoCore
{
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreYamlSerializer.html")]
    public static class ConvoCoreYamlSerializer
    {
        private static readonly ISerializer _serializer = new SerializerBuilder().Build();

        public static string Serialize(Dictionary<string, List<DialogueYamlConfig>> dict)
        {
            dict ??= new Dictionary<string, List<DialogueYamlConfig>>();
            return _serializer.Serialize(dict);
        }
    }
}