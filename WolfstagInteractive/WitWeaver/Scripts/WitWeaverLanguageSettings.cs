// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverLanguageSettings.html")]
    public class WitWeaverLanguageSettings : ScriptableObject
    {
        [Tooltip("List of available language codes (e.g., EN, FR, ES).")]
        public List<string> SupportedLanguages;
    }
}