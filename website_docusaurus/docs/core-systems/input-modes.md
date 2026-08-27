---
sidebar_position: 4
title: Input Modes
---

# Input Modes

The **Input** field on the WitWeaver component determines what conversation (or group of conversations) plays when you call `PlayConversation()` with no arguments. Instead of a fixed reference to one conversation, this field accepts an `IWitWeaverInput` implementation, a small object that resolves a conversation at the moment `PlayConversation()` is called.

---

## What Is [SerializeReference]?

:::note
The Input field uses Unity's `[SerializeReference]` attribute. This allows the field to hold different types of objects, selected from a dropdown in the inspector. Click the small dropdown arrow or right-click the Input field in the inspector and choose the type you want. The inspector will update to show the fields for that type.

This is different from a standard object reference field, which can only hold one specific type.
:::

---

## SingleConversationInput

The simplest input mode. Points to one specific `WitWeaverConversationData` asset. Every call to `PlayConversation()` plays that same conversation.

### Fields

| Field | Description |
|---|---|
| **Conversation** | The `WitWeaverConversationData` asset to play. |

### When to use it

Use `SingleConversationInput` when a WitWeaver component is dedicated to one conversation that never changes - for example, an NPC with a single greeting, a door that always plays the same "locked" explanation, or a quest-giver whose dialogue is replaced by swapping the entire `WitWeaverConversationData` reference in code.

### Overriding at runtime

You can bypass the configured input entirely by calling `PlayConversation(data)` with a specific asset:

```csharp
// Ignore the Input field and play a specific conversation:
_runner.PlayConversation(mySpecificConversation);
```

You can also update the input's conversation reference at runtime:

```csharp
var input = _runner.GetInput<SingleConversationInput>();
if (input != null)
    input.Conversation = anotherConversation;
```

---

## ContainerInput

Points to a `ConversationContainer` and delegates conversation selection to the container's configured strategy (Sequential, Random, WeightedRandom, etc.). Each call to `PlayConversation()` asks the container which conversation to play next.

### Fields

| Field | Description |
|---|---|
| **Container** | The `ConversationContainer` asset to draw conversations from. |
| **Start Alias** | Optional. If set, the container jumps to the entry with this alias instead of letting the strategy pick. Useful for starting a container at a specific known point. |
| **Loop Override** | Override the container's own loop setting for this particular runner. Leave unset to use the container's default. |

### When to use it

Use `ContainerInput` when:

- An NPC cycles through several different greetings (Sequential or Random).
- You want weighted dialogue pools - some lines are rarer than others (WeightedRandom).
- A single container asset is shared across many NPCs of the same type, each with their own runner.
- A scripted sequence needs to play multiple conversations in order with delays (Playlist mode on the container).

See [Conversation Container](conversation-container) for the full breakdown of container modes and selection strategies.

---

## Calling PlayConversation vs StartConversation

There are three ways to trigger conversation playback:

| Call | What it does |
|---|---|
| `PlayConversation()` | Uses the configured Input to resolve which conversation to play. |
| `PlayConversation(WitWeaverConversationData data)` | Ignores the Input field entirely and plays the given conversation directly. |
| `StartConversation()` | A no-argument alias for `PlayConversation()`. Use this as a UnityEvent target. |

The reason `StartConversation()` exists is Unity's event system constraint: methods wired in the inspector via UnityEvent must have no parameters. `PlayConversation()` has an overload with a parameter, which confuses the inspector dropdown. `StartConversation()` is the unambiguous, no-argument entry point suitable for drag-and-drop event wiring.

```csharp
// In code - these are equivalent:
_runner.PlayConversation();
_runner.StartConversation();

// In the inspector: wire to StartConversation(), not PlayConversation()
```

---

:::info[For Advanced Users]
You can implement `IWitWeaverInput` yourself to create fully custom conversation selection logic: procedural selection driven by game state, condition checks, external databases, probability tables, or anything else.

The interface has one method:

```csharp
public interface IWitWeaverInput
{
    WitWeaverConversationData Resolve(WitWeaver runner);
}
```

Implement it as a `[Serializable]` class (not a MonoBehaviour) and it will appear automatically in the `[SerializeReference]` dropdown on the WitWeaver inspector:

```csharp
[Serializable]
public class QuestStateInput : IWitWeaverInput
{
    public WitWeaverConversationData DefaultConversation;
    public WitWeaverConversationData QuestActiveConversation;
    public WitWeaverConversationData QuestCompleteConversation;

    public WitWeaverConversationData Resolve(WitWeaver runner)
    {
        if (QuestManager.IsComplete("main_quest"))
            return QuestCompleteConversation;
        if (QuestManager.IsActive("main_quest"))
            return QuestActiveConversation;
        return DefaultConversation;
    }
}
```

The `runner` parameter gives you access to the WitWeaver component itself (and therefore its GameObject, scene, and any other components) at the moment of resolution.
:::
