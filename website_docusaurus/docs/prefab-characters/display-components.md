---
sidebar_position: 5
title: Display Components
---

# Display Components

A display component is a `MonoBehaviour` you add to a prefab character that translates WitWeaver expression calls into concrete visual changes. When the runner applies an expression to a character, the display component is what decides what actually happens on screen: an Animator state change, a blend shape transition, or a custom action.

All display components implement `IWitWeaverCharacterDisplay` and extend `WitWeaverCharacterDisplayBase`.

---

## Responsibility Split

Display components and `BaseExpressionAction` ScriptableObjects cover different aspects of expression application. They are complementary, not mutually exclusive.

| Responsibility | Handled by |
|---|---|
| Animator parameter changes | `WitWeaverAnimatorDisplay` |
| Blend shape weight changes | `WitWeaverBlendShapeDisplay` |
| Audio, particles, custom effects | `BaseExpressionAction` on the representation asset |
| Any combination of the above | Both, simultaneously |

When a line is processed, the display component fires first, then any `BaseExpressionAction` assets attached to the expression mapping on `PrefabCharacterRepresentationData` run. Both happen in response to the same `ApplyExpression()` call.

---

## Expression Mapping by Display Name

All built-in display components map **expression display names** to their parameters, not GUIDs.

When `BindRepresentation()` is called, the display component reads the list of expression mappings on the bound `PrefabCharacterRepresentationData` asset. For each expression, it looks for an entry in its own inspector mapping list where `Expression Display Name` matches the expression's display name on the representation. It builds an internal GUID-to-parameter lookup from those matches.

This means:
- You configure display components by typing the same human-readable name you gave the expression in the representation asset.
- GUID values never need to be copied or handled manually.
- If a display name on the representation has no matching entry in the display component, a warning is logged at bind time identifying the missing expression by name.

---

## WitWeaverAnimatorDisplay

Drives Animator parameters in response to expression changes. This is the most common display component for 3D characters with animation controllers.

**Inspector fields:**

| Field | Description |
|---|---|
| **Animator** | The `Animator` to drive. Auto-resolved from this GameObject and its children if left empty. |
| **Expression Mappings** | A list of entries mapping expression display names to Animator parameters. |

**Each entry in Expression Mappings:**

| Field | Description |
|---|---|
| **Expression Display Name** | Must exactly match a display name in the bound representation asset. |
| **Parameter Name** | The Animator parameter to set when this expression is applied. |
| **Parameter Type** | `Bool`, `Int`, `Float`, or `Trigger`. |
| **Bool Value / Int Value / Float Value** | The value to set. Trigger parameters have no value field — the trigger is simply set. |

**Supported parameter types:**

```
Bool    → SetBool(parameterName, boolValue)
Int     → SetInteger(parameterName, intValue)
Float   → SetFloat(parameterName, floatValue)
Trigger → SetTrigger(parameterName)
```

**Example setup:**

A character has expressions named `Neutral`, `Happy`, and `Angry` in their representation asset. The `WitWeaverAnimatorDisplay` mapping list would look like:

| Expression Display Name | Parameter Name | Parameter Type | Value |
|---|---|---|---|
| `Neutral` | `EmotionState` | Int | `0` |
| `Happy` | `EmotionState` | Int | `1` |
| `Angry` | `EmotionState` | Int | `2` |

When the runner applies the `Happy` expression, the display sets `EmotionState` to `1` on the Animator, triggering whatever transition your animation controller defines for that value.

:::tip
If your Animator uses a Trigger rather than a persistent parameter — for example, a `Talk` trigger that plays a one-shot talking animation — set **Parameter Type** to `Trigger` and leave the value fields empty.
:::

---

## WitWeaverBlendShapeDisplay

Drives `SkinnedMeshRenderer` blend shape weights in response to expression changes. A single expression can drive multiple blend shapes simultaneously, which is required for real facial rigs where expressions are composed from several shapes.

**Inspector fields:**

| Field | Description |
|---|---|
| **Renderer** | The `SkinnedMeshRenderer` to drive. Auto-resolved from this GameObject and its children if left empty. |
| **Transition Duration** | Time in seconds to blend between weights. Set to `0` for instant snapping. |
| **Neutral Reset Indices** | Blend shape indices to reset to `0` before each expression is applied. Prevents shapes from accumulating across expression changes. |
| **Expression Mappings** | A list of entries mapping expression display names to one or more blend shape targets. |

**Each entry in Expression Mappings:**

| Field | Description |
|---|---|
| **Expression Display Name** | Must exactly match a display name in the bound representation asset. |
| **Targets** | A list of blend shape targets to drive simultaneously when this expression is applied. |

**Each target:**

| Field | Description |
|---|---|
| **Blend Shape Index** | The index of the blend shape on the `SkinnedMeshRenderer`. |
| **Target Weight** | The weight to drive the blend shape to (0–100). |

**Transition behaviour:**

When `Transition Duration` is greater than zero, the display starts a coroutine that interpolates both outgoing and incoming shapes simultaneously:

- All indices in **Neutral Reset Indices** interpolate from their current weight toward `0`.
- All targets in the expression mapping interpolate from their current weight toward their `Target Weight`.

Both sets move in parallel during the same coroutine pass, so outgoing and incoming shapes overlap naturally rather than sequencing.

**Keeping Neutral Reset Indices in sync:**

