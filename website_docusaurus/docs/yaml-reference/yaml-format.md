---
sidebar_position: 2
title: YAML Format
---

# YAML Format

This page is the complete field reference for WitWeaver™'s YAML dialogue format. Every supported field is documented here with an explanation of its purpose, whether it is required, and examples showing correct usage.

---

## Root structure

A WitWeaver YAML file contains one or more conversations. Each conversation is a **top-level key** - the Conversation Key - mapped to a list of dialogue lines. The Conversation Key is the identifier that links this YAML block to a `WitWeaverConversationData` asset in Unity.

```yaml
MyConversation:
  - CharacterID: "Narrator"
    LocalizedDialogue:
      EN: "Hello world."
```

In this example, `MyConversation` is the Conversation Key. The `-` that opens the next line starts a list entry (a single dialogue line). Each dialogue line is an indented block of fields.

You can define multiple conversations in one file by adding additional top-level keys:

```yaml
MorningGreeting:
  - CharacterID: "NPC"
    LocalizedDialogue:
      EN: "Good morning!"

EveningGreeting:
  - CharacterID: "NPC"
    LocalizedDialogue:
      EN: "Good evening!"
```

Each conversation key must have a corresponding `WitWeaverConversationData` asset in your Unity project with its **Conversation Key** field set to that exact string.

---

## Fields per dialogue line

### CharacterID

**Required.**

The identifier of the character speaking this line. This value must exactly match the `CharacterID` field on the corresponding `WitWeaverCharacterProfileBaseData` asset.

```yaml
- CharacterID: "Guard"
  LocalizedDialogue:
    EN: "Halt! Who goes there?"
```

:::warning
`CharacterID` is case-sensitive. `"guard"`, `"Guard"`, and `"GUARD"` are three different identifiers. If WitWeaver cannot find a character profile matching the ID on a line, it will log a warning and attempt to continue with no character display. Always copy the CharacterID exactly from the character profile asset.
:::

---

### LineID

**Automatically generated. Do not write these manually.**

A `LineID` is a stable, unique identifier for each dialogue line within its conversation. WitWeaver uses LineIDs to track which lines the player has visited and where they left off - essential for save/restore to work correctly even after you add, remove, or reorder lines in the YAML.

**LineIDs are generated for you automatically.** When you link a YAML file to a `WitWeaverConversationData` asset (by assigning it in the inspector and running validation), WitWeaver reads your YAML, assigns a unique ID to every line that doesn't already have one, and writes them back into the YAML file. You will see them appear in your source file after the first import:

```yaml
- CharacterID: "Guard"
  LineID: "a1b2c3d4"    # ← written by WitWeaver automatically
  LocalizedDialogue:
    EN: "Halt! Who goes there?"
```

Once a LineID has been generated for a line, it is stable for the lifetime of that line. You can freely add new lines, remove other lines, or reorder the conversation. The existing IDs do not change, so saved progress remains valid.

:::warning
Do not edit or delete a LineID that WitWeaver has written. Changing an ID is equivalent to removing the old line and adding a new one - any save data referencing the old ID will no longer match and the player's progress for that line will be lost. Treat auto-generated LineIDs as read-only.
:::

:::note
If you delete a line from the YAML, its LineID disappears with it. Save data that referenced that line will gracefully fall back to the nearest valid line. This is expected behaviour when you intentionally remove dialogue.
:::

---

### LocalizedDialogue

**Required.**

A map of language codes to display strings. The language code keys must match the codes registered in your `WitWeaverSettings` asset, but matching is case-insensitive at runtime - `EN`, `en`, and `En` all resolve correctly.

At least one language key must be present. If the player's currently active language has no entry for a line, WitWeaver falls back to the first available language and logs a warning.

**Single-language example:**
```yaml
- CharacterID: "Narrator"
  LocalizedDialogue:
    EN: "The kingdom fell silent."
```

**Multi-language example:**
```yaml
- CharacterID: "Guard"
  LocalizedDialogue:
    EN: "Halt! Who goes there?"
    FR: "Halte! Qui passe?"
    ES: "Alto! Quien pasa?"
    DE: "Halt! Wer geht da?"
```

---

## Complete minimal example

This is what you write:

```yaml
TownSquare:
  - CharacterID: "Guard"
    LocalizedDialogue:
      EN: "Halt! Who goes there?"
  - CharacterID: "Player"
    LocalizedDialogue:
      EN: "It's just me, passing through."
  - CharacterID: "Guard"
    LocalizedDialogue:
      EN: "Move along, then."
```

After linking this file to a `WitWeaverConversationData` asset (Dialogue Source section, YAML tab) and embedding it, WitWeaver writes the LineIDs back into the file automatically (imports initiated from a linked spreadsheet never write back to a YAML file; their LineIDs go into the spreadsheet instead):

```yaml
TownSquare:
  - CharacterID: "Guard"
    LineID: "a1b2c3d4"    # written by WitWeaver - do not edit
    LocalizedDialogue:
      EN: "Halt! Who goes there?"
  - CharacterID: "Player"
    LineID: "e5f6a7b8"    # written by WitWeaver - do not edit
    LocalizedDialogue:
      EN: "It's just me, passing through."
  - CharacterID: "Guard"
    LineID: "c9d0e1f2"    # written by WitWeaver - do not edit
    LocalizedDialogue:
      EN: "Move along, then."
```

---

## Special characters

### Apostrophes

