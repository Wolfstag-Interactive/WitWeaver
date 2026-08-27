---
sidebar_position: 2
title: Built-in Actions
---

# Built-in Actions

WitWeaver ships with six ready-to-use dialogue actions in the `SampleActions` folder. Each is a `ScriptableObject` you create as an asset and assign to dialogue lines in the inspector. Create them via **Right-click in the Project panel → Create → WitWeaver → Actions → [Action Name]**.

---

## Enable / Disable GameObject

**Create:** Right-click → Create → WitWeaver → Actions → Enable Or Disable GameObject

Activates or deactivates a GameObject in the scene when the dialogue line is reached. The action completes in a single frame; there is no animation.

| Field | Type | Description |
|---|---|---|
| **Target GameObject** | `GameObjectReference` | The GameObject to toggle. Supports scene references resolved via `GameObjectReference`. |
| **Enable** | `bool` | `true` to activate the object, `false` to deactivate it. |
| **Continue On Error** | `bool` | When `true`, logs a warning and continues if the target cannot be found. When `false` (default), logs an error and halts the action. |

**Reversal:** `ExecuteOnReversedLineAction` is not overridden; the base implementation does nothing. If you need to restore the previous state on reversal, create a second action with the opposite `Enable` value and assign it manually, or write a custom subclass.

**Practical uses:** showing or hiding UI panels, activating trigger zones, toggling scene decorations that appear mid-conversation.

:::note
If the target GameObject is already in the desired state when the action runs, WitWeaver logs a message and skips the `SetActive` call. This prevents redundant state changes but otherwise does nothing.
:::

---

## Fade In / Out Sprite Renderer

**Create:** Right-click → Create → WitWeaver → Actions → Fade In Or Out SpriteRenderer

Fades the alpha of a `SpriteRenderer` over a configurable duration using an `AnimationCurve`. The action waits for the full fade to complete before the runner proceeds.

| Field | Type | Description |
|---|---|---|
| **Target GameObject** | `GameObjectReference` | The GameObject containing the `SpriteRenderer` to fade. |
| **Fade Type** | `FadeType` | `FadeIn` (transparent → opaque), `FadeOut` (opaque → transparent), or `Custom` (use manual alpha values). |
| **Fade Duration** | `float` (0.1–10 s) | How long the fade takes in seconds. |
| **Fade Curve** | `AnimationCurve` | Controls the alpha over normalized time. Defaults to ease-in/out. |
| **Start Alpha** | `float` (0–1) | Starting alpha. Only used when `Fade Type` is `Custom` or `Use Auto Alpha Values` is off. |
| **End Alpha** | `float` (0–1) | Ending alpha. Only used when `Fade Type` is `Custom` or `Use Auto Alpha Values` is off. |
| **Use Auto Alpha Values** | `bool` | When `true` (default), overrides Start/End Alpha based on Fade Type (FadeIn → 0→1, FadeOut → 1→0). |
| **Enable GameObject On Fade In** | `bool` | When `true`, sets the GameObject active before starting a FadeIn, so you do not need a separate Enable action. |
| **Disable GameObject On Fade Out** | `bool` | When `true`, deactivates the GameObject after a FadeOut that ends at alpha 0. |
| **Continue On Error** | `bool` | When `true`, logs a warning and exits if the target or `SpriteRenderer` cannot be found. |

**Reversal:** `ExecuteOnReversedLineAction` is not overridden; no automatic fade-back occurs on reversal. Add a second action with the opposite `FadeType` assigned to handle the reversed path, or set `RunOnlyOncePerConversation` if the fade should only happen once.

**Practical uses:** fading character portraits in and out, revealing scene elements as narration plays, transitioning between dialogue beats.

:::tip
Use **Enable GameObject On Fade In** together with **Disable GameObject On Fade Out** to fully manage a character portrait's lifecycle from a single action asset. Set the portrait's GameObject to inactive in the scene, assign this action as a before-action, and the character will appear smoothly without needing a separate Enable action.
:::

---

## Instantiate Prefab

**Create:** Right-click → Create → WitWeaver → Actions → InstantiatePrefab

Spawns a prefab into the scene when the line is reached. The instance is placed at a specified world position with a specified rotation. The action completes immediately after instantiation.

| Field | Type | Description |
|---|---|---|
| **Prefab** | `GameObject` | The prefab to instantiate. |
| **Position** | `Vector3` | World position for the instantiated object. |
| **Rotation** | `Vector3` | Euler rotation applied as `Quaternion.Euler(Rotation)`. |

