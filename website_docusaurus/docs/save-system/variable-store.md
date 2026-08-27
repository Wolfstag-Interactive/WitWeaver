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

The first four types hold a single value. The two Collection types instead hold a whole group of named values inside one variable. See [Collections](#collections) below.

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

All `Set` methods overwrite any existing value for that key. They return `false` if the write was rejected, which happens when the entry is marked read-only or the key already belongs to a different kind of variable. If the key does not exist yet, a new entry is created automatically. `Session` variables live only in memory, while `Global` and `Conversation` variables go into the same list the save system writes to disk. If you leave out the `scope` parameter, it defaults to `ConvoVariableScope.Global`. To give a variable a default value, description, or tags that show up in the inspector, declare it up front (see [Inspector declaration](#declaring-variables-in-the-inspector)).

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
`GetVariable()` returns `null` if the variable does not exist. Using the result without checking for `null` first will cause a `NullReferenceException`. Prefer the `TryGet` methods in gameplay code unless you have pre-declared the variable and are certain it will be present.
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

A **Collection** is a variable that holds a group of named values inside it. Each entry in the group has its own text sub-key paired with an `int` or `string` value. Collections are a natural fit for inventory-style data: item counts, per-character relationship values, discovered locations, unlocked recipes, dialogue topics the player has heard, completed side quests, etc.

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

- **You always work through the methods above.** The store never hands out the raw data inside a Collection. This is what lets it fire change events on every write and keep the inspector display accurate.
- **`GetCollectionKeys` returns a fresh copy every time**, so it is safe to loop over the returned list while adding or removing entries from the Collection.
- **Removing the last entry leaves an empty Collection**, not a deleted variable. An empty inventory is still a valid inventory, so `HasVariable(key)` stays `true`.
- **A Collection cannot be accidentally replaced.** Calling a single-value `Set` method (like `SetInt`) on a Collection key, or a Collection method on a single-value variable, logs a warning and changes nothing. Single-value `TryGet` calls on a Collection key simply return `false`.
- **A Read Only Collection rejects every change**, including removing entries and clearing.
- Entry values can only be `int` or `string`. There is no float version (float rounding tends to drift in count-style data) and no bool version (to track a yes/no per key, add or remove the key itself). Collections cannot be nested inside each other.

### Scopes and authored defaults

Collections use the same three scopes as every other variable: `Global`, `Conversation`, and `Session`. Collections you set up in the inspector get one extra safety guarantee: **the values you authored are never changed at runtime**. The first time the game modifies one of these Collections, the store quietly makes a temporary copy in memory and edits that copy instead, leaving your original untouched. Reads automatically use the copy once it exists. Saving writes out the copy's current values, and loading a save restores into the copy as well. When you leave Play Mode (or call `ResetVariable`), the copy is thrown away and the Collection is back to exactly what you authored.

### Change events

Every change to a Collection fires `OnVariableChanged` once, using the Collection's own key. The values passed to the event describe the single entry that changed (numbers arrive as strings), not the whole Collection:

| Operation | oldValue | newValue |
|---|---|---|
| `SetCollection*` (new sub-key) | `null` | the value |
| `SetCollection*` (existing sub-key) | previous value | new value |
| `RemoveCollectionEntry` | removed value | `null` |
| `ClearCollection` | `null` | `null` (a single event, not one per entry) |

You cannot listen to one specific entry inside a Collection. Subscribe to the Collection's key instead.

### Authoring Collections in the inspector

Selecting **CollectionInt** or **CollectionString** as an entry's Type replaces the single Default Value field with a **Collection Defaults** list, where each row is one sub-key and its value. Rows can be dragged to reorder, and the + and - buttons add and remove them. Sub-keys must be unique and cannot be empty; rows that break either rule are tinted red. The red tint is only a hint and never blocks you while editing. If a duplicate does make it into Play Mode, the first row wins and a warning is logged.

During Play Mode the row shows a short read-only summary (`Collection - N entries`) of the live values. The row also highlights orange as soon as anything in the Collection has been changed during the current play session. The highlight means "this was touched", not "this is different from the default": writing a value that happens to match the authored default still shows orange.

:::warning[Conditions read single values only]
YAML condition expressions cannot look inside a Collection. If a dialogue branch needs to react to a count (for example "has at least 3 keys"), keep a copy of that number in a regular Int variable whenever you update the Collection, and write the condition against that variable.
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

For single-value variables, `OnVariableChanged` only fires when the value actually changed. For Collections, the event describes the entry that changed. See [Change events](#change-events).

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
The defaults you author here are the **starting state for a new game**. The save system builds on top of them. Single-value variables (`Bool`, `Int`, `Float`, `String`) update their entry directly when the game writes to them; the inspector remembers the authored value separately so it can still show you what changed. Collection variables never touch the authored data at all, because the game works on a temporary in-memory copy (see [Scopes and authored defaults](#scopes-and-authored-defaults)). When a save slot is loaded, the saved values are applied on top of this baseline. Leaving Play Mode reloads the asset, so the next session starts from the authored defaults again.
:::

---

## Internal storage model

The variable store keeps two internal entry lists:

| List | Access | Lifetime |
|---|---|---|
| `_persistentEntries` | Serialized field; authored in the inspector | Lives with the asset. Holds `Global` and `Conversation` scoped entries. |
| `_sessionEntries` | `[NonSerialized]`; created lazily | In-memory only; gone when Play Mode exits or the application closes. Holds `Session` scoped entries plus the temporary copies of Collections that have been changed. |

When reading a variable, the store checks `_sessionEntries` first and then falls back to `_persistentEntries`. Where a write lands depends on the kind of variable. Single-value writes go to the list that matches the requested scope, so a `Global` or `Conversation` value updates its persistent entry directly. Collection changes always go to the session list: the first change copies the authored Collection there, and every change after that edits the copy. That is why authored Collection defaults are never modified at runtime.

:::info[For Advanced Users]
The variable store editor tracks a **snapshot of authored defaults** captured when Unity exits Edit Mode. During Play Mode, any variable whose current runtime value differs from its authored default is highlighted in orange in the inspector. This **live diff** makes it easy to see at a glance which variables have been touched during a test playthrough, without running a separate debug overlay.

You can also use the editor's **scope filter** and **text filter toolbar** to quickly find variables in large stores. The editor repaints at 0.1-second intervals during Play Mode so the live diff stays current without requiring manual inspector focus.
:::

---

## Clearing variables

```csharp
// Clear all Session-scoped variables. Global and Conversation variables are
// not affected, and neither are the temporary copies of changed Collections.
_store.ClearByScope(ConvoVariableScope.Session);

// Clear all variables of a specific scope. Single-value variables are removed;
// authored Collections revert to their authored defaults instead of being deleted.
_store.ClearByScope(ConvoVariableScope.Conversation);

// Reset a single Collection to its authored defaults, or remove it entirely if
// it was created at runtime. Single-value variables are not supported and log
// a warning.
_store.ResetVariable("inventory");
```

These are useful during scene transitions or when starting a new game: clear Conversation-scoped variables between conversations, or clear all session variables on "New Game".
