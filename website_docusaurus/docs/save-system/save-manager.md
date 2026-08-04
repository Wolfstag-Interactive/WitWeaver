---
sidebar_position: 2
title: Save Manager
---

# Save Manager

`ConvoCoreSaveManager` is the central hub of the save system. It owns the active save provider, manages save slots, assembles the full game snapshot, and distributes restored snapshots to all registered conversation savers.

---

## What kind of object is it?

`ConvoCoreSaveManager` is a **ScriptableObject**, a project asset rather than a scene component. You create one per project, keep it in your project files, and reference it from scene GameObjects (your bootstrapper, conversation runners, UI, etc.) via serialized fields.

**Create via**: Right-click in the Project window → **Create → ConvoCore → Runtime → Save Manager**

---

## Inspector fields

| Field | Description |
|---|---|
| **Variable Store** | Reference to your `ConvoVariableStore` asset. The save manager reads Global-scoped variables from here when assembling a snapshot, and writes them back when restoring. |
| **Settings State** | Reference to a `ConvoCoreSettingsSnapshot` asset. Used to persist language preference and other settings independently of game slot data. |
| **Use Yaml** | When enabled, the built-in YAML provider is used instead of JSON. Both produce the same data - YAML is more readable for debugging, JSON is more compact. Has no effect if you inject a custom provider via `SetProvider()`. |
| **Default Slot** | The slot name used by `SaveToDefaultSlot()` and `LoadFromDefaultSlot()`. Useful for simple single-slot games. |

---

## Initialization

Call `Initialize()` before any save or load operation. The bootstrapper is the right place for this.

```csharp
using WolfstagInteractive.ConvoCore.SaveSystem;
using UnityEngine;

public class GameBootstrapper : MonoBehaviour
{
    [SerializeField] private ConvoCoreSaveManager _saveManager;

    private void Awake()
    {
        _saveManager.Initialize();
        _saveManager.InitializeSettings(); // Load language and other settings
    }
}
```

:::warning
`Initialize()` must be called before any `Save()`, `Load()`, `HasSave()`, or `DeleteSave()` call. Invoking those methods on an uninitialized manager logs a warning and returns without taking action. Ensure your bootstrapper's `Awake()` runs before any scene objects that call `PlayConversation()`; use Unity's Script Execution Order settings if needed.
:::

---

## Saving and loading

```csharp
// Save the current game state to a named slot
_saveManager.Save("slot_1");

// Load a saved slot - restores the game state from disk
_saveManager.Load("slot_1");

// Save and load using the Default Slot configured in the inspector
_saveManager.SaveToDefaultSlot();
_saveManager.LoadFromDefaultSlot();

// Check whether a save slot exists on disk
bool hasSave = _saveManager.HasSave("slot_1");

// Delete a save slot from disk (permanent - cannot be undone at runtime)
_saveManager.DeleteSave("slot_1");
```

:::tip
For games with a single save slot, set the **Default Slot** field in the inspector and use `SaveToDefaultSlot()` / `LoadFromDefaultSlot()` throughout your code. This avoids hardcoding slot strings in multiple scripts.
:::

---

## Settings

Settings (language preference and user-defined fields) are stored separately from game slot data and do not require a slot name.

```csharp
// Load saved settings from disk and apply them (called at startup)
_saveManager.InitializeSettings();

// Write current settings to disk
_saveManager.SaveSettings();
```

`InitializeSettings()` also triggers `ConvoCoreLanguageManager` to apply the saved language code, so the correct locale is active before any UI renders.

---

## Snapshot registry

`ConvoCoreConversationSaveManager` components call `RegisterConversationSnapshot()` automatically when they commit. You rarely need to call these directly, but they are public for edge cases:

```csharp
// Register or update a conversation snapshot in the manager's in-memory registry
_saveManager.RegisterConversationSnapshot(snapshot);

// Retrieve the most recently registered snapshot for a conversation
ConversationSnapshot snap = _saveManager.GetConversationSnapshot(conversationGuid);

// Returns null if no snapshot has been registered for that GUID
```

---

## Raw snapshot access

