// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

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