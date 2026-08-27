---
sidebar_position: 3
title: Expressions
---

# Expressions

Expressions are named emotional states (`Happy`, `Angry`, `Surprised`, `Neutral`) that control what a character looks like during a specific dialogue line. Each expression maps to a visual change: a different sprite, an animation, or any custom logic you provide.

Expressions are not separate assets. Each one lives as an entry inside a representation asset's **Expression Mappings** list. A representation owns its expressions, and each entry pairs a name with the visual data that representation needs.

---

## Where Expressions Live

Every built-in representation type stores its expressions the same way: a list of mapping entries directly on the asset. What each entry holds depends on the representation type.

| Representation type | What each expression entry holds |
|---|---|
| **Sprite** | A portrait sprite and a full body sprite |
| **Prefab** | Expression actions only (the prefab's display component or actions provide the visuals) |
| **Animated** | An animation for the portrait and one for the full body (flipbook frames, an Animator prefab, or a custom payload) |

Every entry, regardless of type, also has:

| Field | Description |
|---|---|
| **Display Name** | A human-readable label shown in dropdowns (`"Happy"`, `"Surprised"`, etc.). |
| **GUID** | An auto-generated, stable unique identifier. Dialogue lines store this, not the name. Read-only in the inspector. |
| **Expression Actions** | Optional `BaseExpressionAction` assets that run when the expression is applied. See below. |

To add an expression, select the representation asset and click **+** on its Expression Mappings list. The GUID is generated for you.

:::note
Prefab representations have one extra layer: a shared expression pool on the asset, plus optional per-configuration-entry overrides. When an entry defines an override with the same ID, the override wins. See [Prefab Characters](../prefab-characters/overview).
:::

---

## Names and GUIDs

The display name is for people. The GUID is the actual identity.

:::tip
Rename expressions freely. Dialogue lines reference the GUID, so renaming `"Surprised"` to `"Shocked"` changes nothing for existing lines. The new name simply appears in the dropdowns.
:::

:::warning
Deleting a mapping entry and adding a new one with the same name produces a different GUID. Any lines that referenced the old entry fall back to the first expression in the list and log a warning. Rename entries instead of deleting and recreating them.
:::

Two entries on the same representation should never share a display name. The representation inspectors warn you when this happens: a warning box appears at the top of the asset and the affected rows are highlighted. The GUIDs remain unique either way, but duplicate names make the per-line dropdown ambiguous for whoever is authoring lines.

---

## Setting Expressions on Dialogue Lines

Expressions are assigned per dialogue line in the `ConvoCoreConversationData` inspector.

1. Select your `ConvoCoreConversationData` asset.
2. Expand a dialogue line's entry.
3. Pick the character's **Representation** for that line.
4. The **Expression** dropdown fills with the display names of every expression on that representation.
5. Pick one. The line stores the matching GUID behind the scenes.

Hovering the expression field shows a small preview of the selected expression, so you can confirm the right sprite or frame without opening the representation asset.

:::note
If the dropdown shows a message instead of names: "Assign a Representation to select an Expression" means the line has no representation picked yet, and "(No Expressions)" means the chosen representation asset has an empty Expression Mappings list. Add entries to the representation first, then come back to the line.
:::

Changing the line's representation clears its expression selection, because expressions belong to a specific representation. Pick the new representation's expression afterwards.

---

## What Happens at Runtime

When a dialogue line displays:

1. The line hands its stored expression GUID to the character's representation.
2. The representation looks up the matching mapping entry and the UI applies its visuals (sets the sprites, starts the animations, or resolves the prefab display).
3. Any Expression Actions on that entry run, in list order.

If the line has no expression selected, or the GUID no longer exists on the representation, the representation falls back to the first entry in its list and logs a console warning. Keep the first entry as a sensible neutral state so the fallback always looks reasonable.

---

## Expression Actions

For logic that should fire when an expression is applied (playing a sound, spawning a particle burst, nudging the camera, setting an Animator parameter, etc.), create a ScriptableObject that extends `BaseExpressionAction` and attach it to the mapping entry's **Expression Actions** list.

```csharp
using UnityEngine;
using WolfstagInteractive.ConvoCore;

[CreateAssetMenu(menuName = "ConvoCore/Expression Actions/Play Emotion Sound")]
public class PlayEmotionSoundAction : BaseExpressionAction
{
    [SerializeField] private AudioClip _clip;

    public override void ExecuteAction(ExpressionActionContext context)
    {
        // context.Runtime        - the ConvoCore runner
        // context.Conversation   - the active conversation data
        // context.LineIndex      - which line is being shown
        // context.Representation - the representation the expression belongs to
        // context.Display        - the character display, when one exists (prefab path)
        // context.ExpressionId   - the GUID of the expression being applied

        if (_clip != null)
            AudioSource.PlayClipAtPoint(_clip, Vector3.zero);
    }
}
```

A few things to know:

- `ExecuteAction` runs immediately and does not pause the dialogue. If you need something that blocks the line (a camera move, a timed fade), use a [dialogue line action](../dialogue-actions/custom-actions) instead.
- One entry can hold several actions. They run in list order.
- The same action asset can be reused on many expressions and many characters.
- `context.Display` is filled in on the prefab path, where a character display component exists. On the sprite and animated paths it is empty, because those draw straight onto the UI images.

---

## Next Steps

| I want to… | Go here |
|---|---|
| Understand the representations that hold expression mappings | [Character Representations →](character-representations) |
| Animate portraits per expression | [Animated Representations →](animated-representations) |
| Run blocking logic on a specific line | [Custom Actions →](../dialogue-actions/custom-actions) |
| Assign expressions in YAML instead of the inspector | [YAML Format →](../yaml-reference/yaml-format) |
