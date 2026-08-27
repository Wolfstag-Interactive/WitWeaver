using UnityEngine;

namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    public static class ConvoCoreSnapshotMigrator
    {
        public static ConvoCoreGameSnapshot Migrate(ConvoCoreGameSnapshot snapshot)
        {
            if (snapshot == null) return null;

            switch (snapshot.SchemaVersion)
            {
                case "1.0":
                    return MigrateGame_1_0_to_1_1(snapshot);
                case ConvoCoreGameSnapshot.CurrentSchemaVersion:
                    return snapshot;
                default:
                    Debug.LogWarning($"[ConvoCoreSnapshotMigrator] Unknown game snapshot schema version '{snapshot.SchemaVersion}'. Returning unmodified.");
                    return snapshot;
            }
        }

        public static ConvoCoreSettingsSnapshot Migrate(ConvoCoreSettingsSnapshot snapshot)
        {
            if (snapshot == null) return null;

            switch (snapshot.SchemaVersion)
            {
                case "1.0":
                    return MigrateSettings_1_0(snapshot);
                default:
                    Debug.LogWarning($"[ConvoCoreSnapshotMigrator] Unknown settings snapshot schema version '{snapshot.SchemaVersion}'. Returning unmodified.");
                    return snapshot;
            }
        }

        private static ConvoCoreGameSnapshot MigrateGame_1_0_to_1_1(ConvoCoreGameSnapshot snapshot)
        {
            // 1.1 added Collection variables. No Collections existed in 1.0 snapshots, so the
            // data passes through unchanged and only the version string is updated.
            snapshot.SchemaVersion = "1.1";
            return snapshot;
        }

        private static ConvoCoreSettingsSnapshot MigrateSettings_1_0(ConvoCoreSettingsSnapshot snapshot)
        {
            // Version 1.0 is the current version, no migration needed.
            return snapshot;
        }
    }
}