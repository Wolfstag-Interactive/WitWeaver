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

At runtime, the WitWeaver runner reads each line’s character ID and selected representation (stored as the entry's stable **Representation ID**), looks up the matching entry in the profile’s Representations list, and calls `ApplyExpression()` to update the visible character display. The **Name** on each entry is display-only — it labels the entry in dropdowns, and renaming it never breaks existing lines.

---

## Profiles vs. Representations

:::note
The distinction matters: a character **profile** defines *who* a character is (their name, ID, name color, and the full list of all their visual variants). A **representation** defines *how one specific visual variant looks* (the sprites for each expression, the prefab reference, or whatever your display system needs).

One profile can hold many representations. For example, a guard character might have a `"Default"` representation (normal armor), an `"Armored"` representation (heavy plate), and a `"Disguised"` representation (civilian clothes). All three are entries in the same profile’s Representations list.
:::

---

## Built-in Representation Types

WitWeaver™ ships with three ready-to-use representation types.

### SpriteCharacterRepresentationData

Used for 2D sprite-based characters. Holds a list of expression mappings directly on the asset.

**Creating one**: Right-click → **Create → WitWeaver → Character → Representation → Sprite Character Representation**

**What it stores**:
- A list of expression mappings, each with a display name, a stable GUID, a portrait sprite, and a full body sprite
- The runner calls `ApplyExpression()` on this representation when a line begins, which passes the correct sprite to your UI’s character display component

When the runner processes a dialogue line, it reads the line’s selected expression GUID, finds the matching entry in this representation’s mapping, and gives the sprite to your `WitWeaverCharacterDisplayBase` subclass to render.

### PrefabCharacterRepresentationData

Used for 3D prefab-based characters or any setup where a prefab reference is more appropriate than a flat sprite.

**Creating one**: Right-click → **Create → WitWeaver → Character → Representation → Prefab Character Representation**

**What it stores**:
- A reference to a prefab that your display code can instantiate, activate, or reference
- The runner surfaces this prefab through the standard `ApplyExpression()` call; what you do with it is entirely up to your `WitWeaverCharacterDisplayBase` implementation

### AnimatedCharacterRepresentationData

Used for portraits and full body images that play an animation instead of showing a single still sprite: blinking, talking loops, Animator-driven rigs, etc.

**Creating one**: Right-click → **Create → WitWeaver → Character → Representation → Animated Character Representation**

**What it stores**:
- A list of expression mappings, each holding up to two animations (one for the speaker portrait, one for the full body slot)
- Each animation is a payload: a flipbook (sprite frames plus a playback speed), an Animator prefab, or a custom payload type you write yourself

The included 2D canvas UI plays these animations in the same portrait and slot images used by static sprites. See [Animated Representations](animated-representations) for the full guide, including how to plug in outside animation systems like Live2D or Spine.

---

## How the Runner Selects a Representation

When a dialogue line begins, the runner performs this resolution sequence:

1. Read the line’s `CharacterID` to find the speaking character.
2. Look up that character’s profile in the conversation’s **Participant Profiles** list.
3. Read the line’s selected **Representation ID** — the stable GUID stamped on the profile’s `RepresentationPair` entry. (The dropdown in the line inspector shows the entry’s display name but stores this ID, so renaming an entry never breaks lines.)
4. Resolve the ID against the profile’s **Representations** list via `GetRepresentation()`.
5. Call `ApplyExpression()` on the resolved `CharacterRepresentationBase` asset, passing the line’s selected expression GUID and the target character display component.

The whole sequence lives in one place: `conversationData.ResolveRepresentation(in data, fallbackCharacterId, role)` (with `ResolveSpeakerRepresentation(line)` as the speaker-slot shortcut). The runner, the built-in expression-action pass, and both sample UIs all call it, so every consumer resolves identically. The `role` names the slot semantics: `RepresentationRole.Speaker` never resolves to null while the profile has any representation (empty selection auto-assigns the first entry with a warning); `RepresentationRole.Visible` treats an empty selection as a legal "None" and returns null silently. Custom UIs should call this method rather than hand-rolling their own lookup.

If the selected ID is blank, the first entry in the Representations list is used (this means "default" by contract and does not log). If the ID is set but cannot be found — for example the representation entry was deleted — the runner still falls back to the first entry so the conversation never crashes mid-play, but it logs a warning naming the profile, the requested ID, and the substituted entry (once per missing ID per play session). If the Representations list is entirely empty, an error is logged and no visual is applied.

:::tip
Code that wants to detect a miss instead of accepting the fallback — for example a custom UI or tool — can call `profile.TryGetRepresentation(id, out var representation)`, which performs an exact ID lookup with no fallback and no logging.
:::

---

## Adding a Representation to a Profile

1. Create the representation asset (Sprite or Prefab type, or your custom type).
2. Select the character’s **Profile** asset in the Project panel.
3. In the Inspector, scroll to the **Representations** list.
4. Click **+** to add a new `RepresentationPair`. A stable **Representation ID** (GUID) is generated for the entry automatically; it is shown read-only in the Inspector and is what dialogue lines reference.
5. Set the **Name** field (e.g., `"Default"`). This is a display label only — you can rename it at any time without breaking lines that already use the entry.
6. Drag your representation asset into the **Representation** field.

Repeat for each visual variant the character needs.

---

## Creating Custom Representation Types

:::info[For Advanced Users]
You can create your own representation type for any visual system: a spine animation controller, a dynamic texture system, a VRM avatar, or anything else.

Extend `CharacterRepresentationBase` (which is a `ScriptableObject`) and implement its three abstract members:

```csharp
using WolfstagInteractive.WitWeaver;
using UnityEngine;

[CreateAssetMenu(menuName = "WitWeaver/Character Representation/My Custom Representation")]
public class MyCustomRepresentationData : CharacterRepresentationBase
{
    // Resolves an expression ID to a payload your UI knows how to render.
    // The return shape is yours to define — consumers type-test it, so return
    // your own mapping type. Representations whose visuals are bound by a
    // spawned display component may return the ID unchanged instead (this is
    // what the built-in prefab representation does).
    public override object ProcessExpression(string expressionID)
    {
        // Return your mapping entry for this ID, a sensible default, or null.
        return null;
    }

    // Called by the UI layer to apply an expression: run any attached
    // BaseExpressionAction entries and drive your visuals.
    public override void ApplyExpression(
        string expressionId,
        WitWeaver runtime,
        WitWeaverConversationData conversation,
        int lineIndex,
        IWitWeaverCharacterDisplay display)
    {
        // Look up your expression data by GUID and apply it.
        // For example: trigger an animation, swap a material, or update a shader param.
    }

    // Exact lookup of the expression mapping object for a given GUID — no
    // fallback; return null on a miss. The editor uses this to feed previews.
    public override object GetExpressionMappingByGuid(string expressionGuid)
    {
        return null;
    }
}
```

That is the whole required surface. Two optional interfaces add more:

**Inline editor preview (optional).** To show a hover preview of your expressions in the dialogue line inspector, implement `IEditorPreviewableRepresentation`. The interface only exists in the editor, so both the base-list entry and the members must sit inside `#if UNITY_EDITOR`:

```csharp
public class MyCustomRepresentationData : CharacterRepresentationBase
#if UNITY_EDITOR
    , IEditorPreviewableRepresentation
#endif
{
    // ...the three members above...

#if UNITY_EDITOR
    // Pixel height your preview needs. Return 0 to mean "nothing to preview
    // right now" — the inspector then skips the preview entirely.
    public float GetPreviewHeight() => 64f;

    // Draw the preview. `mapping` is whatever your GetExpressionMappingByGuid
    // returned for the line's selected expression — it can be null.
    public void DrawInlineEditorPreview(object mapping, Rect rect)
    {
        // Use GUI/EditorGUI calls to render into the given rect.
    }
#endif
}
```

Representations that skip this interface simply have no inline preview; everything else works normally.

**One-time setup (optional).** If your representation needs a setup step before it is first used in a conversation (for example, loading assets or acquiring a scene reference), implement `IWitWeaverRepresentationInitializable`:

```csharp
public class MyCustomRepresentationData : CharacterRepresentationBase,
    IWitWeaverRepresentationInitializable
{
    public void Initialize()
    {
        // Called once when the conversation's dialogue data is initialized.
    }
}
```

The runner checks for this interface on every representation in the conversation's participant profiles and calls `Initialize()` before any line is processed.
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