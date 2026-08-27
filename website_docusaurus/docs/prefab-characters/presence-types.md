---
sidebar_position: 4
title: Character Behaviours
---

# Character Behaviours

A **character behaviour** is a ScriptableObject that decides where 3D prefab characters are positioned during a conversation and how long they remain in the scene. The 3D UI calls into the behaviour at conversation start, per line, and at conversation end — the behaviour handles the rest.

All character behaviour types extend `WitWeaverCharacterBehaviour`. WitWeaver ships with seven built-in types covering the most common placement needs.

Character behaviours live on individual **configuration entries** inside a `PrefabCharacterRepresentationData` asset — not on the UI component. This means different configuration entries on the same character (for example `"CloseUp"` and `"Distant"`) can each carry a different set of behaviours, and the active set switches automatically when the entry changes between dialogue lines.

Each configuration entry holds a **list** of behaviours. At runtime all behaviours in the list are invoked together. The first behaviour that returns a non-null display drives expression application for that character.

---

## Choosing a Behaviour

| Situation | Behaviour to use |
|---|---|
| Characters are already hand-placed in the scene | `ExternalBehaviour` |
| Characters should appear at specific authored world positions | `WorldPointBehaviour` |
| A character should follow a moving scene object | `FollowTargetBehaviour` |
| Character is positioned relative to the active camera | `CameraRelativeBehaviour` |
| A scene character walks or turns to a spot at conversation start | `TransformLerpBehaviour` |
| A scene character with an Animator needs its parameters driven | `WitWeaverAnimatorBehaviour` |
| Different dialogue modes need different placement strategies | `SequencedBehaviour` |

---

## ExternalBehaviour

Characters are fully managed by the developer. WitWeaver does not spawn, position, move, or destroy anything.

**How it works:**
- `ResolvePresence()` looks up the character in `WitWeaverSceneCharacterRegistry` by ID (from `CharacterBehaviourContext.CharacterId`) and returns the `IWitWeaverCharacterDisplay` component on that GameObject.
- If the character is not found in the registry, `ResolvePresence()` returns `null`, a warning is logged to the Console identifying the missing character ID, and expression application is skipped for that character on that line.
- `OnConversationEnd()` is a no-op — cleanup is entirely your responsibility.

**Inspector fields:** None beyond the base class.

**Use when:** characters are already standing in the scene, placed by the designer or spawned by your own systems, and you want WitWeaver to drive only their expressions.

:::warning
If you use `ExternalBehaviour`, every character in your conversation must be registered with `WitWeaverSceneCharacterRegistry` at runtime before the conversation starts. If a character is missing, you will see a warning and that character's expressions will be skipped silently.
:::

---

## WorldPointBehaviour

Characters are spawned at authored world positions when they first appear in a line. Positions are authored visually in the scene using `WitWeaverSpawnPoint` markers and referenced here by string ID. Characters are cached after their first line — a character that appears on multiple lines is not re-spawned.

**How it works:**
- `OnConversationBegin()` queries `WitWeaverSpawnPointRegistry` for each configured Spawn Point ID and creates a lightweight marker `Transform` at the matching spawn point's world position and rotation.
- `ResolvePresence()` checks whether the character has already been spawned. If not, it spawns a prefab instance via the spawner and parents it under the marker at the index matching `CharacterBehaviourContext.CharacterIndex`.
- On subsequent lines with the same character, the cached `IWitWeaverCharacterDisplay` is returned directly.
- `OnConversationEnd()` releases all spawned instances via the spawner and destroys the marker Transforms.

**Inspector fields:**

| Field | Description |
|---|---|
| **World Points** | A list of Spawn Point ID entries. Index 0 is used for the first character on a line (`CharacterIndex == 0`), index 1 for the second, and so on. |

**Setting up spawn points:**

Position characters visually in the scene using `WitWeaverSpawnPoint` markers:

1. Add **WitWeaverSpawnPointRegistry** to any GameObject in the scene (required once per scene).
2. Create a new empty GameObject at the desired character position and name it descriptively (e.g. `SpawnPoint_Guard`).
3. Add **WitWeaverSpawnPoint**. Set a unique **Spawn Point ID** (e.g. `"Guard_Post"`).
4. In the `WorldPointBehaviour` asset inspector, add an entry to **World Points** and type the matching Spawn Point ID.

