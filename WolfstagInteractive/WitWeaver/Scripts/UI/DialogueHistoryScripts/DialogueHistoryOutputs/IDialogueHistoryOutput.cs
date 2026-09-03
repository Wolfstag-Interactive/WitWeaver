// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

namespace WolfstagInteractive.WitWeaver
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