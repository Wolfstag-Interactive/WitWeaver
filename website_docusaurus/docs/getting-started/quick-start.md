---
sidebar_position: 2
title: Quick Start
---

# Quick Start

This guide walks you through creating a simple “Hello World” conversation from scratch. By the end you will have a fully wired WitWeaver™ setup running in a Unity scene, with console log output confirming that lines are advancing correctly. Adding a visible UI is covered in the [UI Foundation](../ui/ui-foundation) page; this guide focuses on getting the core logic working first.

:::info[Minimum Setup at a Glance]
Every WitWeaver conversation needs exactly four things:

1. **A YAML file**: your dialogue text
2. **A Character Profile asset**: defines who is speaking
3. **A Conversation Data asset**: links the YAML and participants together
4. **A WitWeaver component** on a GameObject: the runtime runner

This guide builds each one from scratch, step by step.
:::

**Time to complete: approximately 10 minutes.**

---

## Step 1: Create a YAML file

Right-click anywhere in the **Project** panel → **Create → WitWeaver → Create Yaml File**. Name the new file `MyFirstConversation`.

Open the file (double-click to open in your script editor) and replace its contents with the following:

```yaml
MyFirstConversation:
  - CharacterID: "Narrator"
    LocalizedDialogue:
      EN: "Hello! This is your first WitWeaver conversation."
  - CharacterID: "Narrator"
    LocalizedDialogue:
      EN: "Press any key to advance to the next line."
  - CharacterID: "Narrator"
    LocalizedDialogue:
      EN: "That's it! You've finished the conversation."
```

:::note
**What is YAML?** YAML (“YAML Ain’t Markup Language”) is a human-readable plain-text format commonly used for configuration and data files. WitWeaver uses YAML for dialogue: you write your lines in YAML, and WitWeaver compiles them into Unity assets. The indentation in YAML is significant: use two spaces per indent level (not tabs).
:::

---

## Step 2: Create a Character Profile

Right-click in the **Project** panel → **Create → WitWeaver → Character Profile**. Name the new asset `Narrator`.

Select the asset and look at its **Inspector**:

- Set **Character ID** to `Narrator`.
- Set **Character Name** to `Narrator` (this is the display name shown in UI; it can differ from the ID, but for this tutorial keep them the same).

Leave all other fields at their defaults for now.

:::tip
**Character ID is case-sensitive** and must exactly match the `CharacterID` value in your YAML. If your YAML says `"Narrator"` but the profile says `"narrator"`, WitWeaver will not be able to link them and will log a warning at parse time. Always copy-paste the ID rather than typing it twice.
:::

---

## Step 3: Create a Conversation Data asset

Right-click in the **Project** panel → **Create → WitWeaver → Conversation Dialogue Object**. Name the new asset `MyFirstConversation`.

Select the asset and configure it in the **Inspector**:

1. **Conversation Key**: Set this to `MyFirstConversation`. This must exactly match the root key in your YAML file (the first line, `MyFirstConversation:`).

2. **Participant Profiles**: Click the **+** button on the Participant Profiles list and drag the `Narrator` Character Profile asset into the new slot.

3. **Link and compile the YAML**: With the asset still selected, scroll to the **Dialogue Source** section, select the **YAML** tab, assign your `.yml` file to **Source .yaml**, and click **Link & Embed**. WitWeaver validates the file, generates LineIDs, and embeds the text into the asset. A sync-status warning then appears saying the embedded source has lines this asset does not; click **Import From YAML For Key** in that warning to populate the compiled dialogue lines. Check the Console for any parse warnings.

:::note
**What is a ScriptableObject?** In Unity, a ScriptableObject is a data asset stored as a file in your project, similar to a spreadsheet or config file that you can edit visually in the Inspector. The Conversation Data asset is a ScriptableObject that holds the compiled version of your YAML: the list of participants, the ordered dialogue lines, localized text, and metadata. You never need to edit the compiled data by hand; always edit the YAML and re-import.
:::

:::warning
If you skip the **Link & Embed** and **Import From YAML For Key** steps, the Conversation Data asset will be empty at runtime and no lines will play. If your conversation silently does nothing when you press Play, this is the first thing to check.
:::

---

## Step 4: Add WitWeaver to the scene

Open (or create) the scene you want to test in.

1. In the **Hierarchy** panel, right-click → **Create Empty**. Rename the new GameObject to `DialogueRunner`.