The scene-view gizmo on each `WitWeaverSpawnPoint` shows a sphere, a forward direction ray, and a label so you can see all authored positions at a glance.

:::tip
Because spawn points are GameObjects in the scene, you position characters exactly like any other object — using the standard Transform handles. No manual coordinate entry needed.
:::

:::warning
If a Spawn Point ID has no matching `WitWeaverSpawnPoint` active in the scene when the conversation starts, a warning is logged and that character slot is skipped. The `WorldPointBehaviour` inspector also highlights unresolved IDs while in edit mode.
:::

---

## FollowTargetBehaviour

A character is spawned at conversation start and follows a scene `Transform` for the duration of the conversation. The follow target is resolved via `WitWeaverSceneCharacterRegistry` by ID.

**How it works:**
- `ResolvePresence()` looks up the **follow target** Transform in `WitWeaverSceneCharacterRegistry` by the configured ID. The character prefab is then spawned via the spawner and a `WitWeaverFollowTarget` component is attached to keep it tracking the target.
- Each frame the spawned instance is moved to match the target's position plus the configured offset.
- `OnConversationEnd()` fires any configured completion Animator triggers, then releases the spawned instance.

**Inspector fields — per slot:**

| Field | Description |
|---|---|
| **Target Scene Id** | Registry ID of the follow target — the scene Transform the spawned character tracks. Must be registered with `WitWeaverSceneCharacterRegistry`. |
| **Offset** | A world-space offset applied to the follow target's position. Useful for placing the character slightly ahead of or beside the target. |
| **Animator Parameter Name** | Optional. Animator parameter to set on the spawned character when following begins. Leave empty to skip. |
| **Parameter Type** | `Bool`, `Int`, `Float`, or `Trigger`. |
| **Bool / Int / Float Value** | Value to apply depending on the selected parameter type. |
| **Completion Trigger Name** | Optional. Animator trigger fired when the conversation ends, before the character is released. |

**Use when:** a companion character should stay near the player during dialogue, or a character should appear attached to a moving vehicle or platform.

---

## CameraRelativeBehaviour

A character is positioned at an authored offset from the active camera. Useful for first-person games or VR, where characters should always appear at a consistent position in front of the player regardless of where they are in the world.

**How it works:**
- `ResolvePresence()` resolves the current camera (via `Camera.main`) and spawns the character prefab.
- The character's position is calculated from the camera's position and forward/right/up vectors using the authored offset values.
- Position can be set once at conversation start or updated every frame, depending on the **Positioning Mode** setting.
- `OnConversationEnd()` releases the spawned instance.

**Inspector fields — per slot:**

| Field | Description |
|---|---|
| **Forward Distance** | How far in front of the camera the character appears, in metres. |
| **Lateral Offset** | Left/right offset from camera centre. Negative values place the character to the left. |
| **Height** | Up/down offset from camera position. |

**Inspector fields — shared:**

| Field | Description |
|---|---|
| **Positioning Mode** | `Once` — position is set at spawn only. `Continuous` — position tracks the camera every frame via a runtime component. |

**Use when:** the game is first-person or VR, or whenever a character's screen position should stay fixed relative to the player's view rather than a world coordinate.

---

## WitWeaverAnimatorBehaviour

A scene-resident character with an Animator. The behaviour resolves the character via `WitWeaverSceneCharacterRegistry` and returns it to the expression pipeline. Expression application is handled by a `WitWeaverAnimatorDisplay` component on the scene object.

**How it works:**
- `ResolvePresence()` looks up the character via `WitWeaverSceneCharacterRegistry` and returns its `IWitWeaverCharacterDisplay` component. This is identical to `ExternalBehaviour`.
- `OnConversationEnd()` is a no-op.

**Inspector fields:** None beyond the base class.

`WitWeaverAnimatorBehaviour` is a named variant of `ExternalBehaviour` provided as a clarity signal in the Project panel. All Animator parameter configuration lives on the `WitWeaverAnimatorDisplay` component on the character prefab.

**Use when:** characters are fully animated scene objects with existing Animator controllers, and you want WitWeaver to set Animator parameters in response to dialogue expression changes.

:::note
Both `WitWeaverAnimatorBehaviour` and `ExternalBehaviour` resolve scene-resident characters via the registry and return their `IWitWeaverCharacterDisplay`. Use `WitWeaverAnimatorBehaviour` as a clarity signal that the character uses `WitWeaverAnimatorDisplay`; use `ExternalBehaviour` when the character uses any other display component.
:::

