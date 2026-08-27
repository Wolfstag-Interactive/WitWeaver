---
sidebar_position: 2
title: Character Representations
---

# Character Representations

A character representation defines **how a character looks** for a given dialogue line: their sprite set, 3D prefab, or any other visual configuration. Representations are ScriptableObjects that extend `CharacterRepresentationBase`.

---

## System Overview

The diagram below shows how a character profile, its representations, and expressions relate:

```
CharacterProfile ("Guard")
├── CharacterID:   "Guard"
├── CharacterName: "Town Guard"
└── Representations list
    ├── Name: "Default"  ──▶  SpriteCharacterRepresentationData
    │                             ├── Happy   ──▶  Sprite (smiling guard)
    │                             ├── Angry   ──▶  Sprite (scowling guard)
    │                             └── Neutral ──▶  Sprite (neutral guard)
    ├── Name: "Armored"  ──▶  SpriteCharacterRepresentationData
    │                             └── (different sprite set - heavy plate armor)
    └── Name: "3D Model" ──▶  PrefabCharacterRepresentationData
                                  └── Prefab  ──▶  GuardPrefab.prefab
```

At runtime, the ConvoCore runner reads each line’s character ID and desired representation name, looks up the matching entry in the profile’s Representations list, and calls `ApplyExpression()` to update the visible character display.

---

## Profiles vs. Representations

:::note
The distinction matters: a character **profile** defines *who* a character is (their name, ID, name color, and the full list of all their visual variants). A **representation** defines *how one specific visual variant looks* (the sprites for each expression, the prefab reference, or whatever your display system needs).

One profile can hold many representations. For example, a guard character might have a `"Default"` representation (normal armor), an `"Armored"` representation (heavy plate), and a `"Disguised"` representation (civilian clothes). All three are entries in the same profile’s Representations list.
:::

---

## Built-in Representation Types

ConvoCore ships with three ready-to-use representation types.

### SpriteCharacterRepresentationData

Used for 2D sprite-based characters. Holds a list of expression mappings directly on the asset.

**Creating one**: Right-click → **Create → ConvoCore → Character → Representation → Sprite Character Representation**

**What it stores**:
- A list of expression mappings, each with a display name, a stable GUID, a portrait sprite, and a full body sprite
- The runner calls `ApplyExpression()` on this representation when a line begins, which passes the correct sprite to your UI’s character display component

When the runner processes a dialogue line, it reads the line’s selected expression GUID, finds the matching entry in this representation’s mapping, and gives the sprite to your `ConvoCoreCharacterDisplayBase` subclass to render.

### PrefabCharacterRepresentationData

Used for 3D prefab-based characters or any setup where a prefab reference is more appropriate than a flat sprite.

**Creating one**: Right-click → **Create → ConvoCore → Character → Representation → Prefab Character Representation**

**What it stores**:
- A reference to a prefab that your display code can instantiate, activate, or reference
- The runner surfaces this prefab through the standard `ApplyExpression()` call; what you do with it is entirely up to your `ConvoCoreCharacterDisplayBase` implementation

### AnimatedCharacterRepresentationData

Used for portraits and full body images that play an animation instead of showing a single still sprite: blinking, talking loops, Animator-driven rigs, etc.

**Creating one**: Right-click → **Create → ConvoCore → Character → Representation → Animated Character Representation**

**What it stores**:
- A list of expression mappings, each holding up to two animations (one for the speaker portrait, one for the full body slot)
- Each animation is a payload: a flipbook (sprite frames plus a playback speed), an Animator prefab, or a custom payload type you write yourself

The included 2D canvas UI plays these animations in the same portrait and slot images used by static sprites. See [Animated Representations](animated-representations) for the full guide, including how to plug in outside animation systems like Live2D or Spine.

---

## How the Runner Selects a Representation

When a dialogue line begins, the runner performs this resolution sequence:

1. Read the line’s `CharacterID` to find the speaking character.
2. Look up that character’s profile in the conversation’s **Participant Profiles** list.
3. Read the line’s display settings to find the desired **representation variant name** (e.g., `"Armored"`).
4. Search the profile’s **Representations** list for an entry with that name.
5. Call `ApplyExpression()` on the resolved `CharacterRepresentationBase` asset, passing the line’s selected expression GUID and the target character display component.

