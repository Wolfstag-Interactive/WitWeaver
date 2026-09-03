---
sidebar_position: 3
title: Conversation Save Manager
---

# Conversation Save Manager

`WitWeaverConversationSaveManager` is a MonoBehaviour that sits alongside your `WitWeaver` component on a scene GameObject. It tracks progress for one conversation (or one entry in a `ConversationContainer`) and feeds the resume context back into the runner automatically when the conversation starts.

---

## How it connects to WitWeaver

You do not need to write any glue code. `WitWeaver.PlayConversation()` calls `GetComponent<IWitWeaverStartContextProvider>()` on its own GameObject before starting. `WitWeaverConversationSaveManager` implements that interface and returns a `WitWeaverStartContext` describing what to do. If no provider is present, the runner starts fresh as usual - the interface is optional.

:::note
`IWitWeaverStartContextProvider` lives in the WitWeaver runtime assembly (not the save system assembly) so the core runner can reference it without creating a circular dependency. The save system assembly implements the interface. You never need to call the interface directly; it is wired up automatically.
:::

---

## Setup

1. Select the GameObject that has your `WitWeaver` component.
2. Add Component → `WitWeaverConversationSaveManager`.
3. In the inspector, assign your `WitWeaverSaveManager` and `WitWeaverVariableStore` assets.
4. Configure the conversation source (see below).
5. Choose your start mode and auto-commit flags.

That is all. On the first run, `WitWeaver` starts fresh and the saver records progress. On subsequent runs, it restores the saved state before the runner starts.

---

## Conversation source

Pick **one** of the two modes:

**Direct Conversation mode**: track a single `WitWeaverConversationData` asset:

| Field | Description |
|---|---|
| **Direct Conversation** | Assign the `WitWeaverConversationData` asset you want to track. |

**Container mode**: track one entry inside a `ConversationContainer`:

| Field | Description |
|---|---|
| **Conversation Container** | Assign the `ConversationContainer` asset. |
| **Active Conversation Index** | The index of the currently active conversation within the container. Update this at runtime when switching between conversations in the container. |

:::tip
Use Direct Conversation mode for standalone NPCs. Use Container mode when one GameObject cycles through multiple conversations (e.g. an NPC with different conversations per quest stage). Update **Active Conversation Index** before calling `PlayConversation()` to make sure the saver tracks the right entry.
:::

---

## Inspector fields: references

| Field | Description |
|---|---|
| **Save Manager** | The `WitWeaverSaveManager` ScriptableObject asset. |
| **Variable Store** | The `WitWeaverVariableStore` ScriptableObject asset. |

---

## Inspector fields: start mode

**Default Start Mode** controls what happens when a saved snapshot exists for this conversation:

| Mode | Behaviour |
|---|---|
| `Fresh` | Ignore the snapshot. Start from line 0 every time. Visited lines and variables from the snapshot are not restored. Useful for conversations that should always replay. |
| `Resume` | Start from the `ActiveLineId` stored in the snapshot. Restore all `VisitedLineIds` and Conversation-scoped variables. This is the standard "continue where you left off" behaviour. |
| `Restart` | Start from line 0, but restore Conversation-scoped variables from the snapshot. Useful for conversations that should re-run the full dialogue, but where gameplay state from previous runs should be preserved. |

**Restore Behavior** controls when and how the restore decision is made:

| Value | Behaviour |
|---|---|
| `ResumeFromActiveLine` | Applies the default start mode automatically in `OnEnable` or `Start` (depending on auto-restore flags). No code required. |
| `AskViaEvent` | Fires `OnRestoreDecisionRequired` with the saved snapshot. Your code inspects the snapshot and calls either `ResumeFromSnapshot()` or ignores it to start fresh. Use this to show a "Continue from save?" prompt. |

---

## Inspector fields: auto-commit flags

These flags control when `CommitSnapshot()` is called automatically. All default to `false`.

| Flag | Trigger |
|---|---|
| **Auto Commit On Start** | Fired when `WitWeaver.StartedConversation` fires. Commits the snapshot at conversation start (useful for recording that the player entered this conversation). |
| **Auto Commit On End** | Fired when `WitWeaver.EndedConversation` fires. Commits the snapshot when the conversation stops (paused, ended, or completed). |
| **Auto Commit On Line Complete** | Fired when `WitWeaver.OnLineCompleted` fires. Commits after every line. Most granular option - highest I/O frequency. |
| **Auto Commit On Choice Made** | Fired when `WitWeaver.OnChoiceMade` fires. Commits whenever the player selects a branch choice. |

:::tip
For most projects, enable only **Auto Commit On End**. This records progress once per conversation session and minimises disk writes. Enable **Auto Commit On Choice Made** if your conversations have long branching paths and you want to resume mid-branch.
:::

---

## Inspector fields: auto-restore flags

