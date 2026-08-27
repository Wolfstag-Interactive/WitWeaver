namespace WolfstagInteractive.ConvoCore
{
    public interface IDialogueHistoryOutput
    {
        void Clear();
        void Append(string line);
        void RefreshView(); // optional: e.g., scroll update
    }

    public interface IDialogueHistoryOutputPrefab : IDialogueHistoryOutput
    {
        void SpawnEntry(DialogueHistoryEntry entry);
    }
}