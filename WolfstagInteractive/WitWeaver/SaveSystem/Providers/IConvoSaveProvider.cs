namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    public interface IConvoSaveProvider
    {
        void Save(string saveSlot, ConvoCoreGameSnapshot snapshot);
        ConvoCoreGameSnapshot Load(string saveSlot);
        bool HasSave(string saveSlot);
        void Delete(string saveSlot);

        void SaveSettings(string key, ConvoCoreSettingsSnapshot snapshot);
        ConvoCoreSettingsSnapshot LoadSettings(string key);
    }
}