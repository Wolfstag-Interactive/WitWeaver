---
sidebar_position: 6
title: Player Choices
---

# Player Choices

When a dialogue line's continuation mode is set to `PlayerChoice`, WitWeaver pauses playback, hands a list of options to your UI, and waits for the player to make a selection. This page covers how to configure choice lines, what each field does, how to implement the UI coroutine that presents the options, and the edge cases you need to handle.

---

## Overview

A `PlayerChoice` line is a pause point in the conversation. Rather than automatically advancing to the next line, WitWeaver:

1. Collects the list of `ChoiceOption` entries configured on that line.
2. Resolves the localized label for each option in the current language.
3. Optionally appends a "Go Back" option if `AllowGoBack` is enabled.
4. Calls `PresentChoices()` on your active UI component.
5. Waits until your UI writes a selection into the `ChoiceResult` object.
6. Branches to the conversation associated with the selected option.

---

## Setting up a choice line in the Inspector

1. Open a `WitWeaverConversationData` asset in the Unity Inspector.
2. Find the line you want to act as a choice point.
3. Set its **Continuation Mode** to `PlayerChoice`.
4. A **Choice Options** list will appear. Add one entry per choice.
5. Optionally enable **Allow Go Back** to let the player step back to the previous line.

---

## ChoiceOption fields

Each entry in the **Choice Options** list has the following fields:

| Field | Required | Description |
|---|---|---|
| **Labels** | Yes | A localized map of language codes to display strings. Uses the same format as `LocalizedDialogue` in YAML - one entry per supported language. |
| **Target Container** | Yes | The `ConversationContainer` asset that holds the conversation to branch into when this option is selected. |
| **Target Alias Or Name** | Yes | The alias or name of the entry inside the target container to jump to when this option is selected. |
| **Push Return Point** | No | If checked, WitWeaver saves the current position onto the return stack before branching. When the branched conversation ends, it returns to the line after the choice line and resumes. |

---

## AllowGoBack

Each choice-bearing line has an **Allow Go Back** toggle. When it is enabled, WitWeaver automatically appends an extra option to the end of the choices list at runtime. Its label is `"← Go Back"` (or a localized equivalent if you configure one in `WitWeaverSettings`). Selecting it calls `ReverseOneLine()` internally, which steps the conversation back to the previous line and replays it.

Use `AllowGoBack` on choices where the player might want to re-read the preceding line before committing to an answer, for example a character asking a question that the player may not have fully read yet.

:::warning
`AllowGoBack` is not a full undo mechanism. It only moves back one line and does not restore any side effects that fired during that line (such as custom dialogue actions that triggered game events). Use it only for re-reading, not for reversing meaningful in-game state changes.
:::

---

## Implementing PresentChoices in your UI

Your UI class inherits from `WitWeaverUIFoundation`. To display choices, override the `PresentChoices` coroutine:

```csharp
protected override IEnumerator PresentChoices(
    List<ChoiceOption> options,
    List<string> localizedLabels,
    ChoiceResult result)
{
    // Spawn a button for each label
    for (int i = 0; i < localizedLabels.Count; i++)
    {
        int capturedIndex = i; // Capture the loop variable for the closure
        Button btn = Instantiate(_choiceButtonPrefab, _choiceContainer);
        btn.GetComponentInChildren<TMP_Text>().text = localizedLabels[i];
        btn.onClick.AddListener(() => result.SelectedIndex = capturedIndex);
    }

    // Wait until a valid choice is made
    yield return new WaitUntil(() => result.SelectedIndex >= 0);

    // Clean up buttons
    foreach (Transform child in _choiceContainer)
        Destroy(child.gameObject);
}
```

### Parameters

