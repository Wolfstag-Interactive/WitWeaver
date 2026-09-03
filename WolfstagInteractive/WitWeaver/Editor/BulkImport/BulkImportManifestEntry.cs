// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

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
