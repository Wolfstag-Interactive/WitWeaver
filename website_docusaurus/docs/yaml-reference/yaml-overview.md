---
sidebar_position: 1
title: YAML Overview
---

# YAML Overview

WitWeaver authors dialogue in YAML - a plain-text format that any editor can open, any version-control system can track, and any writer can read without prior programming experience. This page explains what YAML is, why WitWeaver uses it, and the practical rules you need to follow to write valid dialogue files.

---

## What is YAML?

YAML stands for "YAML Ain't Markup Language." It is a human-readable data format that expresses structure through indentation rather than brackets, braces, or XML tags.

:::note
YAML is like JSON but without the braces, brackets, or mandatory quotes - it uses indentation to show hierarchy instead. A YAML key-value pair looks like `Key: Value`. Nested values are indented beneath their parent with consistent spacing. If you have ever written a configuration file for a tool or game engine, you have likely already seen YAML.
:::

Here is a side-by-side comparison of the same data written in JSON and YAML:

**JSON:**
```json
{
  "CharacterID": "Guard",
  "LocalizedDialogue": {
    "EN": "Halt! Who goes there?",
    "FR": "Halte! Qui passe?"
  }
}
```

**YAML:**
```yaml
CharacterID: "Guard"
LocalizedDialogue:
  EN: "Halt! Who goes there?"
  FR: "Halte! Qui passe?"
```

The YAML version contains the same information with less visual noise. Indentation (two spaces in this example) shows that `EN` and `FR` are nested under `LocalizedDialogue`.

---

## Why WitWeaver uses YAML

### Dialogue lives in source control

Because YAML is plain text, every dialogue line is a diff in your version-control history. Writers and programmers can work side-by-side in the same repository. Merge conflicts in dialogue files are resolved the same way as code conflicts - line by line, with full context visible.

### Anyone can edit it

A writer who has never opened Unity can open a `.yml` file in any text editor, change a line of dialogue, save the file, and have that change flow into the game automatically the next time Unity is active. No Unity knowledge required.

### Hot-reload in the editor

The `WitWeaverYamlWatcher` is an editor-only system that watches your YAML files for changes. The moment you save a `.yml` file, the watcher reads the new content, embeds it in the linked Conversation Data asset, and reparses the dialogue - all without you pressing a button or triggering a reimport. You can keep Unity open on one screen and your text editor on another and see changes reflected immediately.

---

## YAML is the single source of truth

The `WitWeaverConversationData` ScriptableObject is a **compiled cache** of your YAML. It is the runtime representation that Unity serializes and ships in your build. When WitWeaver parses a YAML file, it writes the result into the ScriptableObject's fields.

**Never edit `WitWeaverConversationData` directly in the Inspector and then modify the YAML separately.** If you do, the next YAML save will overwrite your Inspector edits. Always make dialogue changes in the YAML file and let WitWeaver recompile.

:::warning
Editing the compiled `WitWeaverConversationData` asset directly (instead of the YAML source) will cause your changes to be silently overwritten the next time the YAML file is saved. There is no merge step - the YAML always wins. Treat the ScriptableObject as read-only output, not an editing surface.
:::

The correct workflow is always:

```
Edit .yml file → Save → WitWeaverYamlWatcher auto-updates the asset → Done
```

---

## File naming and location

- Use the `.yml` extension, not `.yaml`. Unity's `TextAsset` importer recognizes `.yml` files and treats them as text assets. Using `.yaml` requires extra configuration that `.yml` avoids.
- The file name does not need to match the conversation key written inside the file. You might name your file `chapter_one.yml` and have a conversation key of `VillageIntro` inside it. Both are independent identifiers.
- You can store YAML files anywhere inside your `Assets/` folder, but a dedicated folder (such as `Assets/Dialogue/`) keeps your project tidy. The Conversation Data asset links the source file and embeds its text for runtime use - move or rename the file through the Unity editor so the link is repaired automatically.

:::tip
Use a YAML-aware editor such as [VS Code](https://code.visualstudio.com/) with the [YAML extension by Red Hat](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) installed. It gives you syntax highlighting, indentation guides, and inline error messages that catch tab characters and misaligned keys before you switch back to Unity.
:::

---

## Whitespace rules

YAML uses whitespace to define structure. This makes it readable but also sensitive to errors:

- **Always use spaces, never tabs.** Most text editors default to tabs for indentation. YAML parsers (including the one WitWeaver uses, YamlDotNet) reject tab characters. Configure your editor to insert spaces when you press Tab.
- **Keep indentation consistent.** If you indent the first entry with two spaces, indent every sibling entry with two spaces. Mixing two-space and four-space indentation inside the same block will cause a parse error.
- **Do not add trailing spaces** at the end of lines. While not always fatal, they can cause subtle mismatches in multi-line strings.

:::warning
YAML is sensitive to indentation. A single tab character, one missing space, or one extra space at the start of a line will produce a parse error. When WitWeaver cannot parse a YAML file, it logs an error to the Unity Console and the Conversation Data asset will have no dialogue lines. If your conversation appears empty, the first place to look is your YAML file for indentation problems.
:::

---

## Next steps

- [YAML Format](./yaml-format) - Full field reference and annotated examples for every supported field.
- [Line Continuation](../core-systems/line-continuation) - Control what happens after each dialogue line plays (inspector-only - configured on the asset, not in YAML).
- [Player Choices](../core-systems/player-choices) - Set up branching dialogue where the player selects from options.
- [YAML Loading](./yaml-loading) - How WitWeaver finds and loads YAML files at runtime.