| Flag | Trigger |
|---|---|
| **Auto Restore On Awake** | Calls `TryAutoRestore()` in `Awake()`. Use this if conversation objects are active at scene load and you want restore to happen as early as possible. |
| **Auto Restore On Start** | Calls `TryAutoRestore()` in `Start()`. Safer default - gives other Awake() callbacks time to run first (e.g. the save manager bootstrapper). |

:::warning
If your bootstrapper initialises `WitWeaverSaveManager` in `Awake()` and `WitWeaverConversationSaveManager` also restores in `Awake()`, the order of execution matters. Use **Auto Restore On Start** unless you have configured Script Execution Order to guarantee the bootstrapper runs first.
:::

---

## How WitWeaver uses the start context

When `PlayConversation()` is called, WitWeaver™ invokes `IWitWeaverStartContextProvider.GetStartContext()`. The saver returns a `WitWeaverStartContext` struct:

```csharp
public struct WitWeaverStartContext
{
    public WitWeaverStartMode Mode;    // Fresh, Resume, or Restart
    public string StartLineId;     // LineID to begin from (Resume mode only)
    public HashSet<string> VisitedLineIds; // Lines already marked as visited
}
```

- **Fresh**: runner starts from index 0, no visited lines marked.
- **Resume**: runner calls `BeginFromLine(StartLineId)` and `ApplyVisitedLines(VisitedLineIds)` before displaying the first line.
- **Restart**: runner starts from index 0, but `WitWeaverVariableStore` already has Conversation-scoped variables restored from the snapshot.

---

## Committing snapshots manually

```csharp
// Push the current conversation progress to WitWeaverSaveManager's registry.
// Does not write to disk - call WitWeaverSaveManager.Save() to persist.
_conversationSaver.CommitSnapshot();

// Read the current in-memory snapshot without committing it.
ConversationSnapshot snap = _conversationSaver.GetConversationSnapshot();
```

Calling `CommitSnapshot()` does not automatically trigger a disk write. The snapshot is registered with `WitWeaverSaveManager`, which assembles all registered snapshots into a `WitWeaverGameSnapshot` when `Save()` is called.

---

## AskViaEvent pattern

Set **Restore Behavior** to `AskViaEvent` to display a "Continue from save?" UI before the conversation starts.

```csharp
using WolfstagInteractive.WitWeaver.SaveSystem;
using UnityEngine;

public class ConversationResumeUI : MonoBehaviour
{
    [SerializeField] private WitWeaverConversationSaveManager _saver;
    [SerializeField] private WitWeaver _runner;
    [SerializeField] private GameObject _resumePanel;

    private void OnEnable()
    {
        _saver.OnRestoreDecisionRequired += HandleRestoreDecision;
    }

    private void OnDisable()
    {
        _saver.OnRestoreDecisionRequired -= HandleRestoreDecision;
    }

    private void HandleRestoreDecision(ConversationSnapshot snapshot)
    {
        // Show the panel and wire up the buttons for this decision
        _resumePanel.SetActive(true);

        _continueButton.onClick.RemoveAllListeners();
        _continueButton.onClick.AddListener(() =>
        {
            _resumePanel.SetActive(false);
            _saver.ResumeFromSnapshot(snapshot);
            _runner.PlayConversation();
        });

        _newGameButton.onClick.RemoveAllListeners();
        _newGameButton.onClick.AddListener(() =>
        {
            _resumePanel.SetActive(false);
            // Do not call ResumeFromSnapshot - runner will start fresh
            _runner.PlayConversation();
        });
    }
}
```

:::warning
If you use `AskViaEvent`, do not call `_runner.PlayConversation()` until the player has responded to the prompt. Calling it before the handler fires will start the conversation before the restore decision is made.
:::

---

## Read-only state properties

These properties reflect the current in-memory state and update in real time during a running conversation. They are visible in the custom inspector during Play Mode.

| Property | Type | Description |
|---|---|---|
| `ActiveLineId` | `string` | LineID of the most recently displayed line. Empty string if the conversation has not started. |
| `IsComplete` | `bool` | `true` if the conversation has reached its end at least once. |
| `VisitedLinesCount` | `int` | Number of unique lines the player has seen. |
| `ConversationVariablesCount` | `int` | Number of Conversation-scoped variables currently registered. |
| `IsDirty` | `bool` | `true` if changes have been made since the last `CommitSnapshot()` call. |
| `LastCommitTime` | `DateTime` | UTC timestamp of the last successful `CommitSnapshot()` call. |

---

## Custom inspector

The `WitWeaverConversationSaveManager` inspector repaints at 0.1-second intervals during Play Mode so the read-only state properties update in real time without requiring manual inspector refreshes. The inspector layout adapts based on whether a snapshot exists; if no save data is present for the current conversation, the restore section is hidden.