| Parameter | Type | Description |
|---|---|---|
| `options` | `List<ChoiceOption>` | The raw choice data from the Conversation Data asset. Access `Target Container`, `Target Alias Or Name`, and `Push Return Point` here if your UI needs them (e.g., to display branch previews or icons). |
| `localizedLabels` | `List<string>` | The already-localized display strings for the current language, in the same order as `options`. Use these for button text; do not re-localize manually. |
| `result` | `ChoiceResult` | Write the player's 0-based selection index to `result.SelectedIndex` when a choice is made. WitWeaver reads this value when the coroutine completes. |

:::note
A coroutine is a function that can pause its execution and resume later without blocking the rest of the game. The line `yield return new WaitUntil(() => result.SelectedIndex >= 0)` means "pause here and check this condition every frame. When it becomes true (meaning a button was clicked and wrote a valid index), continue executing." This pattern lets your UI remain responsive while WitWeaver waits for input.
:::

:::warning
`result.SelectedIndex` starts at `-1`, which means "no selection made yet." Your coroutine must not return until `result.SelectedIndex` is a valid, non-negative index. If your coroutine returns early (for example, due to a coding mistake that skips the `WaitUntil`), WitWeaver will attempt to branch using index `-1` and log an error. Always ensure the coroutine blocks until the player makes a genuine selection.
:::

### Capturing the loop variable

The line `int capturedIndex = i;` inside the loop is important. Without it, every button's `onClick` closure would capture the same variable `i`, which by the time any button is clicked will equal `localizedLabels.Count` (one past the last valid index). Capturing `i` into a separate `capturedIndex` variable for each iteration gives each button its own independent copy of the correct index.

---

## Handling the case of no choices

If a line is set to `PlayerChoice` but its **Choice Options** list is empty and `AllowGoBack` is false, WitWeaver has no valid options to present. In this case it logs a warning to the Unity Console and automatically advances to the next line as if the continuation mode were `Continue`. The `PresentChoices` coroutine is not called.

This behavior allows you to stub out choice lines during development: leave the options list empty, continue building other content, and fill in the choices later without breaking playback.

:::tip
While developing, check the Unity Console for warnings about empty choice lines. A warning here during play mode usually means a choice line you intended to configure is missing its options; it will not cause a crash but it will skip the choice entirely.
:::

---

## Full example: a three-option choice

The following example shows a complete choice setup for a scene where the player must decide how to respond to a merchant.

**YAML (the dialogue leading up to the choice):**
```yaml
MerchantNegotiation:
  - CharacterID: "Merchant"
    LocalizedDialogue:
      EN: "I'll give you three gold pieces for the amulet. What do you say?"
  - CharacterID: "Player"
    LocalizedDialogue:
      EN: ""
```

The `Player` line with the empty dialogue string is the choice line. Its actual display text is defined by the `ChoiceOption` labels set in the Inspector, not by `LocalizedDialogue`. Setting an empty string here keeps the YAML clean while the Continuation Mode and options are configured on the asset.

**Inspector configuration for the choice line:**

- Continuation Mode: `PlayerChoice`
- Allow Go Back: enabled
- Choice Options:
  - Option 0: Labels `EN: "Deal. Three gold it is."` → Target: `MerchantNegotiation/AcceptBranch`
  - Option 1: Labels `EN: "I want five gold."` → Target: `MerchantNegotiation/CounterBranch`, Push Return Point: enabled
  - Option 2: Labels `EN: "Forget it. I'm keeping it."` → Target: `MerchantNegotiation/RefuseBranch`

With `AllowGoBack` enabled, a fourth option "← Go Back" will appear automatically at runtime, letting the player re-read the merchant's offer before choosing.

---

:::info[For Advanced Users]
`ChoiceResult` is a plain class with a single `int SelectedIndex` field initialized to `-1`. WitWeaver reads it synchronously after your `PresentChoices` coroutine finishes. There is no thread-safety concern because Unity coroutines run on the main thread. If you need to handle animated transitions, tween-out effects, or audio cues after a choice is made but before WitWeaver branches, perform all of that work inside `PresentChoices` before the coroutine returns. WitWeaver will not branch until the coroutine completes.
:::