If the representation name is blank or not found, the runner falls back to the first entry in the Representations list.

:::warning
If the Representations list is empty or the named variant cannot be found and there is no fallback entry, no expression will be applied and the character display will remain in whatever state it was in from the previous line. This does not log a hard error; it fails silently. Always ensure every profile has at least one representation entry.
:::

---

## Adding a Representation to a Profile

1. Create the representation asset (Sprite or Prefab type, or your custom type).
2. Select the character’s **Profile** asset in the Project panel.
3. In the Inspector, scroll to the **Representations** list.
4. Click **+** to add a new `RepresentationPair`.
5. Set the **Name** field (e.g., `"Default"`).
6. Drag your representation asset into the **Representation** field.

Repeat for each visual variant the character needs.

---

## Creating Custom Representation Types

:::info[For Advanced Users]
You can create your own representation type for any visual system: a spine animation controller, a dynamic texture system, a VRM avatar, or anything else.

Extend `CharacterRepresentationBase` (which is a `ScriptableObject`) and implement the following members:

```csharp
using WolfstagInteractive.ConvoCore;

[CreateAssetMenu(menuName = "ConvoCore/Character Representation/My Custom Representation")]
public class MyCustomRepresentationData : CharacterRepresentationBase
{
    // Called by the runner to apply an expression to a character display.
    public override void ApplyExpression(
        string expressionId,
        ConvoCore runner,
        ConvoCoreConversationData data,
        int lineIndex,
        ConvoCoreCharacterDisplayBase display)
    {
        // Look up your expression data by GUID and apply it to your display.
        // For example: trigger an animation, swap a material, or update a shader param.
    }

    // Returns the expression mapping object for a given GUID.
    // Used by the editor inspector to show expression previews.
    public override object GetExpressionMappingByGuid(string guid)
    {
        // Return the mapping entry that matches this GUID, or null if not found.
        return null;
    }

    // Editor-only: draws a preview of this expression in the inline inspector.
    public override void DrawInlineEditorPreview(object mapping, Rect rect)
    {
        // Use GUI/EditorGUI calls to render a preview in the given rect.
    }

    // Editor-only: returns the pixel height of the inline preview area.
    public override float GetPreviewHeight()
    {
        return 64f;
    }
}
```

If your representation needs to perform a one-time setup step before it is first used in a conversation (for example, loading assets asynchronously or acquiring a reference to a scene object), implement `IConvoCoreRepresentationInitializable`:

```csharp
public class MyCustomRepresentationData : CharacterRepresentationBase,
    IConvoCoreRepresentationInitializable
{
    public void Initialize(ConvoCore runner, ConvoCoreConversationData data)
    {
        // Called once before the first line in the conversation that uses this representation.
    }
}
```

The runner checks for this interface on every representation it resolves at the start of a conversation and calls `Initialize()` before any line is processed.
:::

---

## Representations and Expressions

Representations and expressions work together:

- The **representation** provides the raw visual data (a sprite per emotion, a prefab, an animation controller).
- The **expression** is the named emotion that selects *which* part of that visual data to use for a given line.

For example, a `SpriteCharacterRepresentationData` might contain entries for `Happy`, `Angry`, and `Neutral` expressions. When a dialogue line has the `Happy` expression selected, the runner reads the sprite mapped to `Happy` in that representation and passes it to your display.

Each representation owns its own expression list. Two representations of the same character can both have a `Happy` entry with different sprites, and each entry has its own GUID. Because a line's expression selection points at one specific representation's entry, changing the line's representation also clears its expression selection.

See [Expressions](expressions) for full details on creating and assigning expressions.

---

## Next Steps

| I want to… | Go here |
|---|---|
| Create and assign expressions to a representation | [Expressions →](expressions) |
| Understand the full character profile setup | [Character Profiles →](character-profiles) |
| Build the UI layer that renders the character display | [UI Foundation →](../ui/ui-foundation) |