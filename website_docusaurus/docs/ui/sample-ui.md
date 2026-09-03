---
sidebar_position: 2
title: Sample UI
---

# Sample UI

WitWeaver™ includes a fully working dialogue UI in its Samples package. If you want a real, working display layer to learn from or build on top of rather than creating one from scratch, this is the fastest way to get started.

---

## What Is Included

The Samples package contains:

| Asset | Description |
|---|---|
| `ConversationToolDialogueUI.prefab` | A ready-made dialogue panel prefab: speaker name, dialogue text, choice buttons, and character portrait area |
| `WitWeaverSampleConversationData.asset` | A pre-built conversation asset with multiple lines and branching choices |
| `DialogueScriptSample.yml` | The YAML source file for the sample conversation |
| Character Profile assets | Three example profiles (Ava, Jared, Wolfstag) with expression mappings |
| Sprite assets | Character and environment sprites used by the sample scene |
| `AutoStartSampleConversation.cs` | A helper script that starts the sample conversation automatically on Play |
| `WitWeaverSampleScene.unity` | A complete sample scene wired up and ready to run |

---

## How to Import the Samples

1. Open the **Package Manager** (Window → Package Manager).
2. In the package list on the left, find and select **WitWeaver**.
3. Click the **Samples** tab on the right side of the Package Manager window.
4. Click **Import** next to the WitWeaver samples entry.

Unity will copy the sample assets into your project at `Assets/Samples/WitWeaver/`. You can move these files wherever you like; they are fully editable copies, not references to the package source.

:::tip
After importing, open `WitWeaverSampleScene.unity` and press **Play** to see WitWeaver running with a complete dialogue, branching choices, and animated character portraits, all wired up and ready to inspect.
:::

---

## The Sample UI Prefab

The `ConversationToolDialogueUI` prefab is a `WitWeaverUIFoundation` subclass, the same base class you extend when building a custom UI. It demonstrates all the core patterns:

- **Speaker name display**: The active character’s name appears at the top of the dialogue panel, tinted with their profile color.
- **Dialogue text**: Each line’s localized text is displayed using a TextMeshPro component.
- **Advance button**: A "Continue" button advances to the next line. Keyboard input (Space / Enter) also works.
- **Choice buttons**: When a line has branching options, the advance button is hidden and a set of choice buttons is presented instead. Selecting one dismisses the buttons and advances into the chosen branch.
- **Character portrait area**: The panel includes a portrait display slot that the sample expressions system updates per line.

---

## Using It as a Starting Point

The simplest approach is to copy the prefab into your own project folder and modify it:

1. In the Project panel, navigate to `Assets/Samples/WitWeaver/`.
2. Duplicate `ConversationToolDialogueUI.prefab` (Ctrl/Cmd + D) and move the copy to your own project folder (e.g. `Assets/MyGame/UI/`).
3. Open the prefab for editing by double-clicking it.
4. Modify the layout, colors, fonts, and hierarchy to match your game’s art style.
5. The script attached to the prefab root is the `WitWeaverUIFoundation` subclass; you can read it to understand how each method is implemented, then adapt it or rewrite it for your needs.

:::note
The sample UI scripts are editable C# source files, not compiled DLLs. You can read, copy, and modify them freely. The scripts use **TextMeshPro** (`TMPro` namespace) for text rendering; if you want to use a different text system, replace the `TMP_Text` references with your preferred component type.
:::

:::warning
**TextMeshPro dependency**: The sample UI requires TextMeshPro to be installed (Window → Package Manager → TextMeshPro). TextMeshPro is **not** a hard dependency of WitWeaver itself; it is only used in the sample UI scripts and prefab. If you build a custom UI from scratch using a different text system, you do not need TMP installed.
:::

---

## Wiring the Sample Prefab to WitWeaver

If you want to drop the sample prefab into your own scene:

1. Drag `ConversationToolDialogueUI.prefab` into your scene Hierarchy (as a child of a Canvas).
2. Select the **WitWeaver** runner component in your scene.
3. Drag the prefab’s root GameObject into the **Conversation UI** field on the WitWeaver component.

The panel will appear when a conversation starts and hide when it ends.

---

## Next Steps

| I want to… | Go here |
|---|---|
| Build a custom UI from scratch | [Building a Custom UI →](building-a-ui) |
| Understand the methods the UI overrides | [UI Foundation →](ui-foundation) |
| Add a scrollable dialogue transcript | [Dialogue History →](dialogue-history) |
| Display character expressions alongside the text | [Expressions →](../characters/expressions) |
