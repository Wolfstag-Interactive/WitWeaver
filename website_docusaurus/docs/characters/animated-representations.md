---
sidebar_position: 4
title: Animated Representations
---

# Animated Representations

An `AnimatedCharacterRepresentationData` asset lets a character's portrait and full body image play an animation instead of showing a single still sprite. The portrait can blink, breathe, talk, or loop any effect you can author, and it displays in the exact same portrait and slot images the static sprite representation uses.

Each expression on the asset (Happy, Angry, Neutral, etc.) holds up to two animations: one for the speaker portrait and one for the full body slot. When a dialogue line selects that expression, the UI starts the matching animations automatically.

---

## What Can Be Animated

Out of the box, ConvoCore ships two animation types (called payloads):

| Payload | What it is | Good for |
|---|---|---|
| **Flipbook** | A list of sprites played one after another at a speed you choose | Blinking portraits, talking loops, pixel art animation, simple idle motion, etc. |
| **Animator Prefab** | A UI prefab with its own Animator that gets placed inside the portrait or slot area | Multi-part rigs, animations that move or scale pieces, anything authored with Unity's animation window |

You can also write your own payload type for other animation systems (Live2D, Spine, video, etc.). See [Writing a Custom Animation Backend](#writing-a-custom-animation-backend) below.

---

## Creating the Asset

Right-click in the **Project** panel → **Create → ConvoCore → Character → Representation → Animated Character Representation**

Then add it to a character profile the same way as any other representation:

1. Select the character's **Profile** asset.
2. In the **Representations** list, click **+**.
3. Give the entry a name (for example `"Animated"`).
4. Drag the animated representation asset into the representation field.

From that point on, dialogue lines can pick the `"Animated"` variant, and the expression dropdown on each line lists the expressions you define on this asset.

---

## Asset Fields

| Field | Description |
|---|---|
| **Use Unscaled Time** | On by default. When on, animations keep playing even while the game is paused (`Time.timeScale = 0`). Turn it off if you want portraits to freeze together with gameplay. |
| **Expression Mappings** | The list of expressions this representation supports. Each entry is described below. |

### Expression Mapping Fields

| Field | Description |
|---|---|
| **Display Name** | The human-readable name shown in dropdowns (for example `"Happy"`). The inspector warns you if two entries share a name. |
| **GUID** | An auto-generated, stable unique identifier. Dialogue lines reference this, not the name, so renaming an expression never breaks existing lines. Read-only. |
| **Portrait** | The animation played on the speaker portrait image. Choose a payload type from the dropdown, or leave it on **(None)** to skip the portrait. |
| **Full Body** | The animation played on the character's full body slot image. Same choices as Portrait. |
| **Default Display Options** | Flip and scale settings applied whenever this expression shows, unless a dialogue line overrides them. Works the same as on the sprite representation. |
| **Expression Actions** | Optional `BaseExpressionAction` assets that run when this expression is applied. Useful for side effects like sounds, particles, camera nudges, etc. |

:::tip
The two channels are independent. An expression can have an animated portrait and no full body animation, or the other way around. A channel set to **(None)** simply leaves that image alone.
:::

---

## The Flipbook Payload

The simplest way to animate: give it sprites, tell it how fast to play them.

| Field | Description |
|---|---|
| **Frames** | The list of sprites, played in order. |
| **Frames Per Second** | How many frames play each second. 8 to 12 works well for most hand-drawn loops. |
| **Loop Mode** | What happens when the last frame is reached (see below). |

**Loop modes:**

- **Loop**: starts over from the first frame. Use for idle loops, talking loops, etc.
- **Once**: plays through one time and stays on the last frame. Use for one-shot reactions like a shocked take.
- **Ping Pong**: plays forward, then backward, then forward again. Use for breathing or swaying motions where the first and last frames differ.

:::note
Author all frames of one flipbook at the same size. Frames with different sizes can appear to jump around inside the image area.
:::

The inspector shows a live preview of the flipbook right in the expression list, so you can check the motion without entering play mode.

## The Animator Prefab Payload

For animations that are more than a sprite swap, this payload places a prefab of your own inside the portrait or slot area and lets its Animator drive the visuals.

| Field | Description |
|---|---|
| **Animated Prefab** | A UI prefab whose root is a RectTransform and which contains an Animator. It is stretched to fill the portrait or slot image. |
| **Control Mode** | **Play State** (default) jumps the Animator straight to a named state. **Set Trigger** fires a trigger parameter instead. |
| **State Or Trigger Name** | The state name or trigger parameter name to use. |
| **Layer** | The Animator layer used with Play State. Leave at 0 unless your controller uses layers. |

While the prefab is showing, the image's own sprite is hidden so the two do not draw on top of each other. When the line changes or the conversation hides, the prefab is turned off and the image is restored automatically. The same prefab is reused between lines instead of being created again each time.

:::tip
Prefer **Play State** over **Set Trigger**. Triggers fired on the very first frame a prefab becomes active can be missed by the Animator. ConvoCore works around this, but Play State is the more reliable option when either would work.
:::

---

## How It Shows Up In Game

The included 2D canvas UI (`ConvoCoreSampleUICanvas`) handles animated representations with no extra setup:

- The **speaker portrait** image plays the Portrait animation of the first character on the line.
- Each character's **full body slot** image (the Left, Center, and Right slots by default) plays their Full Body animation.
- Flip and scale display options, per-line slot selection, and fade-in components all behave exactly like they do for static sprites.
- Moving to the next line, jumping back to the previous line, and switching languages mid-line are all handled for you. Animations stop cleanly and restart from the first frame when a line is shown again.

:::warning
The included 3D sample UI (`ConvoCoreSampleUI3D`) does not render animated representations. It only works with prefab representations, because 3D scenes animate characters through the character display and behaviour system instead. Use the animated representation with canvas style UIs.
:::

If you built your own UI on top of `ConvoCoreUIFoundation`, see the note for custom UIs at the end of this page.

---

## Quick Setup Example

A character named Ava with a blinking idle portrait and a talking expression:

1. Create an **Animated Character Representation** asset named `AvaAnimated`.
2. Add an expression, name it `Idle`. Set **Portrait** to **Flipbook**, drop in 4 blink frames, 8 FPS, **Loop**.
3. Add another expression, name it `Talking`. Set **Portrait** to **Flipbook** with mouth frames, **Ping Pong**.
4. Open Ava's profile asset and add `AvaAnimated` to the Representations list with the name `"Animated"`.
5. In your conversation asset, select a dialogue line spoken by Ava, choose the `"Animated"` representation, and pick `Idle` or `Talking` from the expression dropdown.
6. Press play. The portrait animates as soon as the line displays.

---

## Writing a Custom Animation Backend

The two built-in payloads are not the whole story. Any animation system can plug in by writing one class that describes the data (the payload) and one class that plays it (the playback). Nothing in ConvoCore needs to change: the payload dropdown in the inspector finds your class automatically.

This section walks through a real example: showing a **Live2D Cubism** model as an animated portrait.

### The two pieces

**A payload** is a plain serializable class that extends `AnimatedExpressionPayload`. It holds the fields the user fills in and answers two questions: "am I set up correctly?" and "create the thing that plays me."

**A playback** implements `IAnimatedExpressionPlayback`. It has three jobs: `Start()` shows the first visual state, `Tick(deltaTime)` advances the animation each frame, and `Dispose()` cleans up.

The playback never touches your UI components directly. It talks to an `IAnimatedPortraitSurface`, a small go-between with three methods:

| Method | What it does |
|---|---|
| `SetFrame(sprite)` | Shows a sprite on the portrait or slot image. |
| `GetPrefabHost()` | Returns a Transform your prefab can be placed under, or null if the current UI cannot host prefabs. |
| `SetNativeVisualVisible(visible)` | Hides or shows the image's own sprite, so your prefab and the sprite do not draw at the same time. |

Because the playback only knows the surface, the same payload works on any UI that provides one. The included canvas UI provides a surface for its uGUI images; a UI Toolkit or world-space UI would provide its own.

### Step 1: solve the rendering question first

Live2D models draw with mesh renderers, which do not render inside a Unity UI canvas on their own. The standard fix is a render texture prefab:

- The prefab root is a RectTransform with a **RawImage**.
- Inside it, on a layer of its own, sit the Cubism model and a small orthographic camera pointed at it.
- The camera renders into a **RenderTexture**, and the RawImage displays that texture.

The result is a self-contained UI prefab that shows the live model. Build and test this prefab on its own before writing any ConvoCore code. The same trick works for any world-object system you want inside a portrait (3D heads, particle portraits, etc.).

### Step 2: write the payload

```csharp
using System;
using UnityEngine;
using WolfstagInteractive.ConvoCore;

[Serializable]
public sealed class Live2DAnimationPayload : AnimatedExpressionPayload
{
    [Tooltip("UI prefab containing the RawImage, the render camera, and the Cubism model.")]
    public GameObject Live2DPortraitPrefab;

    [Tooltip("Name of the Cubism expression to apply, as set up on the model's expression list.")]
    public string ExpressionName;

    public override bool IsConfigured => Live2DPortraitPrefab != null;

    public override IAnimatedExpressionPlayback CreatePlayback(AnimatedPlaybackContext context)
        => new Live2DPlayback(this, context);

#if UNITY_EDITOR
    // Shown in inspector and hover previews. Returning null is fine;
    // you can also add a Sprite field and return a hand-picked thumbnail.
    public override Sprite GetPreviewSprite(float normalizedTime) => null;
#endif
}
```

That is the whole authoring side. As soon as this class compiles, **Live 2D** appears in the payload dropdown next to Flipbook and Animator Prefab, on every expression, with its fields drawn underneath.

### Step 3: write the playback

```csharp
using UnityEngine;
using WolfstagInteractive.ConvoCore;
// using Live2D.Cubism.Framework.Expression;

public sealed class Live2DPlayback : IAnimatedExpressionPlayback
{
    private readonly Live2DAnimationPayload _payload;
    private readonly AnimatedPlaybackContext _context;
    private GameObject _instance;

    public Live2DPlayback(Live2DAnimationPayload payload, AnimatedPlaybackContext context)
    {
        _payload = payload;
        _context = context;
    }

    public void Start()
    {
        // Ask the surface where prefabs may live. A UI that cannot host
        // prefabs returns null, and we simply do nothing.
        var host = _context.Surface.GetPrefabHost();
        if (host == null)
        {
            Debug.LogWarning("[Live2DPlayback] This UI cannot host prefab portraits.");
            return;
        }

        // InstantiateCached reuses the same instance between lines instead
        // of creating a new copy every time the expression plays.
        _instance = _context.InstantiateCached(_payload.Live2DPortraitPrefab);
        if (_instance == null) return;

        _instance.SetActive(true);

        // Hide the image's own sprite while the model is showing.
        _context.Surface.SetNativeVisualVisible(false);

        // Apply the requested Cubism expression by name.
        // var controller = _instance.GetComponentInChildren<CubismExpressionController>();
        // controller.CurrentExpressionIndex = FindExpressionIndex(controller, _payload.ExpressionName);
    }

    public void Tick(float deltaTime)
    {
        // Nothing to do: the Cubism components animate themselves.
        // A hand-rolled animation system would advance itself here instead.
    }

    public void Dispose()
    {
        if (_instance != null)
            _instance.SetActive(false);
        _context.Surface.SetNativeVisualVisible(true);
    }
}
```

### Step 4: author it like any other expression

Create an Animated Character Representation for the character, add expressions named after the emotions you set up in Live2D (`Happy`, `Sad`, etc.), and pick **Live 2D** as the Portrait payload on each one, filling in the matching expression name. Add the representation to the character's profile, select it on dialogue lines, done. The expression dropdown, hover preview, display slots, cleanup between lines, and pause behavior all come along for free.

### What you get without writing it

- The **inspector dropdown** finds every payload class in your project automatically.
- **Line advance, previous line, and conversation end** all call `Dispose()` for you, so cleanup lives in exactly one place.
- **Instance reuse**: `InstantiateCached` keeps one instance per prefab per image, so replaying an expression does not pile up copies.
- **Pause friendliness**: the delta time passed to `Tick` already respects the representation's Use Unscaled Time setting.

:::warning
Payload classes are stored inside the asset by their class name. If you rename or move a payload class later, existing assets lose that data unless you add Unity's `UnityEngine.Scripting.APIUpdating.MovedFrom` attribute with the old name. Pick names you can live with.
:::

### A note for lip sync and per-line effects

Making the mouth move while text types out is a per-line behavior, not a per-expression visual. Put that logic in a `BaseExpressionAction` on the expression's **Expression Actions** list, or a dialogue line action. See [Custom Actions](../dialogue-actions/custom-actions).

### A note for custom UIs

If you built your own UI on `ConvoCoreUIFoundation`, you have two hooks:

- **uGUI based UIs** that copied the sample canvas approach can override `RenderRepresentation` (it is virtual) and reuse `ConvoCoreAnimatedPortraitPlayer.GetOrAdd(image).Play(...)` on their own images. Remember to call `ConvoCoreAnimatedPortraitPlayer.StopOn(...)` wherever you hide images between lines.
- **Non-uGUI UIs** (UI Toolkit, world-space, custom renderers) implement `IAnimatedPortraitSurface` once for their display target and tick the playback themselves. Every payload, built-in or custom, then works on that UI unchanged.

---

## Next Steps

| I want to… | Go here |
|---|---|
| Understand representations in general | [Character Representations →](character-representations) |
| Create and assign expressions | [Expressions →](expressions) |
| Run side effects when an expression applies | [Custom Actions →](../dialogue-actions/custom-actions) |
| Build the UI layer that shows portraits | [UI Foundation →](../ui/ui-foundation) |
