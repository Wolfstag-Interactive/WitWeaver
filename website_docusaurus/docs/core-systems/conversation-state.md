---
sidebar_position: 5
title: Conversation State
---

# Conversation State

WitWeaver™ tracks the lifecycle of a running conversation through the `ConversationState` enum, accessible at any time via the `CurrentDialogueState` property. Understanding state transitions is important for writing reliable UI code, integrating with other game systems, and avoiding common bugs like double-starting a conversation.

---

## The ConversationState Enum

| State | Description |
|---|---|
| `Inactive` | No conversation is running. The runner is idle and ready to accept a new `PlayConversation()` call. |
| `Active` | A conversation is currently playing and advancing through dialogue lines. |
| `Paused` | The conversation is running but waiting. A call to `PauseConversation()` was made. Lines do not advance until `ResumeConversation()` is called. |
| `Completed` | The conversation reached its last line naturally and finished. The runner transitions back to `Inactive` after cleanup completes. |

---

## State Transitions

The following diagram shows all valid state transitions and the method or condition that triggers each one:

```
Inactive ──── PlayConversation() ────────────────▶ Active
                                                      │
Active ──── PauseConversation() ────────────▶ Paused │
                                                      │
Paused ──── ResumeConversation() ───────────▶ Active  │
                                                      │
Active ──── last line reached ────────────▶ Completed ──▶ Inactive
                                                      │
Active ──── StopConversation() ────────────▶ Inactive │
                                                      │
Paused ──── StopConversation() ────────────▶ Inactive
```

Key rules:

- `StopConversation()` always returns to `Inactive` and fires `EndedConversation`.
- Reaching the last line transitions to `Completed` (briefly), then to `Inactive`, and fires `CompletedConversation`.
- `PauseConversation()` and `ResumeConversation()` only affect the `Active` and `Paused` states respectively; calling them from other states has no effect.

---

## Checking State in Code

Read `CurrentDialogueState` before starting a conversation to avoid restarting one that is already running:

```csharp
public void TryStartConversation()
{
    if (_runner.CurrentDialogueState == ConversationState.Inactive)
    {
        _runner.StartConversation();
    }
    else
    {
        Debug.Log("A conversation is already in progress.");
    }
}
```

You can also branch on the full set of states:

```csharp
switch (_runner.CurrentDialogueState)
{
    case ConversationState.Inactive:
        _startButton.interactable = true;
        _pauseButton.interactable = false;
        break;

    case ConversationState.Active:
        _startButton.interactable = false;
        _pauseButton.interactable = true;
        break;

    case ConversationState.Paused:
        _startButton.interactable = false;
        _pauseButton.interactable = true; // shows "Resume" label
        break;

    case ConversationState.Completed:
        // transitional - handle cleanup in CompletedConversation event instead
        break;
}
```

:::tip
Subscribe to events instead of polling `CurrentDialogueState` every frame. Use `StartedConversation`, `CompletedConversation`, `PausedConversation`, and `EndedConversation` for event-driven state management. Polling is more error-prone and wastes performance. Reserve direct state checks for one-off gate conditions (like the `TryStartConversation` example above).
:::

---

## CanReverseOneLine

The `CanReverseOneLine` property returns `true` when the conversation history stack has at least one line that can be stepped back to. Use it to drive the interactability of a "go back" button in your UI:

```csharp
private void Update()
{
    _backButton.interactable = _runner.CanReverseOneLine;
}
```

Or, more efficiently, update the button state only when a line completes or the conversation state changes:

```csharp
private void OnEnable()
{
    _runner.OnLineCompleted += _ => RefreshBackButton();
    _runner.StartedConversation.AddListener(RefreshBackButton);
    _runner.CompletedConversation.AddListener(RefreshBackButton);
    _runner.EndedConversation.AddListener(RefreshBackButton);
}

private void RefreshBackButton()
{
    _backButton.interactable = _runner.CanReverseOneLine;
}
```

`CanReverseOneLine` is `false` when:

- The conversation is at the very first line (nothing to go back to).
- No conversation is running (`Inactive` or `Completed`).
- The runner’s history stack has been cleared.

Calling `ReverseOneLine()` when `CanReverseOneLine` is `false` does nothing; it will not throw an error, but it will not move anywhere.

---

## Common Mistakes

:::warning
Calling `PlayConversation()` when `CurrentDialogueState` is already `Active` will **restart the conversation from the beginning** without a clean stop. The `StopConversation` event will not fire, and any state or UI tied to the previous conversation may not clean up correctly.

Always check state before starting, or call `StopConversation()` first if you intentionally need to switch conversations mid-play:

```csharp
if (_runner.CurrentDialogueState != ConversationState.Inactive)
    _runner.StopConversation();

_runner.PlayConversation(newConversation);
```
:::

:::warning
Do not confuse `EndedConversation` with `CompletedConversation`. They are distinct events with distinct meanings:

- `EndedConversation` fires when `StopConversation()` is called (an **early, manual termination**).
- `CompletedConversation` fires when the conversation **finishes naturally** at its last line.

If you attach cleanup logic (disabling UI, re-enabling player movement, etc.) only to `CompletedConversation`, that cleanup will not run when the conversation is stopped early. Attach it to both events, or use a shared method:

```csharp
private void OnEnable()
{
    _runner.CompletedConversation.AddListener(OnConversationOver);
    _runner.EndedConversation.AddListener(OnConversationOver);
}

private void OnConversationOver()
{
    _playerController.enabled = true;
    _dialoguePanel.SetActive(false);
}
```
:::

---

## Event Subscription Safety

:::warning
**Always unsubscribe from C# events when your component is disabled or destroyed.**

If a `MonoBehaviour` subscribes to a WitWeaver C# event in `OnEnable` but never removes the listener in `OnDisable`, the runner continues to hold a reference to your object even after it is destroyed. When the event fires, Unity will throw a `MissingReferenceException` or `NullReferenceException`.

The correct pattern:

```csharp
private void OnEnable()
{
    _runner.OnLineStarted += HandleLineStarted;
    _runner.CompletedConversation.AddListener(OnConversationComplete);
}

private void OnDisable()
{
    _runner.OnLineStarted -= HandleLineStarted;
    _runner.CompletedConversation.RemoveListener(OnConversationComplete);
}
```

**Avoid anonymous delegates as event listeners.** If you write:

```csharp
// ❌ Cannot be unsubscribed - do not use this pattern
_runner.OnLineStarted += (id) => HandleLineStarted(id);
```

…you create a delegate object that you cannot reference later. There is no way to unsubscribe it. The runner will hold a dangling reference to your destroyed object indefinitely, causing errors.

Always use a named method so you can both subscribe and unsubscribe:

```csharp
// ✅ Correct - named method can be unsubscribed
_runner.OnLineStarted += HandleLineStarted;
// later in OnDisable:
_runner.OnLineStarted -= HandleLineStarted;
```
:::