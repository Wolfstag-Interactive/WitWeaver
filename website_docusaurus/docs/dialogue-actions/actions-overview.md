---
sidebar_position: 1
title: Actions Overview
---

# Actions Overview

Dialogue actions are the primary way to connect your dialogue to the rest of your game. They are `ScriptableObject` assets that run game logic in sync with individual lines - before the text appears, or after the player advances. Use them to trigger camera moves, spawn NPCs, play cutscenes, flip quest flags, update analytics, fade portraits in and out, or run any other code your game needs.

Because actions are assets rather than scene components, each one is reusable across dozens of conversations, configurable per-instance in the Inspector, and tracked in version control just like any other project file. A single `FadeInCharacter` action can be shared by every scene that uses that character; change the timing once and it updates everywhere.

:::tip
Dialogue actions are one of WitWeaver's most powerful features. Everything you see in the [built-in actions](built-in-actions) is built on the same `BaseDialogueLineAction` base class that your own actions use. If the built-in actions do not cover your needs, [creating a custom action](custom-actions) takes only a few minutes.
:::

---

## Before vs After Actions

Each `DialogueLineInfo` exposes two action lists:

| List | When it runs |
|---|---|
| **Actions Before Dialogue Line** | Runs before the line's text is displayed. Use this to set up a shot: show a character, move the camera, trigger a particle effect. The conversation waits for every before-action to complete before the UI renders the line. |
| **Actions After Dialogue Line** | Runs after the player advances past the line (i.e., after the input that dismisses the line is received). Use this to clean up: hide a character, trigger a quest update, play an outgoing transition. The runner waits for every after-action to complete before moving to the next line. |

:::note
An action is a **coroutine**, a function that can pause in the middle of its execution and resume later. This lets actions do things over time (fade an object in over 0.5 seconds, move a camera over 1.0 second) without freezing the rest of the game. Use `yield return new WaitForSeconds(duration)` to pause for a set amount of time, or `yield return null` to wait exactly one frame before continuing.
:::

The conversation runner does not proceed to the next step until the current action coroutine is finished. If you have five before-actions, each one completes in sequence before the text appears on screen.

---

## RunOnlyOncePerConversation

Every action asset exposes a `RunOnlyOncePerConversation` boolean field.

- When **disabled** (the default), the action executes every time that line is reached, including after a reversal.
- When **enabled**, WitWeaver records which lines have already triggered this action during the current conversation playthrough. If the player reverses past the line and reaches it again, the action is skipped on the second pass.

Use `RunOnlyOncePerConversation` for actions that have side effects that should not repeat: spawning a prop into the scene, triggering a quest flag, playing a one-shot cinematic.

---

## Reversal Behavior

WitWeaver supports stepping backwards through dialogue via `ReverseOneLine()`. When the player reverses, the runner undoes the **before-actions** of the line being left, not the after-actions, because those already ran before the current line began.

Reversal calls `ExecuteOnReversedLineAction()` on each before-action, in **reverse order** (last action first). This is the action's opportunity to undo whatever `ExecuteLineAction()` did: restore a position, hide a character that was revealed, destroy a spawned prop.

:::warning
If your action has irreversible side effects (playing a cinematic, spending currency, sending a network event), you have two options:

1. Implement a proper undo in `ExecuteOnReversedLineAction()` so the reversal path is clean.
2. Set `RunOnlyOncePerConversation = true` so the action does not re-run if the player reverses and re-enters the line.

The base implementation of `ExecuteOnReversedLineAction()` does nothing by default. If you do not override it, reversal silently skips any cleanup for your action's side effects.
:::

After-actions are never reversed. They ran after the player advanced past a line; reversing to that line does not undo them.

---

## Assigning Actions in the Inspector

Open a `WitWeaverConversationData` asset and select a dialogue line. You will see two lists:

- **Actions Before Dialogue Line**
- **Actions After Dialogue Line**

Click the **+** button on either list and drag an action asset from the Project panel into the slot. You can assign the same action asset to multiple lines; each execution gets its own instance at runtime, so shared assets do not pollute each other's state.

---

## Execution Order Within a List

Actions within a single list (before or after) run **sequentially**. The runner yields on each action's coroutine before starting the next. There is no parallelism within a list. If you need actions to run in parallel, wrap them in a custom action that starts multiple coroutines with `StartCoroutine`.

---

## The Action Lifecycle at Runtime

When WitWeaver executes a line, the full sequence is:

1. Instantiate all before-actions (via `ScriptableObject.Instantiate`, each gets a fresh copy).
2. Execute each before-action's `ExecuteLineAction()` coroutine in order. Wait for each to complete.
3. Render the localized dialogue text to the UI.
4. Wait for line advancement (player input or timer, depending on the line's `DialogueLineProgressionMethod`).
5. Instantiate all after-actions.
6. Execute each after-action's `ExecuteLineAction()` coroutine in order. Wait for each to complete.
7. Advance to the next line.

The instantiation step means that runtime-only state (like a cached `_originalColor` for a fade action) lives on the instance, not on the shared asset. The asset's serialized fields remain untouched between runs.
