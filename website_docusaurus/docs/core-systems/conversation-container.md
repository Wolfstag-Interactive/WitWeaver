---
sidebar_position: 3
title: Conversation Container
---

# Conversation Container

A `ConversationContainer` is a ScriptableObject that groups multiple conversations and defines a strategy for selecting which one to play. Rather than hard-coding a single conversation into a runner, you can point the runner at a container and let it decide at runtime, cycling through a pool of NPC greetings, playing a story sequence in order, or choosing dialogue based on weighted probability.

---

## Creating a Container

Right-click in the **Project** panel and choose:

**Create → WitWeaver → Conversation Container**

Add `WitWeaverConversationData` assets to the **Entries** list and configure the mode and selection strategy.

---

## Container Modes

### Playlist

Conversations play sequentially: first entry, then second, then third, and so on. When the last conversation ends, the container either loops back to the first entry (if **Loop** is enabled) or stops.

**Use for:** Cutscenes, tutorial sequences, multi-part story beats, any situation where order matters and every conversation must play.

### Selector

The container picks one conversation to play based on a selection strategy. Only one conversation plays per invocation. The strategy determines which entry is chosen.

**Use for:** NPC idle dialogue pools, ambient conversation banks, situations where variety matters more than order.

---

## Selection Strategies (Selector Mode Only)

| Strategy | Behavior |
|---|---|
| **First** | Always plays the first enabled entry in the list. Useful when the first entry represents a fallback or priority dialogue. |
| **Sequential** | Cycles through enabled entries in order, advancing the index on each play. Wraps back to the first entry after the last. |
| **Random** | Picks a random enabled entry each time. The same entry may be chosen multiple times in a row. |
| **WeightedRandom** | Picks randomly, with the probability of each entry proportional to its **Weight** value. Higher weight = higher chance. |

:::note
The **Sequential** strategy stores its current index in a static dictionary keyed by the container asset instance. This means the "which entry is next" position persists for the duration of a play session but resets when you exit Play Mode or restart the application. If you need to persist the sequential position across sessions, use the save system's container tracking.
:::

---

## Entry Fields

Each entry in a container's list exposes the following fields:

| Field | Description |
|---|---|
| **Alias** | A unique string identifier for this entry within the container. This is the string you reference when branching to a specific entry from a dialogue line or player choice. |
| **Conversation Data** | The `WitWeaverConversationData` asset this entry represents. |
| **Enabled** | When unchecked, all selection strategies skip this entry. Use this to temporarily disable a conversation without removing it from the list. |
| **Delay After End Seconds** | (Playlist mode only.) How many seconds to wait after this conversation ends before the next one begins. Set to `0` for no delay. |
| **Weight** | (WeightedRandom strategy only.) The relative weight of this entry. A weight of `2` makes this entry twice as likely to be chosen as an entry with weight `1`. |
| **Start Line Index** | When this entry is jumped to via a branch, the conversation starts at this line index rather than line 0. Set to `0` for the default start. |
| **Tags** | Optional string tags you can inspect from custom logic or condition checks. WitWeaver™ does not use these internally; they are provided for your own systems. |

:::tip
Always assign meaningful **Alias** names to your entries (e.g., `"confrontation"`, `"peaceful_resolution"`, `"greeting_day1"`). These are the strings you reference in YAML choice targets and branching lines. Blank or generic aliases make branching harder to maintain as your project grows.
:::

---

## Branching Into a Container

When a dialogue line's `LineContinuationMode` is set to `ContainerBranch`, WitWeaver needs two pieces of information:

1. **Target Container**: the `ConversationContainer` asset to jump into.
2. **Target Alias Or Name**: a string that must match the **Alias** of an entry in that container.

At runtime, the container resolves the alias to the corresponding `WitWeaverConversationData` and starts it at the entry's configured `Start Line Index`.

**Example YAML branching (choice-driven):**

```yaml
- CharacterID: Player
  LineContinuationMode: PlayerChoice
  Choices:
    - Text: "I want to fight."
      TargetContainerAlias: "confrontation"
    - Text: "Let's talk this out."
      TargetContainerAlias: "peaceful_resolution"
```

In this case the runner expects a `ConversationContainer` with entries aliased `"confrontation"` and `"peaceful_resolution"`.

---

## GUID-Based Lookups

`ConversationContainer` exposes several methods for looking up entries by identity rather than by inspector position:

| Method | Returns | Description |
|---|---|---|
| `GetByGuid(string guid)` | `ContainerEntry` | Returns the entry whose `ConversationData` has the matching `ConversationGuid`. Returns `null` if not found. |
| `IndexOf(WitWeaverConversationData data)` | `int` | Returns the zero-based index of the entry holding the given data asset. Returns `-1` if not found. |
| `IndexOfGuid(string guid)` | `int` | Returns the zero-based index of the entry whose conversation data has the given GUID. Returns `-1` if not found. |

These methods are used internally by the save system when restoring the active conversation after a load. The save system stores a GUID rather than an index, so it can survive entry reordering in the inspector between sessions.

---

## Relationship to ContainerInput

When the WitWeaver component's **Input** field is set to `ContainerInput`, it points at a `ConversationContainer`. Every time `PlayConversation()` is called, the runner asks the container to resolve the next conversation according to its configured strategy and plays the result.

You can also override the start alias or loop behavior on the `ContainerInput` itself, giving per-runner control without modifying the shared container asset.

See [Input Modes](input-modes) for the full breakdown of `ContainerInput`.
