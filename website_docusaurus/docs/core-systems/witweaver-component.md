---
sidebar_position: 1
title: ConvoCore Component
---

# ConvoCore Component

The `ConvoCore` MonoBehaviour is the central piece of the runtime system. Attach it to a GameObject in your scene and it manages all conversation playback: loading dialogue data, advancing lines, running actions, firing events, and coordinating with the UI.

---

## Setting It Up

1. Create an empty GameObject (or use an existing manager object).
2. Add the **ConvoCore** component via *Add Component → ConvoCore*.
3. Set the **Input** field to determine what plays when `PlayConversation()` is called with no argument. See [Input Modes](input-modes) for the available types.
4. Optionally assign a **Conversation UI**, a reference to a `ConvoCoreUIFoundation` component in your scene.

---

## Inspector Fields

### Input

A `[SerializeReference]` field that accepts either a `SingleConversationInput` or a `ContainerInput`. This is the "default" conversation source. When you call `PlayConversation()` with no arguments, ConvoCore asks the configured input to resolve which conversation to start.

You can also pass a `ConvoCoreConversationData` directly to `PlayConversation(data)` to bypass this field entirely.

See [Input Modes](input-modes) for a full breakdown of both types.

### Conversation UI

A reference to a `ConvoCoreUIFoundation` component. This is the UI layer ConvoCore writes dialogue to. If this field is `null`, ConvoCore still runs the full conversation (all actions fire, all events fire, all state transitions happen), but nothing appears on screen. This can be intentional for headless or automated testing scenarios.

---

## Methods

| Method | Description |
|---|---|
| `PlayConversation()` | Starts a conversation using the configured Input. |
| `PlayConversation(ConvoCoreConversationData data)` | Starts a specific conversation directly, ignoring the Input field. |
| `StartConversation()` | No-argument alias for `PlayConversation()`. Use this as a UnityEvent target. |
| `PauseConversation()` | Pauses the running conversation. Fires `PausedConversation`. |
| `ResumeConversation()` | Resumes from the paused state. |
| `StopConversation()` | Immediately ends the conversation without completing it. Fires `EndedConversation`. |
| `ReverseOneLine()` | Steps back one dialogue line, executing each reversed line action in reverse order. |
| `UpdateUIForLanguage(string languageCode)` | Re-renders the current dialogue line in the given language code after a runtime language switch. |

:::tip
Use `StartConversation()` rather than `PlayConversation()` as your UnityEvent target. Unity’s event system requires callbacks with no parameters when wiring events in the inspector. `StartConversation()` is that no-parameter overload.
:::

:::warning
`StopConversation()` fires `EndedConversation`, **not** `CompletedConversation`. `CompletedConversation` fires only when the last line of a conversation finishes naturally and no more lines remain. If you stop early, listen to `EndedConversation` for cleanup rather than relying on `CompletedConversation`.
:::

---

## Events

ConvoCore exposes two categories of events: **UnityEvents** (wired in the inspector or via `AddListener` in code) and **C# Actions** (subscribed in code only, used for tighter integration with other runtime systems such as the save system).

### UnityEvents

| Event | When it fires |
|---|---|
| `StartedConversation` | When any conversation begins playing. |
| `PausedConversation` | When `PauseConversation()` is called. |
| `EndedConversation` | When `StopConversation()` is called (early termination, not completion). |
| `CompletedConversation` | When the last dialogue line finishes and no further lines remain. |

:::note
UnityEvents can be wired in the inspector by dragging a method onto the event field, or subscribed in code:

```csharp
_runner.CompletedConversation.AddListener(OnConversationComplete);
```

Unsubscribe when your listener object is destroyed to avoid null-reference exceptions:

```csharp
_runner.CompletedConversation.RemoveListener(OnConversationComplete);
```
:::

### C# Actions

| Event | Signature | When it fires |
|---|---|---|
| `OnConversationStarted` | `Action<ConvoCoreConversationData>` | Fires when a conversation starts. Carries the data asset. |
| `OnConversationEnded` | `Action<ConvoCoreConversationData>` | Fires when a conversation ends for any reason (stop or completion). |
| `OnLineStarted` | `Action<string>` | Fires at the start of each dialogue line. The string argument is the line’s ID. |
| `OnLineCompleted` | `Action<string>` | Fires after each dialogue line finishes (after all after-actions have run). |
| `OnChoiceMade` | `Action<int>` | Fires when the player selects a choice. The int argument is the zero-based choice index. |

