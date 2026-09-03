// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    public static class WitWeaverSnapshotMigrator
    {
        public static WitWeaverGameSnapshot Migrate(WitWeaverGameSnapshot snapshot)
        {
            if (snapshot == null) return null;

            switch (snapshot.SchemaVersion)
            {
                case "1.0":
                    return MigrateGame_1_0_to_1_1(snapshot);
                case WitWeaverGameSnapshot.CurrentSchemaVersion:
                    return snapshot;
                default:
                    Debug.LogWarning($"[WitWeaverSnapshotMigrator] Unknown game snapshot schema version '{snapshot.SchemaVersion}'. Returning unmodified.");
                    return snapshot;
            }
        }

        public static WitWeaverSettingsSnapshot Migrate(WitWeaverSettingsSnapshot snapshot)
        {
            if (snapshot == null) return null;

            switch (snapshot.SchemaVersion)
            {
                case "1.0":
                    return MigrateSettings_1_0(snapshot);
                default:
                    Debug.LogWarning($"[WitWeaverSnapshotMigrator] Unknown settings snapshot schema version '{snapshot.SchemaVersion}'. Returning unmodified.");
                    return snapshot;
            }
        }

        private static WitWeaverGameSnapshot MigrateGame_1_0_to_1_1(WitWeaverGameSnapshot snapshot)
        {
            // 1.1 added Collection variables. No Collections existed in 1.0 snapshots, so the
            // data passes through unchanged and only the version string is updated.
            snapshot.SchemaVersion = "1.1";
            return snapshot;
        }

        private static WitWeaverSettingsSnapshot MigrateSettings_1_0(WitWeaverSettingsSnapshot snapshot)
        {
            // Version 1.0 is the current version, no migration needed.
            return snapshot;
        }
    }
}