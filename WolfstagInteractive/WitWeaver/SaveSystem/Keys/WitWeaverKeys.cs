// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    internal static class WitWeaverKeys
    {
        public const string DefaultPrefix = "witweaver.";

        private static string Prefix
        {
            get
            {
                var settings = WitWeaverSettings.Instance;
                if (settings != null && !string.IsNullOrEmpty(settings.SaveKeyPrefix))
                    return settings.SaveKeyPrefix;
                return DefaultPrefix;
            }
        }

        public static string Settings => Prefix + "settings";

        public static string GameSlot(string slot) => Prefix + "game." + slot;

        public static string CharacterName(string characterId) => Prefix + "character.name." + characterId;

        public static string Variable(string key) => Prefix + "var." + key;
    }
}