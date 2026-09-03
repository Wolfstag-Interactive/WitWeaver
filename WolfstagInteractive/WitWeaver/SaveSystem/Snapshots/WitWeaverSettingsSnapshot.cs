// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    /// <summary>
    /// Serializable snapshot of the player's settings state — currently the selected language code.
    /// Assembled and restored by <see cref="WitWeaverSaveManager"/> via <see cref="IWitWeaverSaveProvider"/>.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1SaveSystem_1_1WitWeaverSettingsSnapshot.html")]
[Serializable]
    public class WitWeaverSettingsSnapshot
    {
        public string SchemaVersion = "1.0";
        public string SelectedLanguage;
        public long SaveTimestamp;
    }
}