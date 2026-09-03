---
sidebar_position: 1
title: UI Foundation
---

# UI Foundation

## What is WitWeaverUIFoundation?

`WitWeaverUIFoundation` is a concrete `MonoBehaviour` that defines the contract between the WitWeaver runner and your dialogue display. It is the bridge: WitWeaver™ calls methods on this class to say what should be shown, and your subclass overrides those methods to decide how to show it. All methods have default no-op implementations; override only what your UI needs.

Attach a subclass of `WitWeaverUIFoundation` to any GameObject in the scene, then drag that GameObject into the **Conversation UI** field on the `WitWeaver` component.

:::note
WitWeaver is deliberately headless - it manages conversation state and fires events, but contains zero UI code. This means you can build any kind of dialogue display: a text box, a speech bubble, a 3D floating panel, a comic strip, or a fully custom renderer. `WitWeaverUIFoundation` is the seam where your display plugs in. WitWeaver does not care what UI system you use - Unity UI (uGUI), UI Toolkit, TextMeshPro, IMGUI, or a completely custom approach are all valid.
:::

---

## Methods to Override

All base implementations do nothing by default. The runner will not crash if you do not override them, but nothing will appear on screen.

```csharp
public class WitWeaverUIFoundation : MonoBehaviour
{
    // Called once when a conversation starts.
    // Show your dialogue panel, reset any state here.
    protected virtual void InitializeUI(WitWeaver runner) { }

    // Called every time a new line is ready to display.
    // Update your speaker name, dialogue text, portrait, etc.
    // After this returns, the foundation automatically runs any expression
    // actions your rendering didn't run itself (see Expression Actions below).
    protected virtual void ApplyDialogueLine(
        DialogueLineInfo lineInfo,
        string localizedText,
        string speakerName,
        CharacterRepresentationBase representation,
        WitWeaverCharacterProfileBaseData primaryProfile) { }

    // Called when the active language changes mid-conversation.
    // Update the displayed text to the new localized string.
    protected virtual void UpdateForLanguageChange(
        string localizedText,
        string languageCode) { }

    // Coroutine. Must block until the player signals to advance.
    // Do NOT return immediately - see the warning below.
    protected virtual IEnumerator WaitForUserInput() { yield break; }

    // Coroutine. Display the available choices and write the player's
    // selection index to `result.SelectedIndex`.
    protected virtual IEnumerator PresentChoices(
        List<ChoiceOption> options,
        List<string> localizedLabels,
        ChoiceResult result) { yield break; }

    // Called when the conversation ends. Hide your dialogue panel here.
    protected virtual void HideDialogue() { }

    // Utility shortcut - display an arbitrary text string directly.
    protected virtual void DisplayDialogue(string text) { }
}
```

### WaitForUserInput

:::warning
`WaitForUserInput()` **must loop and yield**. It must not return immediately. If it returns on the first frame, every line in the conversation will advance instantaneously before the player can read anything; the conversation will appear to complete in a single frame with no visible text.

The correct pattern:

```csharp
private bool _playerAdvanced;

protected override IEnumerator WaitForUserInput()
{
    // Reset the flag at the start of each call.
    _playerAdvanced = false;

    // Yield until the player triggers an advance.
    yield return new WaitUntil(() => _playerAdvanced);
}

// Wire this to a button click, key press, or touch tap:
private void OnAdvanceInput()
{
    _playerAdvanced = true;
    RaiseAdvance(); // Tell WitWeaver to continue.
}
```
:::

### PresentChoices

`PresentChoices()` receives the list of choice options and a `ChoiceResult` object. Write the zero-based index of the player's selection to `result.SelectedIndex`. The runner reads that value after the coroutine completes and branches accordingly.

```csharp
protected override IEnumerator PresentChoices(
    List<ChoiceOption> options,
    List<string> localizedLabels,
    ChoiceResult result)
{
    result.SelectedIndex = -1; // -1 means no selection yet

    for (int i = 0; i < localizedLabels.Count; i++)
    {
        int capturedIndex = i;
        Button btn = Instantiate(_choiceButtonPrefab, _choiceContainer);
        btn.GetComponentInChildren<TMP_Text>().text = localizedLabels[i];
        btn.onClick.AddListener(() => result.SelectedIndex = capturedIndex);
    }

    yield return new WaitUntil(() => result.SelectedIndex >= 0);

    foreach (Transform child in _choiceContainer)
        Destroy(child.gameObject);
}
```

