using WolfstagInteractive.WitWeaver;

namespace WolfstagInteractive.WitWeaver.Editor
{
    internal enum BulkImportEntryStatus
    {
        New,
        Update,
        Conflict,
        Error,
        Skipped
    }

    internal sealed class BulkImportManifestEntry
    {
        public string ConversationKey;
        public string YamlAssetPath;
        public int LineCount;
        public BulkImportEntryStatus Status;
        public string StatusDetail;
        public bool Selected;
        public WitWeaverConversationData ExistingAsset;
    }
}
