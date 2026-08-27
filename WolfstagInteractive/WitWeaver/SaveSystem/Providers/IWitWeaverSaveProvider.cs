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