// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    public interface IWitWeaverSaveProvider
    {
        void Save(string saveSlot, WitWeaverGameSnapshot snapshot);
        WitWeaverGameSnapshot Load(string saveSlot);
        bool HasSave(string saveSlot);
        void Delete(string saveSlot);

        void SaveSettings(string key, WitWeaverSettingsSnapshot snapshot);
        WitWeaverSettingsSnapshot LoadSettings(string key);
    }
}