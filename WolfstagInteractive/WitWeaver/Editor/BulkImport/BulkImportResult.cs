// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

namespace WolfstagInteractive.WitWeaver.Editor
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
