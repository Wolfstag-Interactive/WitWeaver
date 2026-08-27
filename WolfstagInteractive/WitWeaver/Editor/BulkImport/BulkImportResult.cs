namespace WolfstagInteractive.ConvoCore.Editor
{
    internal enum BulkImportOutcome
    {
        Created,
        Updated,
        Failed
    }

    internal sealed class BulkImportResult
    {
        public string ConversationKey;
        public string YamlAssetPath;
        public BulkImportOutcome Outcome;
        public string OutputAssetPath;
        public string ErrorMessage;
    }
}