---

## TransformLerpBehaviour

A scene-resident character moves to an authored target position when a conversation begins and returns to their original position when it ends. Useful for an NPC who turns to face the player or walks a few steps to a conversation spot.

**How it works:**
- `ResolvePresence()` looks up the character via the registry, records their current world position and rotation, then smoothly interpolates to the authored target over a configurable duration using a runtime `WitWeaverTransformLerp` component.
- Slots are matched by `CharacterId` first; index is used as a fallback.
- `OnConversationEnd()` interpolates the character back to their original position and rotation.

**Inspector fields — per slot:**

| Field | Description |
|---|---|
| **Scene Character Id** | The registry ID of the scene character to move. |
| **Target Position** | The world position to move to at conversation start. |
| **Target Euler Rotation** | The world rotation to adopt at conversation start, in Euler angles. |
| **Duration** | Time in seconds for the lerp. `0` = instant. Applied both at conversation start and end. |
| **Animator Parameter Name** | Optional. Animator parameter to set at the start of movement. Leave empty to skip. |
| **Parameter Type** | `Bool`, `Int`, `Float`, or `Trigger`. |
| **Bool / Int / Float Value** | Value to apply depending on the selected parameter type. |
| **Completion Trigger Name** | Optional. Animator trigger fired when the lerp completes. |

**Use when:** an NPC should step forward or turn to face the player at conversation start, then return to their idle position when the conversation ends.

---

## SequencedBehaviour

Wraps a list of other behaviour assets and selects between them based on a round-robin counter. Useful when the same scene has multiple dialogue modes with different placement needs.

**How it works:**
- At `OnConversationBegin()`, `SequencedBehaviour` increments an internal counter and selects the sub-behaviour at `counter % behaviours.Count`. All lifecycle calls are forwarded exclusively to the selected sub-behaviour, which operates exactly as if it were assigned directly.

**Inspector fields:**

| Field | Description |
|---|---|
| **Behaviours** | An ordered list of `WitWeaverCharacterBehaviour` assets to cycle through. Wraps around when the list is exhausted. |

**Use when:** the same character has different placement behaviour depending on which conversation is active, or when you want to swap behaviour types between runs without changing the configuration entry.

---

## Writing a Custom Behaviour

Extend `WitWeaverCharacterBehaviour` (a `ScriptableObject`) to create your own placement strategy:

```csharp
using WolfstagInteractive.WitWeaver;
using UnityEngine;

[CreateAssetMenu(menuName = "WitWeaver/Character Behaviour/My Custom Behaviour")]
public class MyCustomBehaviour : WitWeaverCharacterBehaviour
{
    public override IWitWeaverCharacterDisplay ResolvePresence(
        PrefabCharacterRepresentationData representation,
        CharacterBehaviourContext context,
        WitWeaverPrefabRepresentationSpawner spawner)
    {
        // Spawn, locate, or return a character display here.
        // Return null to skip expression application for this character on this line.

        // context.CharacterId            — CharacterID from the conversation participant
        // context.CharacterIndex         — zero-based index of this character on the current line
        // context.ConfigurationEntryName — which configuration entry the UI resolved for this line
        // context.DisplayOptions         — per-line display overrides (scale, flip), or null

        var display = spawner.SpawnAndBind(
            representation,
            context.ConfigurationEntryName,
            context.CharacterId,
            mySpawnTransform);
        return display;
    }

    public override void OnConversationBegin()
    {
        // Optional. Set up scene objects, markers, or runtime state here.
    }

    public override void OnConversationEnd()
    {
        // Optional. Release spawned instances, destroy markers, restore scene state here.
    }
}
```

Because character behaviours are ScriptableObjects, they cannot hold direct serialized references to scene objects. Use `WitWeaverSceneCharacterRegistry` or `WitWeaverSpawnPointRegistry` to resolve scene objects by ID at runtime, or store authored data (positions, offsets, IDs) as serialized fields and resolve live references inside `OnConversationBegin()`.

---

## Next Steps

| I want to… | Go here |
|---|---|
| Set up the 3D UI to use character behaviours | [3D UI →](3d-ui) |
| Configure expression driving on a prefab | [Display Components →](display-components) |