```csharp
// Assemble and return the full game snapshot (does not write to disk)
ConvoCoreGameSnapshot snapshot = _saveManager.GetGameSnapshot();

// Restore the manager's in-memory state from a snapshot (does not read from disk)
_saveManager.RestoreGameSnapshot(snapshot);
```

These are useful when you need to inspect the snapshot before saving, pass it to a cloud service manually, or implement a custom save flow.

---

## Injecting a custom save provider

The active provider can be replaced at runtime before or after `Initialize()`. The new provider takes effect on the next `Save()` or `Load()` call.

```csharp
// Swap in a custom provider (e.g. cloud saves, encrypted storage)
_saveManager.SetProvider(new MyCloudSaveProvider());
```

See [Save Providers](save-providers) for how to implement a custom provider.

---

## Events

All events fire on the main thread. Subscribe to them from any MonoBehaviour.

| Event | Signature | When it fires |
|---|---|---|
| `OnInitialized` | `Action` | After `Initialize()` completes successfully. |
| `OnSaveCompleted` | `Action<string>` | After a successful `Save()` or `SaveToDefaultSlot()`. The string argument is the slot name. |
| `OnLoadCompleted` | `Action<string>` | After a successful `Load()` or `LoadFromDefaultSlot()`. The string argument is the slot name. |
| `OnSettingsSaved` | `Action` | After `SaveSettings()` completes. |
| `OnSettingsLoaded` | `Action` | After `InitializeSettings()` completes. |
| `OnSnapshotAssembled` | `Action<ConvoCoreGameSnapshot>` | After the snapshot is assembled but **before** it is written to disk. |

### Subscribing to events

```csharp
private void OnEnable()
{
    _saveManager.OnSaveCompleted += HandleSaveCompleted;
    _saveManager.OnLoadCompleted += HandleLoadCompleted;
}

private void OnDisable()
{
    _saveManager.OnSaveCompleted -= HandleSaveCompleted;
    _saveManager.OnLoadCompleted -= HandleLoadCompleted;
}

private void HandleSaveCompleted(string slot)
{
    Debug.Log($"Game saved to slot: {slot}");
    // Show a "Game Saved" indicator in the UI
}

private void HandleLoadCompleted(string slot)
{
    Debug.Log($"Game loaded from slot: {slot}");
    // Transition to the gameplay scene
}
```

:::info[For Advanced Users]
`OnSnapshotAssembled` fires with the fully assembled `ConvoCoreGameSnapshot` **before** it is handed to the save provider. This is the extension point for injecting game-specific data (player level, inventory, quest flags, etc.) into the snapshot without modifying the save system itself.

```csharp
private void OnEnable()
{
    _saveManager.OnSnapshotAssembled += InjectGameData;
}

private void OnDisable()
{
    _saveManager.OnSnapshotAssembled -= InjectGameData;
}

private void InjectGameData(string slot, ConvoCoreGameSnapshot snapshot)
{
    // Add a global variable to carry player level into the snapshot
    snapshot.GlobalVariables.Add(new ConvoVariableEntry
    {
        CoreVariable = new ConvoCoreVariable
        {
            Key = "player_level",
            Type = ConvoVariableType.Int
        }.SetInt(_playerLevel),
        Scope = ConvoVariableScope.Global
    });
}
```

On load, read the value back from the snapshot after `OnLoadCompleted` fires using `_saveManager.GetGameSnapshot()`.
:::

---

## Multi-slot UI example

A simple slot selection implementation:

```csharp
public class SaveSlotUI : MonoBehaviour
{
    [SerializeField] private ConvoCoreSaveManager _saveManager;
    [SerializeField] private string[] _slots = { "slot_1", "slot_2", "slot_3" };

    public void OnSaveSlotClicked(int index)
    {
        string slot = _slots[index];
        _saveManager.Save(slot);
    }

    public void OnLoadSlotClicked(int index)
    {
        string slot = _slots[index];
        if (_saveManager.HasSave(slot))
            _saveManager.Load(slot);
        else
            Debug.LogWarning($"No save found in slot: {slot}");
    }

    public void OnDeleteSlotClicked(int index)
    {
        string slot = _slots[index];
        _saveManager.DeleteSave(slot);
    }
}
```
