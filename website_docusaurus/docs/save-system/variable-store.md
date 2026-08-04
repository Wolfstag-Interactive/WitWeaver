---
sidebar_position: 4
title: Variable Store
---

# Variable Store

`ConvoVariableStore` is a ScriptableObject that stores typed, scoped key-value pairs: a lightweight runtime database for gameplay state that dialogue can read and write. It is the bridge between what happens in a conversation and the rest of your game.

**Create via**: Right-click in the Project window → **Create → ConvoCore → Runtime → Variable Store**

One variable store asset can serve your entire project. Create additional stores only if you need strict isolation between unrelated systems (for example, a separate store for a mini-game).

---

## Variable scopes

Every variable has a scope that determines how it is persisted:

| Scope | Persisted? | Where saved | Description |
|---|---|---|---|
| **Global** | Yes | `ConvoCoreGameSnapshot.GlobalVariables` | Shared across all conversations. Use for player-wide state: quest progress, relationship values, story flags that span scenes. |
| **Conversation** | Yes | Inside each `ConversationSnapshot.Variables` | Belongs to one conversation. Use for per-NPC state: whether the player was rude, which branch they took, how many times they have spoken to this character. |
| **Session** | No | Memory only | Reset when the application closes or Play Mode exits. Never written to disk. Use for temporary flags that exist only within a single play session. |

:::note
Think of **Global** as your save file's top-level entries: "has the player freed the village?". **Conversation** scope is per-NPC state: "did the player choose the aggressive option with this merchant?". **Session** scope is for scratch variables: counters, temporary flags, or UI state that is meaningless after a restart.
:::

---

## Variable types

Variables are strongly typed. Supported types:

| Enum value | C# type | Inspector label |
|---|---|---|
| `ConvoVariableType.Bool` | `bool` | Bool |
| `ConvoVariableType.Int` | `int` | Int |
| `ConvoVariableType.Float` | `float` | Float |
| `ConvoVariableType.String` | `string` | String |
| `ConvoVariableType.CollectionInt` | sub-entries of `string → int` | CollectionInt |
| `ConvoVariableType.CollectionString` | sub-entries of `string → string` | CollectionString |

Attempting to read a variable as the wrong type returns the default value for that type (e.g. `0` for Int, `false` for Bool) rather than throwing. Use `TryGet` methods to distinguish between "variable not found" and "variable has the zero value".

