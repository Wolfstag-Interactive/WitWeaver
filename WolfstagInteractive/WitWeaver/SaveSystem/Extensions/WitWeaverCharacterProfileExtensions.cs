// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    public static class WitWeaverCharacterProfileExtensions
    {
        private static string RuntimeNameKey(WitWeaverCharacterProfileBaseData profile)
        {
            return WitWeaverKeys.CharacterName(profile.CharacterID);
        }

        public static string GetDisplayName(this WitWeaverCharacterProfileBaseData profile, WitWeaverVariableStore store)
        {
            if (profile == null) return string.Empty;

            if (store != null)
            {
                var key = RuntimeNameKey(profile);
                if (store.TryGetString(key, out var customName) && !string.IsNullOrEmpty(customName))
                    return customName;
            }

            return profile.CharacterName;
        }

        public static void SetDisplayName(this WitWeaverCharacterProfileBaseData profile, string name, WitWeaverVariableStore store)
        {
            if (profile == null || store == null) return;

            var key = RuntimeNameKey(profile);
            store.SetString(key, name, WitWeaverVariableScope.Global);
        }

        public static void ClearDisplayName(this WitWeaverCharacterProfileBaseData profile, WitWeaverVariableStore store)
        {
            if (profile == null || store == null) return;

            var key = RuntimeNameKey(profile);
            store.SetString(key, string.Empty, WitWeaverVariableScope.Global);
        }
    }
}