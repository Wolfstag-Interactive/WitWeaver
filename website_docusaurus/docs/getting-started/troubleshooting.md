---
sidebar_position: 4
title: Troubleshooting
---

# Troubleshooting

This page covers the most common problems encountered when setting up or using WitWeaver, along with their causes and fixes.

---

## Conversation Does Nothing on Play

**Symptoms:** Pressing Play starts the scene, but nothing happens -- no console output, no UI change.

**Common causes:**

1. **YAML not imported** -- Open the Conversation Data asset and click **Import YAML from Key**, then **Sync from Source**. Without this step the asset contains no dialogue lines and the conversation ends immediately.

2. **`StartConversation()` is never called** -- The WitWeaver component does not auto-start. You must call `StartConversation()` (from a script, a UnityEvent, or a trigger). Check your starter script is active and the runner reference is assigned.

3. **WitWeaver component is inactive** -- Select the `DialogueRunner` GameObject and confirm the component is enabled and the GameObject is active.

4. **Wrong conversation assigned** -- The Conversation Data slot on the WitWeaver component may be empty or point to a different asset. Check the Inspector.

---

## No Console Output (Quick Start)

**Symptom:** Following the Quick Start guide, the Console shows nothing when you press Play.

**Fix:** Enable the **Debug Log Lines** checkbox. Select the `DialogueRunner` GameObject, find the **Debug** section on the WitWeaver component, and check **Debug Log Lines**. Each line will then print to the Console as `[WitWeaver] Line N -- CharacterName: "text"`. Click a log entry to highlight the runner in the Hierarchy.

---

## Text Never Updates After the First Line

**Symptom:** The dialogue panel appears and shows the first line. Clicking advance or pressing a key plays audio or fires events, but the displayed text never changes.

**Cause:** `ApplyDialogueLine()` is missing the `override` keyword in your UI subclass (or you declared your own `UpdateDialogueUI` — that is the foundation's fixed orchestration method, not the override point). Without `override`, Unity silently calls the empty base class version instead of your implementation.

**Fix:**
```csharp
// WRONG -- ApplyDialogueLine() on the base class runs instead
protected void ApplyDialogueLine(...) { ... }

// CORRECT
protected override void ApplyDialogueLine(...) { ... }
```

Verify all overridden methods (`ApplyDialogueLine`, `WaitForUserInput`, `PresentChoices`, `HideDialogue`) have the `override` keyword.

---

## Conversation Completes Instantly (No Text Visible)

**Symptom:** The dialogue panel flashes briefly or not at all, and the conversation completes in a single frame.

**Cause:** `WaitForUserInput()` is returning immediately. Either:
- The `override` keyword is missing (same as above -- the empty base version runs and exits).
- Your `WaitForUserInput()` implementation does not yield -- it executes and returns in one frame.

**Fix:** Ensure `WaitForUserInput()` uses `yield return new WaitUntil(...)` and the flag it waits on is only set when the player triggers an advance action:

```csharp
private bool _playerAdvanced;

protected override IEnumerator WaitForUserInput()
{
    _playerAdvanced = false;
    yield return new WaitUntil(() => _playerAdvanced);
}

private void OnAdvanceClicked()
{
    _playerAdvanced = true;
    RequestAdvance?.Invoke();
}
```

---

## Choice Buttons Appear But Clicking Does Nothing

**Symptom:** Branching choices display correctly, but clicking any button has no effect -- the conversation does not advance.

**Cause:** A C# lambda closure bug. If you write `btn.onClick.AddListener(() => result.SelectedIndex = i)` inside a loop, all lambdas capture the same `i` variable -- which equals the loop's final value after the loop ends.

**Fix:** Capture the index in a local variable inside the loop body:

```csharp
for (int i = 0; i < localizedLabels.Count; i++)
{
    int capturedIndex = i; // capture a fresh copy per iteration
    Button btn = Instantiate(_choiceButtonPrefab, _choiceContainer);
    btn.GetComponentInChildren<TMP_Text>().text = localizedLabels[i];
    btn.onClick.AddListener(() => result.SelectedIndex = capturedIndex);
}
```

---

## "Character ID Not Found" Warning

**Symptom:** The Console shows a warning like `Cannot resolve character profile for ID: 'narrator'` and the line is skipped.

**Cause:** `CharacterID` in YAML is case-sensitive and must exactly match the **Character ID** field on the Character Profile asset. `"Narrator"` and `"narrator"` are different IDs.

**Fix:** Copy-paste the CharacterID value from the Profile asset into the YAML file (or vice versa). Do not retype it.

---

## Events Not Firing After Scene Reload

**Symptom:** C# event subscriptions work in the first Play session but stop working after you stop and re-enter Play mode, or after a scene reload.

**Cause:** Subscribing in `Awake()` means the subscription persists on the original object instance -- but after a reload, the runner is a new instance. Alternatively, subscribing with `+=` without a matching `-=` causes stale references.

**Fix:** Always subscribe in `OnEnable()` and unsubscribe in `OnDisable()`:

```csharp
private void OnEnable()
{
    _runner.OnLineStarted += HandleLineStarted;
}

private void OnDisable()
{
    _runner.OnLineStarted -= HandleLineStarted;
}
```

See [Event Subscription Safety](../core-systems/conversation-state#event-subscription-safety) for more detail.

---

## Save State Not Restoring

**Symptom:** The save system is set up but conversations always start from line 0 regardless of saved progress.

**Common causes:**

1. **YAML not validated after LineIDs were generated** -- LineIDs must be present and stable in the compiled asset for save state to match. After the first import and sync, open the Conversation Data asset and confirm each line has a non-empty `LineID` field.

2. **ConversationGuid changed** -- The save system keys snapshots by `ConversationGuid`. If you called `RegenerateGuid()` or the asset was re-created, the old save data no longer matches. Existing save files become orphaned.

3. **`WitWeaverConversationSaveManager` not on the same GameObject** -- The save manager must be on the same GameObject as the `WitWeaver` runner, or it will not be found via `GetComponent`.

4. **Auto-restore flags not set** -- Confirm that **Restore On Awake** or **Restore On Start** is checked on the `WitWeaverConversationSaveManager` component.

---

## LineIDs Are Empty After Validation

**Symptom:** After clicking Import YAML from Key and Sync from Source, the compiled dialogue lines have no LineID values.

**Cause:** The YAML file was not linked to the Conversation Data asset before importing. LineIDs are generated during the import/compile step -- they cannot be generated without a source YAML.

**Fix:** Open the Conversation Data asset. Ensure the **Conversation Key** field exactly matches the root key in your YAML (the first line, before the colon). Then click **Import YAML from Key** followed by **Sync from Source**.

---

## The UI Foundation Methods Feel Backward

If the connection between `WitWeaverUIFoundation` and the runner is confusing, read it this way: WitWeaver *calls into* your UI, not the other way around. You do not poll WitWeaver -- WitWeaver calls `UpdateDialogueUI()` (which renders through your `ApplyDialogueLine()` override, then runs expression actions), `WaitForUserInput()`, and `PresentChoices()` on your component at the right moments. Your job is to override `ApplyDialogueLine()` and the input methods and make them do the right visual thing.

[UI Foundation](../ui/ui-foundation) | [Building a Custom UI](../ui/building-a-ui)