The two Collection types hold a named group of typed sub-entries instead of a single value — see [Collections](#collections) below.

---

## Writing variables

```csharp
using WolfstagInteractive.ConvoCore.SaveSystem;
using UnityEngine;

public class QuestSystem : MonoBehaviour
{
    [SerializeField] private ConvoVariableStore _store;

    public void StartQuest()
    {
        _store.SetBool("quest_started", true, ConvoVariableScope.Global);
        _store.SetInt("quest_step", 1, ConvoVariableScope.Global);
        _store.SetString("quest_giver", "Elder Morin", ConvoVariableScope.Global);
    }

    public void RecordDialogueChoice(string key, string choiceValue)
    {
        _store.SetString(key, choiceValue, ConvoVariableScope.Conversation);
    }

    public void SetSessionFlag(string key)
    {
        _store.SetBool(key, true, ConvoVariableScope.Session);
    }

    public void AddGold(int amount)
    {
        _store.TryGetInt("player_gold", out int current);
        _store.SetInt("player_gold", current + amount, ConvoVariableScope.Global);
    }
}
```

All `Set` methods overwrite any existing value for that key and return `false` if the write was rejected (read-only entry or type conflict). If the key does not exist, a new entry is created in the layer that matches the requested scope: `Session` writes go to the in-memory session layer, while `Global` and `Conversation` writes go to the persistent entry list — the same list the save system exports from. The `scope` parameter defaults to `ConvoVariableScope.Global` when omitted. To author entries with defaults, descriptions, and tags visible in the inspector, declare them up front (see [Inspector declaration](#declaring-variables-in-the-inspector)).

---

## Reading variables

```csharp
// TryGet - returns false if the variable does not exist.
// Use this when the variable might not have been set yet.
if (_store.TryGetBool("quest_started", out bool questStarted))
{
    Debug.Log($"Quest started: {questStarted}");
}
else
{
    Debug.Log("quest_started has not been set.");
}

if (_store.TryGetInt("player_gold", out int gold))
{
    Debug.Log($"Player gold: {gold}");
}

if (_store.TryGetFloat("elapsed_time", out float elapsed))
{
    Debug.Log($"Elapsed time: {elapsed:F1}s");
}

if (_store.TryGetString("last_choice", out string choice))
{
    Debug.Log($"Last choice: {choice}");
}

// Direct access - retrieves the raw ConvoCoreVariable entry.
// Prefer TryGet for gameplay code; use this when you need the full entry metadata.
ConvoCoreVariable variable = _store.GetVariable("player_gold");
int directGold = variable.GetInt();
```

:::warning
`GetVariable()` returns `null` if the variable does not exist — dereferencing the result without a null check will throw a `NullReferenceException`. Prefer the `TryGet` variants in gameplay code unless you have pre-declared the variable and are certain it will be present.
:::

---

## Checking existence

```csharp
bool exists = _store.HasVariable("quest_started");

if (exists)
{
    // Safe to call GetVariable directly
}
```

---

## Collections

A **Collection** is a variable that holds a named group of typed sub-entries — string sub-keys mapped to `int` or `string` values. Collections are built for inventory-style state: item counts, relationship maps, discovered-location sets.

```csharp
// Writes. Creates the Collection (in the session layer) if the top-level key
// does not exist. The scope parameter is required.
_store.SetCollectionInt("inventory", "sword", 2, ConvoVariableScope.Global);
_store.SetCollectionString("relations", "elder", "friendly", ConvoVariableScope.Global);

// Reads. Return false if the Collection is missing, the sub-key is missing,
// or the variable is not a Collection of the requested type.
if (_store.TryGetCollectionInt("inventory", "sword", out int swordCount))
    Debug.Log($"Swords: {swordCount}");

// Membership and structure.
bool hasSword   = _store.HasCollectionEntry("inventory", "sword");
bool removed    = _store.RemoveCollectionEntry("inventory", "sword"); // true if removed
int  itemCount  = _store.GetCollectionCount("inventory");             // 0 if missing
IReadOnlyList<string> keys = _store.GetCollectionKeys("inventory");   // always a copy

// Emptying and resetting.
_store.ClearCollection("inventory");  // empties, but the variable still exists
_store.ResetVariable("inventory");    // reverts to authored defaults (or removes
                                      // the Collection if it was runtime-created)
```

Key rules:

- **The backing dictionary is never exposed.** All reads and writes go through the sub-key methods above, so change events always fire and the live diff stays accurate.
- **`GetCollectionKeys` returns a new copy on every call**, so you can safely mutate the Collection while iterating a previously returned list.
- **Removing the last sub-key leaves an empty Collection**, not a deleted variable — an empty inventory is valid state, and `HasVariable(key)` remains `true`.
- **Type boundaries are enforced**: a scalar `Set` on a Collection key (or a Collection write on a scalar key) is a logged no-op — nothing is silently replaced. Scalar `TryGet` on a Collection key simply returns `false`.
- **`IsReadOnly` blocks every mutating operation**, including `RemoveCollectionEntry` and `ClearCollection`.
- Only `int` and `string` sub-values exist. There is no `CollectionFloat` (float drift invites bugs in count-like data) or `CollectionBool` (model boolean membership as key presence/absence). Nested Collections are not supported.

### Scopes and copy-on-write

Collections participate in scopes exactly like scalars: `Global`, `Conversation`, `Session`. Authored Collections declared in the inspector, however, get one extra guarantee: **the authored data is never mutated at runtime**. The first mutating operation deep-copies the authored Collection into the in-memory session layer and applies the change to the copy; reads resolve the copy first. Saving exports the copy's current values; loading restores into the session layer. Exiting Play Mode (or calling `ResetVariable`) discards the copy and the Collection reverts to its authored defaults.

### Change events

Every mutating Collection operation fires `OnVariableChanged` once, keyed by the top-level key. The payload is the affected **sub-entry** value (ints rendered as strings), not the whole Collection:

| Operation | oldValue | newValue |
|---|---|---|
| `SetCollection*` (new sub-key) | `null` | the value |
| `SetCollection*` (existing sub-key) | previous value | new value |
| `RemoveCollectionEntry` | removed value | `null` |
| `ClearCollection` | `null` | `null` (a single event, not one per sub-key) |

Per-sub-key listeners are not supported — subscribe to the top-level key.

### Authoring Collections in the inspector

Selecting **CollectionInt** or **CollectionString** as an entry's Type replaces the single Default Value field with a reorderable **Collection Defaults** list of sub-key rows. Sub-keys must be unique and non-empty; invalid rows are tinted red but never block editing — if duplicates survive to runtime, the first occurrence wins and a warning is logged.

During Play Mode the row shows a read-only summary (`Collection — N entries`) of the current runtime state. The row highlights orange as soon as the Collection has been **touched** this playthrough — the highlight is a dirty flag, not a content comparison, so a write that happens to restore the authored value still shows orange.

:::warning[Conditions read scalars only]
YAML condition expressions cannot read Collection sub-keys. If a branch needs to react to a count ("has at least 3 keys"), mirror that number into a scalar variable whenever you update the Collection, and condition on the scalar.
:::

---

## Querying by scope or tag

Both queries return `IReadOnlyList<ConvoVariableEntry>`. Each entry wraps the variable itself (`CoreVariable`) together with its `Scope` and `IsReadOnly` flag:

```csharp
// Get all variables in a specific scope
IReadOnlyList<ConvoVariableEntry> globals  = _store.GetByScope(ConvoVariableScope.Global);
IReadOnlyList<ConvoVariableEntry> convVars = _store.GetByScope(ConvoVariableScope.Conversation);

// Get all variables that have a specific tag
IReadOnlyList<ConvoVariableEntry> questVars = _store.GetByTag("quest");

// Combine: all global quest variables
var globalQuestVars = _store.GetByScope(ConvoVariableScope.Global)
    .Where(e => e.CoreVariable.Tags != null && e.CoreVariable.Tags.Contains("quest"));
```

Tags are defined per-variable in the inspector (see [Inspector declaration](#declaring-variables-in-the-inspector)).

---

## Listening for changes

Subscribe to be notified when a specific variable changes, or when any variable changes:

```csharp
private void OnEnable()
{
    // Listen to a specific key. The callback receives the changed variable.
    _store.Subscribe("player_gold", OnGoldChanged);

    // Listen to all changes. The payload is (key, oldValue, newValue) as strings.
    _store.OnVariableChanged += OnAnyVariableChanged;
}

private void OnDisable()
{
    _store.Unsubscribe("player_gold", OnGoldChanged);
    _store.OnVariableChanged -= OnAnyVariableChanged;
}

private void OnGoldChanged(ConvoCoreVariable variable)
{
    _goldDisplay.text = variable.GetInt().ToString();
}

private void OnAnyVariableChanged(string key, string oldValue, string newValue)
{
    Debug.Log($"[VariableStore] {key}: {oldValue} → {newValue}");
}
```

`OnVariableChanged` only fires when the value actually changed for scalar variables. For Collection variables the payload carries the affected sub-entry values — see [Change events](#change-events).

:::tip
Use `Subscribe` / `Unsubscribe` for targeted bindings (e.g. a UI element that displays one variable). Use `OnVariableChanged` for broad listeners like debug overlays or analytics. Always unsubscribe in `OnDisable` to avoid memory leaks when objects are destroyed.
:::

---

## Declaring variables in the inspector

Variables can be pre-declared in the **Variable Store** inspector under `_persistentEntries`. Each entry has:

| Field | Description |
|---|---|
| **Key** | The variable name. Must be unique within the store. |
| **Type** | `Bool`, `Int`, `Float`, `String`, `CollectionInt`, or `CollectionString`. |
| **Default Value** | The authored starting value for a new game. For Collection types this becomes a reorderable **Collection Defaults** list of sub-key/value rows (see [Collections](#collections)). |
| **Scope** | `Global` or `Conversation`. Session-scoped variables cannot be pre-declared. |
| **Description** | Optional notes for your team. Not used at runtime. |
| **Tags** | String tags used with `GetByTag()`. |
| **Read Only** | Prevents runtime writes. Read attempts work normally; write attempts log a warning and do nothing. For Collections this blocks every mutating operation, including remove and clear. |

Pre-declared variables appear in the inspector during Play Mode with their current runtime value shown next to the authored default.

:::warning
The authored defaults represent the **starting state for a new game** — they are the baseline the save system builds on. How runtime writes interact with them depends on the variable kind: scalar `Global`/`Conversation` writes update the entry in place (the inspector's live diff tracks the authored value separately), while **Collection** writes never touch the authored data at all — they mutate a session-layer copy ([copy-on-write](#scopes-and-copy-on-write)). When the save system loads a slot, it restores the saved values on top of this baseline. Exiting Play Mode reloads the asset, so values you see in the next session start from the authored defaults again.
:::

---

## Internal storage model

The variable store keeps two internal entry lists:

| List | Access | Lifetime |
|---|---|---|
| `_persistentEntries` | Serialized field; authored in the inspector | Lives with the asset. Holds `Global` and `Conversation` scoped entries. |
| `_sessionEntries` | `[NonSerialized]`; created lazily | In-memory only; gone when Play Mode exits or the application closes. Holds `Session` scoped entries plus Collection copy-on-write copies. |

When reading a variable, the store checks `_sessionEntries` first, then falls back to `_persistentEntries`. Where writes land depends on the variable kind: scalar writes go to the list matching the requested scope (so `Global`/`Conversation` scalars update the persistent entry in place), while **Collection** mutations always land in the session layer — the first write deep-copies an authored Collection there, so authored Collection defaults are never modified at runtime.

:::info[For Advanced Users]
The variable store editor tracks a **snapshot of authored defaults** captured when Unity exits Edit Mode. During Play Mode, any variable whose current runtime value differs from its authored default is highlighted in orange in the inspector. This **live diff** makes it easy to see at a glance which variables have been touched during a test playthrough, without running a separate debug overlay.

You can also use the editor's **scope filter** and **text filter toolbar** to quickly find variables in large stores. The editor repaints at 0.1-second intervals during Play Mode so the live diff stays current without requiring manual inspector focus.
:::

---

## Clearing variables

```csharp
// Clear all Session-scoped variables (does not affect Global/Conversation entries,
// or Collection copy-on-write copies carrying those scopes)
_store.ClearByScope(ConvoVariableScope.Session);

// Clear all variables of a specific scope. Scalars are removed; authored
// Collections revert to their authored defaults instead of being deleted.
_store.ClearByScope(ConvoVariableScope.Conversation);

// Reset a single Collection variable to its authored defaults (or remove it
// entirely if it was created at runtime). Scalars are not supported and log
// a warning.
_store.ResetVariable("inventory");
```

These are useful during scene transitions or when starting a new game: clear Conversation-scoped variables between conversations, or clear all session variables on "New Game".
