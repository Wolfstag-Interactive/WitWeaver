// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    /// <summary>
    /// Session-scoped record of the most recent spreadsheet import result per conversation asset, so
    /// the inspector can show the outcome of both button-triggered and watcher-triggered imports.
    /// Cleared by domain reloads (deliberately not persisted).
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverSpreadsheetImportStatus.html")]
    internal static class WitWeaverSpreadsheetImportStatus
    {
        private sealed class Entry
        {
            public bool Success;
            public string Message;
            public DateTime WhenUtc;
        }

        private static readonly Dictionary<EntityId, Entry> _byAsset = new Dictionary<EntityId, Entry>();

        internal static void Record(WitWeaverConversationData data, bool success, string message)
        {
            if (data == null) return;
            _byAsset[data.GetEntityId()] = new Entry
            {
                Success = success,
                Message = message,
                WhenUtc = DateTime.UtcNow
            };
        }

        internal static bool TryGet(WitWeaverConversationData data,
            out bool success, out string message, out DateTime whenUtc)
        {
            success = false;
            message = null;
            whenUtc = default;
            if (data == null || !_byAsset.TryGetValue(data.GetEntityId(), out var entry))
                return false;

            success = entry.Success;
            message = entry.Message;
            whenUtc = entry.WhenUtc;
            return true;
        }
    }
}
#endif