**Reversal:** `ExecuteOnReversedLineAction` is not overridden; the spawned instance is not automatically destroyed on reversal. For scenarios where you need reversal support, use `RunOnlyOncePerConversation` to prevent double-spawning, or write a custom subclass that tracks the spawned reference and destroys it in the reversed action.

**Practical uses:** spawning an NPC mid-conversation, placing a quest item in the world, triggering a particle system prefab.

:::warning
The built-in Instantiate action does not track the spawned instance. If reversal matters for your use case and you need the spawned object removed when the player reverses, write a custom action that stores the reference and calls `Destroy()` in `ExecuteOnReversedLineAction()`. Set `RunOnlyOncePerConversation` on the built-in version to at least prevent double-spawning when the line is revisited.
:::

---

## Modify Transform

**Create:** Right-click → Create → WitWeaver → Actions → ModifyTransform

Finds a `Transform` in the scene by name and sets its world position, rotation, and scale to the specified values. The action completes in a single frame.

| Field | Type | Description |
|---|---|---|
| **Transform Name** | `string` | The name of the GameObject whose transform to modify. Resolved via `GameObject.Find()`. |
| **New Position** | `Vector3` | World position to apply. |
| **New Rotation** | `Vector3` | Euler rotation to apply as `Quaternion.Euler()`. |
| **New Scale** | `Vector3` | Local scale to apply. |

**Reversal:** `ExecuteOnReversedLineAction` is not overridden. The original transform values are not captured, so reversal cannot restore them automatically.

**Practical uses:** snapping a camera rig, prop, or character to a scripted position at a dialogue beat.

:::warning
`GameObject.Find()` searches the entire scene hierarchy by name every time this action runs. In scenes with large hierarchies, this has a non-trivial cost. For performance-sensitive dialogue, prefer a custom action that holds a direct `Transform` reference instead of a name string.

Also note: if more than one GameObject in the scene shares the same name, `GameObject.Find()` returns the first match, which may not be the intended target.
:::

---

## Play Audio Clip

**Create:** Right-click → Create → WitWeaver → Actions → PlayAudioClip

Plays an `AudioClip` at a world position using Unity's `AudioSource.PlayClipAtPoint`. The action **waits for the full clip to finish** before the runner proceeds; the clip's duration determines how long the action takes.

| Field | Type | Description |
|---|---|---|
| **Audio Clip** | `AudioClip` | The clip to play. |
| **Position** | `Vector3` | World position to play the clip from. |
| **Volume** | `float` (0–1) | Playback volume. |

**Reversal:** `ExecuteOnReversedLineAction` is not overridden. Audio cannot be un-played. Reversal does nothing.

**Practical uses:** one-shot sound effects that should block the conversation until complete, such as a door slamming, an explosion, or a short musical sting.

:::note
This action uses `AudioSource.PlayClipAtPoint`, which creates a temporary `AudioSource` at the specified world position for the duration of the clip. It is not the right choice for dialogue voiceover that should align with line display timing.

For character voiceover that plays alongside a displayed line, assign the `AudioClip` directly to the `clip` field on the `DialogueLineInfo` in the Conversation Data inspector. WitWeaver handles that clip's playback automatically as part of the line rendering flow, without blocking line advancement on the clip's duration.
:::

---

## Action Group

**Create:** Right-click → Create → WitWeaver → Actions → Action Group

A composite action that runs a list of other `BaseDialogueLineAction` assets in sequence. The group itself is a single action asset; assign it to a line's action list and it executes all its children one after another, each waiting for the previous to complete.

| Field | Type | Description |
|---|---|---|
| **Action Group** | `List<BaseDialogueLineAction>` | The ordered list of child actions to run. |

**Reversal:** `ExecuteOnReversedLineAction` is not overridden on the built-in group. Reversal of child actions is not automatic; each child's own reversal behavior applies only when the runner reverses through the individual actions on the line stack.

**Practical uses:** grouping a set of actions that always execute together as a logical unit, for example an "intro sequence" that enables a UI panel, fades in a character, and plays a sound effect.

:::tip
Use action groups when a single dialogue line triggers three or more actions. A group named `"TavernIntro"` that contains an enable action, a fade, and an audio cue is far easier to maintain in the inspector than six individual entries in the line's action list. Groups also make it easy to reuse the same sequence across multiple conversations by sharing the single group asset.
:::

:::info[For Advanced Users]
The `ActionGroup` list field is typed as `List<BaseDialogueLineAction>`, so you can nest groups inside groups. This is a valid pattern for building reusable action libraries with sub-groups, though deep nesting can make the inspector difficult to read. Keep nesting to one level in most cases.
:::
