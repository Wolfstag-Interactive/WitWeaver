---
sidebar_position: 3
title: Building a Custom UI
---

# Building a Custom UI

:::tip
WitWeaver™ includes a ready-made sample UI in the Samples package that demonstrates all of these patterns in a working scene. If you prefer a hands-on starting point over building from scratch, [import the Sample UI](sample-ui) first and read its code alongside this guide.
:::

This page walks through creating a complete, working dialogue UI for WitWeaver from scratch. By the end you will have a UI that displays the speaker's name and dialogue text, handles player input to advance lines, and presents branching choices.

---

## Prerequisites

- WitWeaver is installed and a `WitWeaverConversationData` asset exists with parsed dialogue lines.
- **TextMeshPro** is installed (Window → Package Manager → TextMeshPro). The code examples on this page use TMP - see the note below if you plan to use a different text system.
- A scene is open with a `WitWeaver` component on a GameObject.
:::warning
**The examples on this page use TextMeshPro** (`TMP_Text`, `TMP_Dropdown`, etc.). If you have not installed it, go to Window → Package Manager, find **TextMeshPro**, and click Install.

TextMeshPro is **not** a hard dependency of WitWeaver - the framework has no TMP references. You can build your UI using standard Unity UI Text, UI Toolkit, or any other system. The `TMP_Text` references in the code below are purely a choice for the examples.
:::

---

## Step 1: Create the MonoBehaviour

Create a new C# script named `MyDialogueUI.cs`. Replace its contents with the following:

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using WolfstagInteractive.WitWeaver;
using WolfstagInteractive.WitWeaver.UI;

public class MyDialogueUI : WitWeaverUIFoundation
{
    [SerializeField] private GameObject _dialoguePanel;
    [SerializeField] private TMP_Text _speakerNameText;
    [SerializeField] private TMP_Text _dialogueText;
    [SerializeField] private Button _advanceButton;
    [SerializeField] private Transform _choiceContainer;
    [SerializeField] private Button _choiceButtonPrefab;

    private bool _playerAdvanced;

    private void Awake()
    {
        _advanceButton.onClick.AddListener(OnAdvanceClicked);
    }

    protected override void InitializeUI(WitWeaver runner)
    {
        _dialoguePanel.SetActive(true);
        _playerAdvanced = false;
    }

    protected override void ApplyDialogueLine(
        DialogueLineInfo lineInfo,
        string localizedText,
        string speakerName,
        CharacterRepresentationBase representation,
        WitWeaverCharacterProfileBaseData primaryProfile)
    {
        _speakerNameText.text = speakerName;
        _dialogueText.text = localizedText;

        // Apply the character's name color if the profile provides one.
        if (primaryProfile != null)
            _speakerNameText.color = primaryProfile.CharacterNameColor;

        _playerAdvanced = false;
    }

    protected override IEnumerator WaitForUserInput()
    {
        _playerAdvanced = false;
        yield return new WaitUntil(() => _playerAdvanced);
    }

    protected override IEnumerator PresentChoices(
        List<ChoiceOption> options,
        List<string> localizedLabels,
        ChoiceResult result)
    {
        result.SelectedIndex = -1;

        // Hide the advance button while choices are shown.
        _advanceButton.gameObject.SetActive(false);

        // Instantiate one button per choice.
        for (int i = 0; i < localizedLabels.Count; i++)
        {
            int capturedIndex = i;
            Button btn = Instantiate(_choiceButtonPrefab, _choiceContainer);
            btn.GetComponentInChildren<TMP_Text>().text = localizedLabels[i];
            btn.onClick.AddListener(() => result.SelectedIndex = capturedIndex);
        }

        // Wait until the player selects a choice.
        yield return new WaitUntil(() => result.SelectedIndex >= 0);

        // Clean up choice buttons.
        foreach (Transform child in _choiceContainer)
            Destroy(child.gameObject);

        _advanceButton.gameObject.SetActive(true);
    }

    protected override void HideDialogue()
    {
        _dialoguePanel.SetActive(false);
    }

    private void OnAdvanceClicked()
    {
        _playerAdvanced = true;
        RequestAdvance?.Invoke();
    }
}
```

---

## Step 2: Build the UI Hierarchy

In your scene, create a **Canvas** (right-click in the Hierarchy → UI → Canvas). Inside it, build the following structure:

```
Canvas
└── DialoguePanel  (Panel image, anchored to bottom of screen)
    ├── SpeakerNameText   (TMP_Text)
    ├── DialogueText      (TMP_Text, set to wrap, auto-size off)
    ├── AdvanceButton     (Button with a TMP_Text child labeled "Continue")
    └── ChoiceContainer   (empty GameObject, add a VerticalLayoutGroup component)