---

## Events

Fire these events from your input handler to signal the runner. WitWeaver subscribes to them internally.

| Event | When to fire |
|---|---|
| `RequestAdvance` | When the player wants to advance to the next line (button click, key press, tap). |
| `RequestReverse` | When the player wants to go back one line (back button, swipe left, etc.). |

Example key-based input handler:

```csharp
private void Update()
{
    if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
    {
        _playerAdvanced = true;
        RaiseAdvance();
    }
}
```

:::tip
Always use `RaiseAdvance()` (and `RaiseReverse()`) rather than invoking `RequestAdvance` or `RequestReverse` directly. The protected helpers are the intended API; they keep your subclass insulated from the event signature and make it immediately clear to readers that this is a runner signal, not an arbitrary event call.
:::

---

## MaxVisibleCharacterSlots

```csharp
public virtual int MaxVisibleCharacterSlots => 3;
```

Returns `3` by default: one primary speaker and up to two secondary characters visible simultaneously. Override this property if your UI supports a different number of simultaneous character portraits.

WitWeaver uses this value to determine how many `WitWeaverCharacterDisplayBase` components to manage. If your UI is speaker-only (no secondary characters shown), return `1`.

---

## WitWeaverCharacterDisplayBase

:::info[For Advanced Users]
`WitWeaverCharacterDisplayBase` is an abstract `MonoBehaviour` companion to `WitWeaverUIFoundation`. It represents the visual panel for one character slot: the portrait area, model view, or sprite display for a single character.

Your UI can have up to `MaxVisibleCharacterSlots` of these components. When the runner applies an expression for a line, it calls `ApplyExpression()` on the relevant `CharacterRepresentationBase` asset, passing the matching `WitWeaverCharacterDisplayBase` component so the representation can update it directly.

Extend it to hook into your portrait renderer, animator, or any display system:

```csharp
public class MyCharacterDisplay : WitWeaverCharacterDisplayBase
{
    [SerializeField] private Image _portraitImage;
    [SerializeField] private Animator _animator;

    public override Animator Animator => _animator;

    // Called by the representation with the resolved sprite.
    public override void SetSprite(Sprite sprite)
    {
        _portraitImage.sprite = sprite;
        _portraitImage.enabled = sprite != null;
    }

    // Called when this slot becomes empty (no character assigned).
    public override void ClearDisplay()
    {
        _portraitImage.sprite = null;
        _portraitImage.enabled = false;
    }
}
```

You do not need to use `WitWeaverCharacterDisplayBase`; it is a convenience layer, not a requirement. If your UI manages character visuals independently in `ApplyDialogueLine()`, you can skip it entirely.
:::

---

## Expression Actions Run Automatically

After your `ApplyDialogueLine()` override returns, the foundation runs the
[expression actions](../characters/expressions#expression-actions) for every visible character on
the line. You do not need to do anything for them to fire in a custom UI.

Two refinements are available:

- Call `RunExpressionActions(representation, expressionId, lineIndex, display, slotIndex)` yourself
  during rendering when you can supply the resolved `IWitWeaverCharacterDisplay` (the prefab path)
  or need the actions to fire at an exact moment relative to your visuals. Pass the character's
  slot index (its position in the line's `CharacterRepresentations` list, 0 = speaker) — that is
  what tells the automatic pass "this slot is handled", so anything you run is not run again, even
  if you rendered a different representation than the default resolution would pick.
- On the automatic pass, `context.Display` is null — actions that depend on a display handle should
  be run manually as above.

When your UI needs to resolve a line entry to a representation asset (portraits, slots), call
`conversationData.ResolveRepresentation(in data, fallbackCharacterId, role)` rather than looking
it up by hand — it is the same path the runner and the automatic pass use, so your UI can never
disagree with them.

Expression actions re-fire every time a line is presented, including back-navigation; they are
required to be idempotent (see [Expression Actions](../characters/expressions#expression-actions)).

---

## Next Steps

| I want to… | Go here |
|---|---|
| Walk through building a full working UI from scratch | [Building a Custom UI →](building-a-ui) |
| Add a scrollable dialogue transcript to your UI | [Dialogue History →](dialogue-history) |
| Understand character portraits and expression rendering | [Character Representations →](../characters/character-representations) |