After configuring your expression mappings, right-click the `WitWeaverBlendShapeDisplay` component header in the Inspector and select **Populate Neutral Reset Indices From Mappings**. This scans all expression mapping targets, collects every unique blend shape index, and writes the result to **Neutral Reset Indices** in sorted order.

Run this again any time you add or remove expressions from the mapping list.

:::warning
Any existing prefabs configured with an earlier version of `WitWeaverBlendShapeDisplay` will have blank **Targets** lists in the Inspector, as the old single-index format is no longer supported. Re-enter the expression mappings using the new **Targets** list per expression, then run **Populate Neutral Reset Indices From Mappings** to regenerate the reset list.
:::

---

## WitWeaverActionOnlyDisplay

A minimal passthrough component. It provides a valid `IWitWeaverCharacterDisplay` on the prefab so WitWeaver can bind and call expression methods, but adds no built-in visual change of its own. All expression results come from `BaseExpressionAction` ScriptableObjects on the representation asset.

This component has two jobs:

1. **Interface surface** — satisfies the requirement for an `IWitWeaverCharacterDisplay` on the prefab without imposing any visual logic.
2. **Action delegation** — `PrefabCharacterRepresentationData.ApplyExpression()` runs the `BaseExpressionAction` assets attached to the expression mapping. Those actions are the visual response.

**No inspector fields** beyond what `WitWeaverCharacterDisplayBase` provides.

**Use when:** you want full control over expression behaviour via ScriptableObject actions and don't need built-in Animator or blend shape support. Common for characters whose expressions are handled entirely through audio, particles, events, or other custom effect systems.

:::note
`WitWeaverActionOnlyDisplay` supersedes the retired `SimplePrefabRepresentationDisplay`, which is still present for backward compatibility but marked as deprecated. New prefabs should use `WitWeaverActionOnlyDisplay`.
:::

---

## WitWeaverSimpleFade

Implements `IWitWeaverFadeIn` and `IWitWeaverFadeOut`. When the spawner resolves or releases a character, it checks for these interfaces and calls them if present. `WitWeaverSimpleFade` handles the fade by animating either a `Renderer` material's alpha or a `CanvasGroup` alpha over a configurable duration.

**Inspector fields:**

| Field | Description |
|---|---|
| **Fade Target** | `Renderer` — fades the material's `_Color` alpha. `CanvasGroup` — fades the CanvasGroup alpha. |
| **Renderer** | The `Renderer` to fade, if **Fade Target** is `Renderer`. Auto-resolved if left empty. |
| **Canvas Group** | The `CanvasGroup` to fade, if **Fade Target** is `CanvasGroup`. |
| **Fade In Duration** | Time in seconds for the fade-in. |
| **Fade Out Duration** | Time in seconds for the fade-out. |

:::note
`WitWeaverSimpleFade` is not called for scene-resident characters. The spawner only calls fade interfaces on instances it owns — characters that were spawned from the pool. If you want a scene-resident character to fade, trigger the fade yourself in response to WitWeaver's `StartedConversation` and `EndedConversation` events.
:::

---

## Minimum Valid Prefab Configurations

### Spawned character

```
MyCharacter.prefab
├── [root] GameObject
│     ├── WitWeaverAnimatorDisplay   (or BlendShapeDisplay / ActionOnlyDisplay)
│     ├── WitWeaverSimpleFade        (optional)
│     ├── Animator
│     └── ... (your own components)
```

### Scene-resident character

```
MySceneCharacter.prefab / GameObject
├── [root]
│     ├── WitWeaverAnimatorDisplay
│     ├── WitWeaverSceneCharacterRegistrant
│     ├── Animator
│     └── ... (your own components)
```

`WitWeaverSceneCharacterRegistrant` registers the object with `WitWeaverSceneCharacterRegistry` automatically on `OnEnable`. No code is required.

---

## Writing a Custom Display Component

Extend `WitWeaverCharacterDisplayBase` to create a display component for any visual system:

```csharp
using WolfstagInteractive.WitWeaver;
using UnityEngine;

public class MyCustomDisplay : WitWeaverCharacterDisplayBase
{
    // Called when a representation asset is bound.
    // Build any runtime lookups you need here.
    public override void BindRepresentation(CharacterRepresentationBase representationAsset)
    {
        base.BindRepresentation(representationAsset);

        var prefabRep = representationAsset as PrefabCharacterRepresentationData;
        if (prefabRep == null) return;

        // Build your lookup table using prefabRep.ExpressionMappings.
        // Each entry has a .DisplayName and an .ExpressionID (GUID).
    }

    // Called when an expression should be applied.
    // Use the GUID to look up whatever you stored in BindRepresentation.
    public override void ApplyExpression(string expressionId)
    {
        // Apply your visual change here.
    }
}
```

`WitWeaverCharacterDisplayBase` handles scale and flip logic (from `ApplyDisplayOptions`) for you. Override `ApplyDisplayOptions()` if your component needs additional behaviour when display options change, but call `base.ApplyDisplayOptions(options)` to preserve the default scale and flip handling.

---

## Next Steps

| I want to… | Go here |
|---|---|
| Place display components in context with the full workflow | [Overview →](overview) |
| Understand how expression assets are created | [Expressions →](../characters/expressions) |
| Build the UI layer that calls these components | [Canvas UI →](canvas-ui) or [3D UI →](3d-ui) |
