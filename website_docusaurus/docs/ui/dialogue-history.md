---
sidebar_position: 4
title: Dialogue History
---

# Dialogue History

## What Is Dialogue History?

`WitWeaverDialogueHistoryUI` is an optional component that maintains a rolling log of dialogue lines as a conversation plays. It pairs a **renderer** (which formats each entry as text or a prefab) with an **output** (which displays the formatted result on screen). Use it to build a chat-window style UI, an in-game transcript, a visual-novel message log, or an accessibility replay feature.

:::tip
Dialogue history is completely optional. Add it only if your UI design requires a scrollable transcript. Most standard dialogue boxes (name, text, and portrait) do not need it.
:::

---

## Adding It to the Scene

Add the `WitWeaverDialogueHistoryUI` component to any GameObject in the scene (typically the same `DialoguePanel` GameObject as your `WitWeaverUIFoundation` subclass, or a child of it).

**Inspector fields**:

| Field | Description |
|---|---|
| **Max Entries** | Maximum number of history lines to keep. When the limit is reached, the oldest entry is discarded before a new one is added. Set to `0` for unlimited (be careful with long conversations). |
| **Renderer Profile** | The renderer that controls how each history entry is formatted. Select from the built-in options or assign a custom renderer asset. |
| **Output** | The output component that displays the formatted entries (a `TMP_Text` for text output, or a `Transform` for prefab output). |

---

## Calling AddLine from Your UI

In your `WitWeaverUIFoundation` subclass, call `AddLine()` each time a line is displayed. The most natural place is inside `UpdateDialogueUI()`:

```csharp
[SerializeField] private WitWeaverDialogueHistoryUI _historyUI;

protected override void UpdateDialogueUI(
    DialogueLineInfo lineInfo,
    string localizedText,
    string speakerName,
    CharacterRepresentationBase representation,
    WitWeaverCharacterProfileBaseData primaryProfile)
{
    // Update the current line display as normal.
    _speakerNameText.text = speakerName;
    _dialogueText.text = localizedText;

    // Record the line in the history log.
    Color nameColor = primaryProfile != null
        ? primaryProfile.CharacterNameColor
        : Color.white;

    _historyUI.AddLine(speakerName, localizedText, nameColor);
}
```

`AddLine()` passes the data to the configured renderer, formats it, and sends the result to the configured output. You do not need to manage the list of entries directly.

---

## Built-in Renderers

### PlainTextHistoryRenderer

Formats each entry as plain text. The speaker name and dialogue are concatenated using a configurable separator.

**Output**: `"Narrator: Hello world."`

**When to use**: Simple log-style transcripts, debug displays, non-styled text outputs.

### RichTextHistoryRenderer

Wraps the speaker name in a TextMeshPro `<color>` tag using the character's name color. Produces rich text markup that TextMeshPro renders with coloring.

**Output**: `"<color=#FFD700>Town Guard</color>: Halt! State your business."`

**When to use**: Chat-window style UIs, visual novel message logs, any UI using `TMP_Text` with rich text enabled.

### PrefabHistoryRenderer

Instantiates a prefab for each history entry instead of generating text. The prefab receives the speaker name, dialogue text, and name color. The prefab's root component must implement `IWitWeaverHistoryEntry` (or have a child component that does).

**When to use**: Custom-styled history entries with icons, avatars, timestamps, or complex layouts that cannot be expressed as a text string.

**Setting up the history entry prefab**:

```csharp
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using WolfstagInteractive.WitWeaver.UI;

public class MyHistoryEntry : MonoBehaviour, IWitWeaverHistoryEntry
{
    [SerializeField] private TMP_Text _speakerLabel;
    [SerializeField] private TMP_Text _dialogueLabel;
    [SerializeField] private Image _avatarImage; // optional

    public void Populate(string speakerName, string dialogueText, Color nameColor)
    {
        _speakerLabel.text = speakerName;
        _speakerLabel.color = nameColor;
        _dialogueLabel.text = dialogueText;
    }
}
```

---

## Built-in Outputs

### TMPDialogueHistoryOutput

Writes all formatted history entries to a single `TMP_Text` component, separated by newlines. The text component grows with each entry.

**Combine with**: `PlainTextHistoryRenderer` or `RichTextHistoryRenderer`.

**Setup**: Add a `TMP_Text` component to a panel (typically inside a `ScrollRect` with vertical overflow). Add `TMPDialogueHistoryOutput` to the same or a parent GameObject and assign the `TMP_Text`.

:::tip
Place the `TMP_Text` inside a `ScrollRect` with a `ContentSizeFitter` set to **Preferred Size** (vertical). This allows the text to grow as entries accumulate and the scroll view to handle overflow automatically.
:::

### PrefabDialogueHistoryOutput

Parents instantiated prefab entries to a `Transform` (typically the content area of a `ScrollRect`).

**Combine with**: `PrefabHistoryRenderer`.

**Setup**: Create a `ScrollRect` with a content area that has a `VerticalLayoutGroup` and `ContentSizeFitter`. Assign the content `Transform` to the `PrefabDialogueHistoryOutput` component's target field. New entries are added as children of that transform in display order.

---

## Clearing History

Call `_historyUI.Clear()` at the start of each new conversation. Without clearing, lines from a previous conversation will still be visible in the history log when the next one begins.

The most reliable place to clear is inside `InitializeUI()`:

```csharp
protected override void InitializeUI(WitWeaver runner)
{
    _historyUI.Clear();
    _dialoguePanel.SetActive(true);
    _playerAdvanced = false;
}
```

`Clear()` removes all existing entries from the output and resets the internal list. For `TMPDialogueHistoryOutput`, it sets the `TMP_Text` text to empty. For `PrefabDialogueHistoryOutput`, it destroys all child GameObjects under the content transform.

:::note
Dialogue history tracks **display events**: each call to `AddLine()` appends an entry. It is completely independent of the save system. The save system tracks line progress using `LineID` values for restore purposes; clearing the history UI has no effect on save state, and loading a saved conversation does not automatically rebuild the history log. If you need the history to reflect replayed lines after a restore, call `AddLine()` for each replayed line in your restore logic.
:::

---

## Controlling Scroll Position

After adding an entry, you typically want the history view to scroll to the bottom so the latest line is visible. Call `ScrollToBottom()` on your `ScrollRect` after `AddLine()`:

```csharp
[SerializeField] private ScrollRect _historyScrollRect;

protected override void UpdateDialogueUI(...)
{
    // ... update current line display ...

    _historyUI.AddLine(speakerName, localizedText, nameColor);

    // Scroll to show the newest entry.
    // Canvas.ForceUpdateCanvases() ensures layout is updated before scrolling.
    Canvas.ForceUpdateCanvases();
    _historyScrollRect.verticalNormalizedPosition = 0f;
}
```

:::warning
`Canvas.ForceUpdateCanvases()` forces an immediate layout rebuild, which can be expensive if called every frame. Calling it only inside `UpdateDialogueUI()` (once per line) is acceptable. Do not call it in `Update()`.
:::

---

## Next Steps

| I want to… | Go here |
|---|---|
| Build the base UI that calls AddLine | [Building a Custom UI →](building-a-ui) |
| Understand the foundation methods your UI overrides | [UI Foundation →](ui-foundation) |
| Save and restore conversation progress | [Save System →](../save-system/save-system-overview) |