```

**Suggested layout for the DialoguePanel**:
- Anchor: stretch horizontally, pin to bottom
- Height: approximately 200–250 px
- Add a background Image component with a semi-transparent dark color

**ChoiceContainer settings**:
- Add a `VerticalLayoutGroup` component
- Enable **Control Child Size** (Width and Height)
- Enable **Child Force Expand** (Width)
- Set spacing to 8

---

## Step 3: Create a Choice Button Prefab

Create a simple prefab for choice buttons:

1. In the Hierarchy, add a Button (right-click → UI → Button - TextMeshPro).
2. Set the button's minimum height to 40 px via a `LayoutElement` component.
3. Rename the TMP_Text child to `Label`.
4. Drag the button from the Hierarchy into the **Project** panel to create a prefab.
5. Delete the instance from the Hierarchy.

:::tip
Style the choice button prefab with a visible background and hover highlight so players can clearly see their options. The VerticalLayoutGroup on `ChoiceContainer` handles spacing and sizing automatically.
:::

---

## Step 4: Wire It Up in the Inspector

1. Add `MyDialogueUI` to the `DialoguePanel` GameObject using **Add Component**.
2. Fill in all serialized fields:
   - **Dialogue Panel** → the `DialoguePanel` GameObject
   - **Speaker Name Text** → the `SpeakerNameText` TMP_Text
   - **Dialogue Text** → the `DialogueText` TMP_Text
   - **Advance Button** → the `AdvanceButton` Button
   - **Choice Container** → the `ChoiceContainer` Transform
   - **Choice Button Prefab** → the Button prefab created in Step 3
3. Select the GameObject that has the `WitWeaver` component.
4. Drag the `DialoguePanel` GameObject (which now has `MyDialogueUI` on it) into the **Conversation UI** field on the `WitWeaver` component.

---

## Step 5: Hide the Panel at Start

The dialogue panel should be hidden until a conversation begins. Either:
- Uncheck the `DialoguePanel` GameObject's active checkbox in the Hierarchy, or
- Add `_dialoguePanel.SetActive(false)` to an `Awake()` or `Start()` method in your UI script

`InitializeUI()` will re-enable it when a conversation starts.

---

## Step 6: Add Keyboard Input (Optional)

To support keyboard or mouse-click advancement in addition to the button:

```csharp
private void Update()
{
    if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
    {
        // Only advance if a choice is not currently being presented.
        if (_choiceContainer.childCount == 0)
        {
            _playerAdvanced = true;
            RequestAdvance?.Invoke();
        }
    }
}
```

Add this method to `MyDialogueUI`. The guard condition (`childCount == 0`) prevents keyboard input from interfering while choice buttons are visible.

:::tip
For keyboard input, prefer `GetKeyDown` over `GetKey`; `GetKey` fires every frame and would skip lines too fast.
:::

---

## Step 7: Test It

Press **Play**. Trigger the conversation (via `StartConversation()` or the starter script from the Quick Start guide). You should see:

1. The dialogue panel appear.
2. The speaker's name and first line of text display.
3. Clicking the advance button (or pressing Space) moves to the next line.
4. At branch points, choice buttons appear. Clicking one selects that branch.
5. After the last line, the panel disappears.

---

## Troubleshooting

:::warning
**Text appears but never updates after the first line**: Check that `ApplyDialogueLine()` is declared with the `override` keyword. Without it, Unity will not call your implementation; it will call the empty base method (which does nothing) silently. (Also make sure you are overriding `ApplyDialogueLine` and not declaring your own `UpdateDialogueUI` — the latter is the foundation's fixed orchestration method and is not an override point.)
:::

:::warning
**Conversation completes instantly with no text visible**: `WaitForUserInput()` is returning immediately. Make sure it sets `_playerAdvanced = false` at the top and yields on `WaitUntil(() => _playerAdvanced)`. A common mistake is missing the `override` keyword, so the empty base method (it does nothing) runs instead.
:::

:::warning
**Choice buttons appear but clicking them does nothing**: Check that the `capturedIndex` variable is captured inside the loop with a local copy. Without `int capturedIndex = i`, all lambdas reference the same `i` variable and will all read the post-loop value.
:::

**No text at all (panel is visible but blank)**: Check the Console for YAML parse errors. Open the `WitWeaverConversationData` asset and confirm the YAML was validated successfully (right-click → Force Validate Dialogue Lines).

---

## Next Steps

| I want to… | Go here |
|---|---|
| Add a scrollable transcript of past lines | [Dialogue History →](dialogue-history) |
| Display character portraits alongside the text | [Character Representations →](../characters/character-representations) |
| Handle language switching in the UI | [Localization →](../localization/localization-overview) |
| Save and restore conversation progress | [Save System →](../save-system/save-system-overview) |
