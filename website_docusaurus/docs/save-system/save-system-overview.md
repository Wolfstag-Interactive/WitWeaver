---
sidebar_position: 1
title: Save System Overview
---

# Save System Overview

The WitWeaver™ Save System persists conversation progress and game state across play sessions. It is a separate, optional module; the core WitWeaver runner has no hard dependency on it. Drop it in when your project needs durable progress tracking.

---

## What the save system tracks

The save system records four categories of data:

- **Visited dialogue lines**: which lines a player has already seen in each conversation, so conditional logic like "if visited" and "skip-if-seen" works correctly across sessions.
- **Active line position**: where in a conversation the player last left off, enabling resume-from-exact-position behaviour.
- **Variables**: typed key-value pairs (Bool, Int, Float, String) scoped to a single conversation, globally across all conversations, or only for the current session.
- **Player settings**: language preference and any user-defined settings fields.

---

## The three main components

The save system is built from three cooperating components, each with a focused responsibility:

| Component | Type | Role |
|---|---|---|
| `WitWeaverSaveManager` | ScriptableObject | Central orchestrator: owns the save provider, manages save slots, assembles and restores the full game snapshot. |
| `WitWeaverConversationSaveManager` | MonoBehaviour | Per-conversation tracker: records progress for one conversation and provides a resume context to the WitWeaver runner. |
| `WitWeaverVariableStore` | ScriptableObject | Typed, scoped variable storage: the runtime database for gameplay state that dialogue can read and write. |

:::note
A **ScriptableObject** is a Unity asset that lives in your project files and can be referenced from any scene. A **MonoBehaviour** is a script component you attach to a GameObject in a scene. The save system uses both: `WitWeaverSaveManager` and `WitWeaverVariableStore` are shared project assets, while `WitWeaverConversationSaveManager` is a per-runner scene component that sits alongside your `WitWeaver` component.
:::

---

## Data model

The full game state is assembled into a `WitWeaverGameSnapshot` before it is handed to the save provider for serialization:

```
WitWeaverGameSnapshot
├── SchemaVersion              ← format version string (e.g. "1.0")
├── GlobalVariables []         ← variables with Global scope
└── Conversations []
    └── ConversationSnapshot
        ├── ConversationId     ← the WitWeaverConversationData's stable GUID
        ├── ActiveLineId       ← LineID of the last displayed line
        ├── VisitedLineIds []  ← all LineIDs seen so far in this conversation
        ├── IsComplete         ← true if the conversation reached its end
        └── Variables []       ← variables with Conversation scope
```

Player settings are stored separately as a `WitWeaverSettingsSnapshot` (language code plus any user-defined fields). Settings are saved and loaded through `WitWeaverSaveManager.SaveSettings()` / `InitializeSettings()`, independently of game slot saves.

---

## ConversationGuid: the stable key

Every `WitWeaverConversationData` asset has a `ConversationGuid` property, a stable UUID auto-generated the first time the asset is imported or validated. The save system uses this GUID as the dictionary key for `ConversationSnapshot` entries.

This means:

- **Renaming the asset file** does not break existing save files.
- **Moving the asset** to a different folder does not break existing save files.
- Only deleting the asset and creating a new one (which gets a fresh GUID) would orphan a saved snapshot.

:::warning
If you duplicate a `WitWeaverConversationData` asset in the Project window, the duplicate starts with the same GUID as the original. The `WitWeaverGuidValidator` editor tool detects this at import time and regenerates the GUID on the duplicate automatically. Always let Unity reimport before referencing a newly duplicated asset.
:::

---

## How the components work together

At a high level, the data flow is:

1. **At startup**: `WitWeaverSaveManager.Initialize()` is called (by your bootstrapper). If a save slot is loaded, `WitWeaverSaveManager.Load(slot)` reads the snapshot from disk and distributes `ConversationSnapshot` entries to any registered `WitWeaverConversationSaveManager` components.

2. **During play**: `WitWeaverConversationSaveManager` subscribes to `WitWeaver` events. As the player advances lines and makes choices, it updates its local `ConversationSnapshot` and, depending on the auto-commit flags, calls `CommitSnapshot()` to push the updated snapshot to `WitWeaverSaveManager`.

3. **At save time**: `WitWeaverSaveManager.Save(slot)` collects all registered snapshots, appends global variables from `WitWeaverVariableStore`, and writes the assembled `WitWeaverGameSnapshot` to disk via the active `IWitWeaverSaveProvider`.

4. **At resume time**: When `WitWeaver.PlayConversation()` is called, it checks for an `IWitWeaverStartContextProvider` on the same GameObject. `WitWeaverConversationSaveManager` implements this interface and returns a `WitWeaverStartContext` describing whether to start fresh, resume from the active line, or restart from line 0 with variables restored.

---

## Bootstrapper setup

The recommended pattern is a **bootstrapper**, a persistent GameObject that initializes the save system before any scene-specific code runs. A prefab called `WitWeaverSaveManagerBootstrapper` is included in the save system samples.

The bootstrapper's `Awake()` method should call:

```csharp
_saveManager.Initialize();
_saveManager.InitializeSettings(); // Load language preference and other settings
```

Place the bootstrapper in a scene that loads first (e.g. a loading or menu scene) and mark it with `DontDestroyOnLoad` if your save manager needs to persist across scene changes.

:::tip
If you use an additive scene loading pattern, put the bootstrapper in your persistent/manager scene. If you use single-scene loading, put it in your main menu scene and ensure it loads before any conversation objects call `PlayConversation()`.
:::

---

## Namespace and assembly

All save system types live in the `WolfstagInteractive.WitWeaver.SaveSystem` namespace, compiled into the `WolfstagInteractive.WitWeaver.SaveSystem` assembly. Add this using directive to any script that references save system types:

```csharp
using WolfstagInteractive.WitWeaver.SaveSystem;
```

The save system assembly references the WitWeaver runtime assembly. The dependency only flows one way; the core WitWeaver runtime has no reference back to the save system. Interfaces that span both assemblies (such as `IWitWeaverStartContextProvider`) live in the WitWeaver runtime assembly.

---

## Next steps

| Page | What you will learn |
|---|---|
| [Save Manager](save-manager) | Inspector fields, initialization, save/load API, events, and snapshot injection |
| [Conversation Save Manager](conversation-save-manager) | Per-conversation setup, start modes, auto-commit flags, and the AskViaEvent pattern |
| [Variable Store](variable-store) | Variable scopes, typed read/write API, change listeners, and the inspector live-diff |
| [Save Providers](save-providers) | Built-in JSON and YAML providers, and how to implement a custom backend |
| [Snapshot Migration](snapshot-migration) | How the migrator works and how to register a migration step for schema changes |
