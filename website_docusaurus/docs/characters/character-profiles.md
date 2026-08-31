---
sidebar_position: 1
title: Character Profiles
---

# Character Profiles

A `WitWeaverCharacterProfileBaseData` ScriptableObject represents one character in WitWeaver. It defines who the character is, what their display name and color are, and which visual representations they have available. Every character that speaks in a conversation must have a profile.

---

## Creating a Character Profile

Right-click in the **Project** panel → **Create → WitWeaver → Character Profile**.

Name the asset something descriptive, typically the character's in-world name or a short identifier. The asset name does not need to match any ID, but keeping them consistent avoids confusion.

---

## Inspector Fields

| Field | Description |
|---|---|
| **Character Name** | Display name shown in the dialogue UI (this is what players see). Can include spaces, punctuation, and unicode. |
| **Character ID** | Unique identifier used in YAML and conversation data. Must match the `CharacterID` field in your YAML exactly. Case-sensitive. |
| **Character Name Color** | Color used for this character's name label in the dialogue UI. Passed through to your `WitWeaverUIFoundation` subclass as `primaryProfile.CharacterNameColor`. |
| **Character Description** | Optional free-text notes for your team's reference. Not shown at runtime. |
| **Is Player Character** | Mark this for the player-controlled character. Enables `{PlayerName}` substitution in dialogue text. Exactly one profile per conversation should have this checked. |
| **Representations** | A list of `RepresentationPair` entries, each pairing a variant name with a `CharacterRepresentationBase` asset. See [Character Representations](character-representations). |

---

## Character ID Rules

The **Character ID** is the most important field on this asset. It is the link between your YAML file and the runtime character data.

- Must exactly match the `CharacterID` value in your YAML dialogue lines
- Is **case-sensitive** - `guard`, `Guard`, and `GUARD` are treated as three completely different characters
- Must be unique across all characters participating in a given conversation
- Should contain only alphanumeric characters and underscores - avoid spaces and special characters to prevent subtle matching issues

:::tip
Use short, lowercase IDs with underscores: `town_guard`, `merchant`, `player`. Avoid spaces and special characters. Copy-paste IDs between YAML and profile assets rather than typing them twice; a single character typo causes a silent mismatch.
:::

:::warning
A mismatched Character ID (wrong case, typo, or a profile simply missing from the conversation's participant list) causes WitWeaver to log a warning at runtime and skip character resolution for that line. The dialogue still advances, but the speaker will appear as unknown and no character representation will be displayed. If a character's portrait or name never appears, this is the first thing to check.
:::

---

## Adding Profiles to a Conversation

Character profiles do not automatically apply to every conversation. You must explicitly list which characters participate in each `WitWeaverConversationData` asset.

1. Select your `WitWeaverConversationData` asset in the Project panel.
2. In the Inspector, find the **Conversation Participant Profiles** list.
3. Click **+** and drag the character's profile asset into the new slot.
4. Repeat for every character whose `CharacterID` appears in the conversation's YAML.

Every `CharacterID` referenced in the YAML must have a matching profile in this list. WitWeaver resolves speaker data at parse time; missing profiles produce a warning and leave the speaker unresolved for affected lines.

:::note
The **Conversation Participant Profiles** list is on the `WitWeaverConversationData` asset, not on the `WitWeaver` component. It is scoped per conversation, so two different conversations can have different participant sets even if they share some characters.
:::

---

## Player Character and `{PlayerName}` Substitution

Mark exactly one character profile per conversation as **Is Player Character**. WitWeaver uses this profile's **Character Name** as the substitution value for the `{PlayerName}` token in any dialogue text.

For example, if the player's character name is `Alex` and a line contains:

```yaml
- CharacterID: merchant
  LocalizedDialogue:
    EN: "Welcome back, {PlayerName}! Ready to trade?"
```

At runtime this becomes: `"Welcome back, Alex! Ready to trade?"`

:::note
`{PlayerName}` substitution uses the **Character Name** field of the profile marked as **Is Player Character**, not any runtime-entered player name. If you need the player to enter their own name, you must implement that separately and update the profile's Character Name at runtime before the conversation starts.
:::

:::warning
If no profile in the conversation is marked **Is Player Character**, the `{PlayerName}` token is left unreplaced in the output text. If multiple profiles are marked as **Is Player Character**, WitWeaver uses the first one it finds; the result is undefined if the order varies. Mark exactly one profile per conversation.
:::

---

## Representations List

Each entry in the **Representations** list is a `RepresentationPair` containing:

| Sub-field | Description |
|---|---|
| **Name** | A display label for this visual variant, e.g. `"Default"`, `"Armored"`, `"Disguised"`. Shown in the dialogue line's Representation dropdown. Display-only — renaming it never breaks lines that already use the entry. |
| **Representation** | A `CharacterRepresentationBase` asset that defines the sprites, prefab, or other visual data for this variant. |
| **Representation ID** | A stable GUID generated automatically for the entry (read-only). Dialogue lines reference this ID, not the name, so entries can be renamed and reordered safely. |

A character can have as many representations as you need: different outfits, alternate forms, or level-of-detail variants. The runner selects which representation to use based on each dialogue line's display settings.

For full details on creating and customizing representations, see [Character Representations](character-representations).

---

## Example Setup

A typical character profile for a town guard would look like this:

| Field | Value |
|---|---|
| Character Name | `Town Guard` |
| Character ID | `town_guard` |
| Character Name Color | A muted grey or gold |
| Is Player Character | Unchecked |
| Representations | `Default` → `TownGuardSpriteRepresentation` |

And the corresponding YAML entry:

```yaml
- CharacterID: town_guard
  LocalizedDialogue:
    EN: "Halt! State your business."
    FR: "Halte! Declinez votre identite."
```

---

## Next Steps

| I want to… | Go here |
|---|---|
| Define how a character looks (sprites, prefabs) | [Character Representations →](character-representations) |
| Set up per-line emotions and facial expressions | [Expressions →](expressions) |
| Understand all YAML dialogue line options | [YAML Format →](../yaml-reference/yaml-format) |