2. With `DialogueRunner` selected, click **Add Component** in the Inspector. Search for **WitWeaver** and add the `WitWeaver` component.

3. In the WitWeaver component Inspector:
   - Find the **Input Mode** (or **Conversation Input**) field and set it to **Single Conversation**.
   - A **Conversation** field will appear. Drag your `MyFirstConversation` Conversation Data asset into it.

:::note
**What is a MonoBehaviour?** A MonoBehaviour is a C# script you attach to a GameObject to give it behaviour, like adding an engine to a car. The `WitWeaver` component is a MonoBehaviour that manages conversation state: which line is current, when to advance, which character is speaking, and when the conversation ends. It runs the logic but does not display anything itself.
:::

---

## Step 5: Call StartConversation

For this tutorial, you will start the conversation automatically when the scene loads. In a real project you would trigger it from a collider, button, or cutscene event.

1. In the **Hierarchy**, right-click → **Create Empty**. Rename the new GameObject to `WitWeaverStarter`.

2. In the Inspector for `WitWeaverStarter`, click **Add Component → New Script**. Name the script `WitWeaverStarter` and click **Create and Add**.

3. Open `WitWeaverStarter.cs` in your editor and replace its contents with:

```csharp
using UnityEngine;
using WolfstagInteractive.WitWeaver;

public class WitWeaverStarter : MonoBehaviour
{
    [SerializeField] private WitWeaver _runner;

    private void Start()
    {
        _runner.StartConversation();
    }
}
```

4. Save the script. Back in Unity, select the `WitWeaverStarter` GameObject. In the Inspector, drag the `DialogueRunner` GameObject into the **Runner** field that has appeared on the `WitWeaverStarter` component.

:::tip
For production use, wire `StartConversation()` to a **UnityEvent** instead, for example from a trigger collider’s `OnTriggerEnter`, a UI button’s `onClick` event, or a timeline signal. Calling it from `Start()` works for testing, but it fires before your scene has fully settled (e.g., before any fade-in or camera transition completes).
:::

---

## Step 6: Enable debug logging and press Play

Before pressing Play, enable per-line console logging so you can verify the conversation is running:

1. Select the `DialogueRunner` GameObject in the Hierarchy.
2. In the Inspector, find the **Debug** section on the WitWeaver component.
3. Check the **Debug Log Lines** checkbox.

Now press **Play**. Open the **Console** (Window → General → Console). You should see a log entry for each dialogue line:

```
[WitWeaver] Line 0 - Narrator: "Hello! This is your first WitWeaver conversation."
[WitWeaver] Line 1 - Narrator: "Press any key to advance to the next line."
[WitWeaver] Line 2 - Narrator: "That's it! You've finished the conversation."
```

:::tip
Click any log entry in the Console and Unity will **highlight the WitWeaver runner** in the Hierarchy. This makes it easy to find the component responsible for the output in complex scenes.
:::

:::note
**WitWeaver is headless by design.** It manages conversation state and fires C# events at each stage, but displaying text, portraits, or choices is entirely up to your UI layer. This means you can use any UI system (Unity uGUI, UI Toolkit, TextMeshPro, or a custom renderer) without WitWeaver knowing or caring. The [UI Foundation](../ui/ui-foundation) page explains how to build a display layer. Turn off **Debug Log Lines** when you are done testing.
:::

If you see errors instead of log output, the most common causes are:

- **Conversation does nothing** - The YAML was not imported. Re-run **Link & Embed** and **Import From YAML For Key** (Step 3).
- **“Character ID not found”** - The CharacterID in the YAML does not match any Character Profile. Check case and spelling (Step 2).
- **No log output at all** - Make sure **Debug Log Lines** is checked in the WitWeaver component’s Debug section.

See [Troubleshooting →](troubleshooting) for a full list of common issues.

---

## Step 7: Next steps

You now have a working WitWeaver setup. Here is where to go next:

| I want to… | Go here |
|---|---|
| Understand all the YAML options (branches, choices, expressions) | [YAML Format →](../yaml-reference/yaml-format) |
| Display dialogue text and portraits on screen | [UI Foundation →](../ui/ui-foundation) |
| Learn about the WitWeaver component in depth | [WitWeaver Component →](../core-systems/witweaver-component) |
| Add branching choices | [Player Choices →](../core-systems/player-choices) |
| Save conversation progress between sessions | [Save System →](../save-system/save-system-overview) |
| Solve a common setup problem | [Troubleshooting →](troubleshooting) |