Subscribe and unsubscribe like any C# event:

```csharp
private void OnEnable()
{
    _runner.OnLineStarted += HandleLineStarted;
    _runner.OnChoiceMade += HandleChoiceMade;
}

private void OnDisable()
{
    _runner.OnLineStarted -= HandleLineStarted;
    _runner.OnChoiceMade -= HandleChoiceMade;
}

private void HandleLineStarted(string lineId)
{
    Debug.Log($"Line started: {lineId}");
}

private void HandleChoiceMade(int choiceIndex)
{
    Debug.Log($"Player chose option {choiceIndex}");
}
```

:::warning
**Always unsubscribe from C# events in `OnDisable` or `OnDestroy`.**

Subscribing in `OnEnable` without a matching `OnDisable` unsubscription causes the runner to hold a reference to your destroyed component. When the event next fires, Unity throws a `MissingReferenceException`.

**Do not use anonymous delegates as event listeners.** A lambda like `_runner.OnLineStarted += (id) => DoSomething();` creates a delegate you can never unsubscribe; use a named method instead. See [Event Subscription Safety](conversation-state#event-subscription-safety) for the full explanation.
:::

---

## The `{PlayerName}` Placeholder

Write `{PlayerName}` anywhere in a YAML dialogue string and ConvoCore will substitute it at runtime with the `CharacterName` of the character whose profile has `IsPlayerCharacter` checked. No additional code is required - the substitution happens automatically during line rendering.

**Example YAML:**

```yaml
- CharacterID: Merchant
  LocalizedDialogue:
    EN: "Welcome back, {PlayerName}. Looking for supplies?"
```

If your player character is named `"Aria"`, the rendered line becomes: *"Welcome back, Aria. Looking for supplies?"*

Only one character profile should have `IsPlayerCharacter` checked. If multiple profiles have it checked, the first one found is used.

---

## IConvoStartContextProvider

When `PlayConversation()` is called, ConvoCore performs a `GetComponent<IConvoStartContextProvider>()` on its own GameObject. If a component implementing that interface is present (for example, `ConvoCoreConversationSaveManager`), ConvoCore calls it to retrieve a `ConvoStartContext` before the conversation begins.

The `ConvoStartContext` can specify:

- **Where to start** - a specific line index rather than line 0.
- **Which lines to mark as visited** - a set of line IDs that are treated as already-seen (used by the save system to restore visited-line state).

This integration is entirely transparent - the ConvoCore component itself does not need to know anything about the save system. You do not need to change any code on ConvoCore to activate save-system integration. Just add `ConvoCoreConversationSaveManager` to the same GameObject.

---

## Properties

| Property | Type | Description |
|---|---|---|
| `CurrentDialogueState` | `ConversationState` | The current state of the conversation runner. See [Conversation State](conversation-state). |
| `CanReverseOneLine` | `bool` | `true` if the history stack has at least one line that can be reversed to. |

---

:::info[For Advanced Users]
The core execution loop is the `ExecuteDialogueSequence()` coroutine. It iterates through `DialogueLines` starting from the resolved start index, and for each line it:

1. Runs all **before-actions** (`BaseDialogueLineAction` assets assigned to that line, in order).
2. Calls `UpdateDialogueUI()` on the assigned `ConvoCoreUIFoundation`, which triggers rendering.
3. Waits for line continuation (input, timer, or immediate, depending on `LineContinuationMode`).
4. Runs all **after-actions**.
5. Fires `OnLineCompleted`.
6. Advances to the next line (or branches, if the line’s `LineContinuationMode` is a branch type).

The runner maintains a `_lineActionHistory` list. Each time a line is executed, its instantiated before-actions are pushed to the history stack. When `ReverseOneLine()` is called, those actions are retrieved and `ExecuteOnReversedLineAction()` is called on each one in reverse order, allowing actions to undo any state changes they made.
:::