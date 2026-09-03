// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{


[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverLanguageSettingsLoader.html")]
    public class WitWeaverLanguageSettingsLoader : IWitWeaverLanguageSettingsLoader
    {
        public WitWeaverLanguageSettings LoadLanguageSettings()
        {
            // Use the path matching your asset location in the Resources folder.
            return Resources.Load<WitWeaverLanguageSettings>("LanguageSettings");
        }

    }

    public interface IWitWeaverLanguageSettingsLoader
    {
        WitWeaverLanguageSettings LoadLanguageSettings();
    }
}