namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    internal static class ConvoCoreKeys
    {
        public const string DefaultPrefix = "convocore.";

        private static string Prefix
        {
            get
            {
                var settings = ConvoCoreSettings.Instance;
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