namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    public static class ConvoCoreCharacterProfileExtensions
    {
        private static string RuntimeNameKey(ConvoCoreCharacterProfileBaseData profile)
        {
            return ConvoCoreKeys.CharacterName(profile.CharacterID);
        }

        public static string GetDisplayName(this ConvoCoreCharacterProfileBaseData profile, ConvoVariableStore store)
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

        public static void SetDisplayName(this ConvoCoreCharacterProfileBaseData profile, string name, ConvoVariableStore store)
        {
            if (profile == null || store == null) return;

            var key = RuntimeNameKey(profile);
            store.SetString(key, name, ConvoVariableScope.Global);
        }

        public static void ClearDisplayName(this ConvoCoreCharacterProfileBaseData profile, ConvoVariableStore store)
        {
            if (profile == null || store == null) return;

            var key = RuntimeNameKey(profile);
            store.SetString(key, string.Empty, ConvoVariableScope.Global);
        }
    }
}