Apostrophes inside single-quoted YAML strings will break parsing because YAML uses the single quote as the string delimiter. There are two reliable ways to handle dialogue text that contains apostrophes.

**Use double quotes (recommended):**
```yaml
# Safe - double quotes let apostrophes appear freely
LocalizedDialogue:
  EN: "It's a trap!"
```

**Escape the apostrophe inside single-quoted strings:**
```yaml
# Also valid - double the apostrophe to escape it in single-quoted context
LocalizedDialogue:
  EN: 'It''s a trap!'
```

**This will cause a parse error:**
```yaml
# Do not do this
LocalizedDialogue:
  EN: 'It's a trap!'
```

WitWeaver includes a pre-processor that auto-wraps many common apostrophe cases before handing the YAML to the parser, but relying on that behavior is not recommended. Writing double-quoted values consistently is the safest and most explicit approach.

---

### Multi-line dialogue text

For long lines of dialogue that wrap across multiple source lines, use YAML's literal block scalar (`|`) or folded block scalar (`>`).

**Literal block scalar** (`|`) - preserves newlines exactly:
```yaml
- CharacterID: "Narrator"
  LineID: "narrator_prologue"
  LocalizedDialogue:
    EN: |
      Long ago, in a kingdom forgotten by time,
      a hero rose from the ashes of a fallen empire.
```

**Folded block scalar** (`>`) - joins lines with spaces, keeps paragraph breaks:
```yaml
- CharacterID: "Elder"
  LineID: "elder_warning"
  LocalizedDialogue:
    EN: >
      This path is treacherous. Many have tried
      and none have returned. I urge you to reconsider.
```

:::tip
Prefer the folded scalar (`>`) for dialogue that your UI will word-wrap automatically. Use the literal scalar (`|`) when you need explicit line breaks - for example, poetry, in-game letters, or UI tooltips where you control the layout.
:::

---

### The `{PlayerName}` placeholder

Write `{PlayerName}` anywhere inside a dialogue string. At runtime, WitWeaver replaces it with the `CharacterName` from the character profile asset that has the `IsPlayerCharacter` flag checked.

```yaml
- CharacterID: "Innkeeper"
  LineID: "innkeeper_welcome"
  LocalizedDialogue:
    EN: "Welcome back, {PlayerName}! Your usual room is ready."
    FR: "Bienvenue, {PlayerName}! Votre chambre est prete."
```

The substitution happens at display time, after localization lookup, so the placeholder works identically in every language.

:::warning
There must be exactly one character profile in your project with `IsPlayerCharacter` checked. If no profile has it checked, `{PlayerName}` is substituted with the literal string `"Player"` and no error is logged. If multiple profiles have it checked, the first one found is used. Set `IsPlayerCharacter` on exactly one profile and leave it unchecked on all others.
:::

---

## Inspector-only fields

Not every property of a dialogue line is set in the YAML file. The following fields exist on each line inside the `WitWeaverConversationData` asset and are configured in the Unity Inspector - they have no YAML equivalent and cannot be set from the text file.

| Field | Inspector label | What it controls |
|---|---|---|
| **UserInputMethod** | Progression Method | Whether the runner waits for the player to press advance (`UserInput`) or moves on automatically after a delay (`Timed`). Default: `UserInput`. |
| **TimeBeforeNextLine** | Time Before Next Line | Seconds to wait before advancing when Progression Method is set to `Timed`. Has no effect when `UserInput` is selected. |
| **Expression ID** | Expression | The expression to apply to the speaking character's representation for this line (e.g., `"happy"`, `"angry"`). Matches a key in the character's representation asset. |
| **Continuation Mode + options** | Continuation Mode | Whether to continue, end, branch, or present choices after this line. See [Line Continuation](../core-systems/line-continuation). |

These fields are separate from the YAML by design: **YAML stays as readable prose** - just characters speaking dialogue. **Timing, expressions, and branching logic belong in the asset graph**, where they can be iterated visually without touching the source text.

---

## Multiple conversations in one file

A single YAML file can contain as many conversations as you like. Each top-level key is an independent conversation. You must create a separate `WitWeaverConversationData` asset for each key and set its **Conversation Key** field accordingly.

```yaml
ShopGreeting:
  - CharacterID: "Merchant"
    LineID: "merchant_hello"
    LocalizedDialogue:
      EN: "Welcome to my shop!"

ShopFarewell:
  - CharacterID: "Merchant"
    LineID: "merchant_goodbye"
    LocalizedDialogue:
      EN: "Come back anytime!"

ShopOutOfStock:
  - CharacterID: "Merchant"
    LineID: "merchant_out_of_stock"
    LocalizedDialogue:
      EN: "Sorry, I'm all out of that item."
```

Grouping related short conversations in one file keeps the source directory tidy. A good rule of thumb is to group conversations by scene or NPC.

---

:::info[For Advanced Users]
The `WitWeaverYamlParser` uses YamlDotNet with `IgnoreUnmatchedProperties` enabled. Any key present in your YAML that does not correspond to a known field (for example, a comment-as-field or a future field you are testing) is silently ignored - it will not cause a parse error.

Language code normalization happens at parse time: all language codes are lowercased before being stored in the `DialogueLineInfo.LocalizedDialogue` dictionary. The `WitWeaverDialogueLocalizationHandler.GetLocalizedDialogue()` method also lowercases the requested language code before performing the dictionary lookup. This means a mismatch between the casing in your YAML (`EN`) and the casing in `WitWeaverSettings` (`en`) is handled transparently - they will always match.
:::
