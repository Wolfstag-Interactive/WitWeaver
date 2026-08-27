---
sidebar_position: 6
title: Snapshot Migration
---

# Snapshot Migration

As you develop and ship updates to your game, the structure of your save data may change: new fields added, variables renamed, conversation GUIDs regenerated, or scope assignments altered. `WitWeaverSnapshotMigrator` ensures that save files created with an older version of the schema can still be loaded correctly after an update.

---

## How it works

`WitWeaverSnapshotMigrator.Migrate()` is called automatically by `WitWeaverSaveManager` inside `Load()` and `InitializeSettings()`, before the snapshot is distributed to the rest of the system. The migrator reads the `SchemaVersion` string from the snapshot and applies the chain of migration steps in version order until the snapshot is current.

The migrator itself is a static class with one step method per schema change, dispatched by a `switch` on the version string. WitWeaver ships as source, so adding a step means adding a case to that switch (see [Adding a migration step](#adding-a-migration-step)).

The migration pipeline is transparent to your gameplay code - you never need to call `Migrate()` directly.

---

## Schema version field

Both `WitWeaverGameSnapshot` and `WitWeaverSettingsSnapshot` carry a `SchemaVersion` string property. When the save manager writes a snapshot, it stamps the current schema version into this field. When it reads a snapshot back, the stamped version tells the migrator how many steps (if any) need to be applied.

The current schema version is `"1.1"` (exposed in code as `WitWeaverGameSnapshot.CurrentSchemaVersion`).

:::note
For the majority of projects, the migrator requires no configuration whatsoever. It is infrastructure for forward-compatibility, a safety net that costs nothing until you need it. You only need to register migration steps if you deliberately change the shape of the save schema between shipped versions.
:::

---

## Built-in migrations

| From | To | What changed |
|---|---|---|
| `1.0` | `1.1` | [Collection variables](./variable-store.md#collections) were added. Collections are stored as a list of sub-key/value pairs inside each variable, so no 1.0 data needs to be transformed. The step passes the snapshot through unchanged and only updates the version string. |

The 1.0 → 1.1 step is the first registered migration and establishes the pattern for future schema changes: a `"1.0"` save loads through the migrator, comes out stamped `"1.1"`, and continues through the normal restore path.

---

## When to write a migration step

You need a migration step when a **shipped** version of your game wrote save files in a format that the current version no longer reads correctly. Common triggers:

- Renaming a variable key that was previously saved to disk.
- Changing a variable's scope (e.g. moving a key from Conversation scope to Global scope).
- Regenerating the GUID on a `WitWeaverConversationData` asset after it was already shipped (avoids this where possible - see the warning below).
- Adding a required field to `WitWeaverGameSnapshot` or `WitWeaverSettingsSnapshot` that has no sensible default value.
- Removing a field whose presence in old saves would cause a deserialization conflict.

:::warning
Regenerating a `ConversationGuid` after the game has shipped is a destructive operation. All existing save files reference the old GUID. If you must regenerate, write a migration step that renames the old GUID key to the new one in every `ConversationSnapshot`. In general, treat `ConversationGuid` as immutable once the asset is shipped.
:::

---

## Adding a migration step

Steps live inside `WitWeaverSnapshotMigrator` (`SaveSystem/Migration/WitWeaverSnapshotMigrator.cs`). To add one, extend the `switch` in `Migrate()` with a case for the previous version, write a private step method, and bump `WitWeaverGameSnapshot.CurrentSchemaVersion`. Each case chains into the next so older saves walk the whole ladder:

```csharp
public static WitWeaverGameSnapshot Migrate(WitWeaverGameSnapshot snapshot)
{
    if (snapshot == null) return null;

    switch (snapshot.SchemaVersion)
    {
        case "1.0":
            // 1.0 saves pass through 1.0→1.1, then fall into the 1.1 case below.
            return Migrate(MigrateGame_1_0_to_1_1(snapshot));
        case "1.1":
            return Migrate(MigrateGame_1_1_to_1_2(snapshot));
        case WitWeaverGameSnapshot.CurrentSchemaVersion:
            return snapshot;
        default:
            Debug.LogWarning($"[WitWeaverSnapshotMigrator] Unknown game snapshot schema version '{snapshot.SchemaVersion}'. Returning unmodified.");
            return snapshot;
    }
}

// Example step: schema 1.1 to 1.2
// - Renamed global variable "quest_started" to "main_quest_active"
// - Added a default for the new "faction_standing" global variable
private static WitWeaverGameSnapshot MigrateGame_1_1_to_1_2(WitWeaverGameSnapshot snapshot)
{
    // Rename a global variable key
    var questStarted = snapshot.GlobalVariables
        .Find(e => e.CoreVariable != null && e.CoreVariable.Key == "quest_started");
    if (questStarted != null)
        questStarted.CoreVariable.Key = "main_quest_active";

    // Add a new global variable with a default value (idempotent)
    bool alreadyExists = snapshot.GlobalVariables
        .Exists(e => e.CoreVariable != null && e.CoreVariable.Key == "faction_standing");
    if (!alreadyExists)
    {
        snapshot.GlobalVariables.Add(new WitWeaverVariableEntry
        {
            CoreVariable = new WitWeaverVariable
            {
                Key = "faction_standing",
                Type = WitWeaverVariableType.Int
            }.SetInt(0),
            Scope = WitWeaverVariableScope.Global
        });
    }

    snapshot.SchemaVersion = "1.2";
    return snapshot;
}
```

With this chain in place, a save file stamped `"1.0"` has **both** steps applied in sequence before the snapshot is used. A save stamped `"1.1"` only gets the second step. A save already at the current version passes through with no changes.

The built-in `1.0 → 1.1` step ([Collections](./variable-store.md#collections)) follows exactly this pattern and is a good template to copy.

---

## Settings migration

Settings snapshots (`WitWeaverSettingsSnapshot`) are migrated separately through the `Migrate(WitWeaverSettingsSnapshot)` overload in the same class, using the same switch-and-step pattern:

```csharp
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
```

The settings schema is still at `"1.0"`. The Collections change in game schema 1.1 did not affect settings.

---

## Versioning conventions

When you make a schema change:

1. Bump `WitWeaverGameSnapshot.CurrentSchemaVersion` (and/or the settings snapshot's version if settings changed). Keep the version as a `"major.minor"` string: increment `major` for breaking changes, `minor` for additive changes.

2. Note what changed in the version constant's doc comment:

```csharp
public class WitWeaverGameSnapshot
{
    /// <summary>
    /// Schema version written by the current runtime.
    /// 1.0 - initial release
    /// 1.1 - added Collection variables (list-of-pairs inside WitWeaverVariable)
    /// </summary>
    public const string CurrentSchemaVersion = "1.1";

    public string SchemaVersion = CurrentSchemaVersion;
    // ...
}
```

3. Add the corresponding migration step to the migrator's switch (see above).

4. Test the migration by manually editing a save file's `SchemaVersion` field back to an older version and loading it in Play Mode, verifying the migrated values are correct.

---

## Migration step requirements

:::info[For Advanced Users]
Migration steps are applied in ascending version order. If a save file is multiple versions behind, all steps in the chain are applied sequentially: each `case` in the switch runs its step and hands the result to the next version's handling, so a `"1.0"` save with steps for `1.0→1.1` and `1.1→1.2` gets both applied in that order.

**Idempotency**: Each migration step should be safe to apply more than once. Guard all mutations with existence checks (as shown in the examples above). This protects against edge cases where the migrator is accidentally called twice on the same snapshot during development.

**Statelessness**: Migration steps receive only the snapshot - they do not have access to Unity assets, the variable store, or any other runtime state. If your migration needs to look up a GUID from a `WitWeaverConversationData` asset, bake the GUID into a constant in your migration code at the time you write the step. Do not rely on loading the asset at migration time, as it may not be available in all contexts (e.g. headless builds).

**Null safety**: Always check for `null` before accessing nested collections. Old save files may be missing fields that were added in later schema versions - the deserializer initialises missing collections as `null`, not as empty lists.
:::

---

## Detecting missing migration steps

If the migrator reads a snapshot whose `SchemaVersion` has no matching case in the switch (for example a save written by a newer build, or a version whose step was never added), it logs a warning and passes the snapshot through unmodified:

```
[WitWeaverSnapshotMigrator] Unknown game snapshot schema version '2.0'. Returning unmodified.
```

An unknown *newer* version typically indicates a player is running an older build after saving with a newer one. There is no automatic fix for downgrade scenarios; handle this case by displaying a warning to the player or preventing the load. An unknown *older* version means a migration step is missing from the chain.
