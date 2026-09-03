---
sidebar_position: 1
title: Overview
---

# Prefab Characters

WitWeaver™ supports 3D prefab characters in addition to flat sprites. Where a sprite representation stores a set of images, a prefab representation stores a reference to a Unity prefab. The prefab is a real GameObject that can be spawned into a scene, driven by an Animator, and fully integrated into your world.

This section covers the full workflow: how prefab characters are placed, how they receive expressions, and how to choose between the two display modes WitWeaver provides.

---

## The Two Paths

Prefab characters work differently depending on whether your dialogue exists in **canvas space** or **world space**. WitWeaver ships with a dedicated UI class for each case.

```
Your Conversation
       │
       ├─── 2D / Canvas ───▶  WitWeaverSampleUICanvas
       │                         - Slot anchors on a Canvas
       │                         - Sprites and prefabs side-by-side
       │                         - Fixed screen-space positions
       │
       └─── 3D / World ────▶  WitWeaverSampleUI3D
                                 - Character Behaviour ScriptableObjects
                                 - Characters live in the scene
                                 - Placement is the behaviour's job
```

### Canvas path

Characters sit inside a Canvas. Each character slot is a `RectTransform` anchor point that the spawner parents prefab instances under. Sprite and prefab characters can both appear on the same dialogue line. The canvas UI handles both types without any extra configuration.

```
Canvas
  ├── WitWeaverSampleUICanvas
  │
  ├── Slot anchors (RectTransform)
  │     ├── SlotLeft ────▶ CharacterA (prefab instance)
  │     │                      └── WitWeaverAnimatorDisplay
  │     │
  │     └── SlotRight ───▶ CharacterB (sprite rendered into Image)
  │
  └── Dialogue Text, Speaker Name, Choices, ...
```

**Best for:** visual novel layouts, 2D games, UI-layer portraits, any setup where characters are screen-space elements.

### 3D world path

Characters live in the 3D scene. **Character behaviours** — `WitWeaverCharacterBehaviour` ScriptableObjects assigned to each character's configuration entry — handle all placement decisions: where to spawn characters, how they move, and when they are removed from the scene. Common built-in options cover authored world positions, camera-relative placement, following a moving target, and characters already standing in the scene. The UI has no slot concept and delegates all placement to the behaviours.

```
WitWeaverSampleUI3D
      │  conversation starts
      ├──▶  behaviour.OnConversationBegin()
      │          set up markers, spawn points, or store initial state
      │
      │  each dialogue line
      ├──▶  behaviour.ResolvePresence(rep, context, spawner)
      │          returns IWitWeaverCharacterDisplay
      │              └── display.BindRepresentation()
      │              └── display.ApplyExpression()
      │
      │  conversation ends
      └──▶  behaviour.OnConversationEnd()
                 release spawned instances, restore scene state
```

Behaviours are assigned per **configuration entry** on the `PrefabCharacterRepresentationData` asset. Each entry holds a list of behaviours, so a `"CloseUp"` entry and a `"Distant"` entry on the same character can each use different behaviours — and the active set transitions automatically when the entry changes between lines.

**Best for:** RPG dialogue, cinematic scenes, first-person games, VR, any setup where characters exist as world objects.

---

## Choosing a Path

| Situation | Use |
|---|---|
| Characters rendered inside a Canvas | `WitWeaverSampleUICanvas` |
| Characters exist as GameObjects in the scene | `WitWeaverSampleUI3D` |
| Mixing sprite portraits and prefab characters on the same line | `WitWeaverSampleUICanvas` |
| Characters need to move to specific world positions | `WitWeaverSampleUI3D` + `WorldPointBehaviour` |
| Characters follow the player | `WitWeaverSampleUI3D` + `FollowTargetBehaviour` |
| Characters are already placed in the scene by the developer | `WitWeaverSampleUI3D` + `ExternalBehaviour` |
| First-person or VR, character positioned relative to the camera | `WitWeaverSampleUI3D` + `CameraRelativeBehaviour` |

:::note
Both paths use the same `PrefabCharacterRepresentationData` asset and the same display components (`WitWeaverAnimatorDisplay`, `WitWeaverBlendShapeDisplay`, etc.). What differs is how the spawned character gets positioned in the world.
:::

---

## Key Components

### Shared across both paths

| Component | What it does |
|---|---|
| `PrefabCharacterRepresentationData` | ScriptableObject. Holds configuration entries, prefab references, and expression mappings. |
| `WitWeaverPrefabRepresentationSpawner` | MonoBehaviour. Spawns, pools, and resolves prefab instances. One per UI. |
| `WitWeaverPrefabPool` | MonoBehaviour. Object pool for prefab instances. Managed by the spawner. |
| `WitWeaverSceneCharacterRegistry` | MonoBehaviour. Registers scene-resident characters by ID so behaviours can find them without spawning anything. |
| `WitWeaverSceneCharacterRegistrant` | MonoBehaviour. Drop on any scene character to register it with the registry. |

### Canvas path only

| Component | What it does |
|---|---|
| `WitWeaverSampleUICanvas` | The full canvas UI: text, choices, sprite slots, and prefab slot anchors. |

### 3D path only

| Component | What it does |
|---|---|
| `WitWeaverSampleUI3D` | The world-space UI: text and choices only. Delegates character placement to behaviours on each configuration entry. |
| `WitWeaverCharacterBehaviour` | Abstract ScriptableObject base class. Subclass determines where and how characters appear. |
| `WitWeaverSpawnPoint` | MonoBehaviour scene marker. Place on any GameObject; `WorldPointBehaviour` resolves it by ID. |
| `WitWeaverSpawnPointRegistry` | MonoBehaviour. Tracks all active `WitWeaverSpawnPoint` instances by ID at runtime. |

### On the character prefab

| Component | What it does |
|---|---|
| `WitWeaverAnimatorDisplay` | Drives Animator parameters in response to expressions. Most common 3D case. |
| `WitWeaverBlendShapeDisplay` | Drives `SkinnedMeshRenderer` blend shape weights in response to expressions. |
| `WitWeaverActionOnlyDisplay` | Passthrough. Runs `BaseExpressionAction` ScriptableObjects only; adds no built-in visual change. |
| `WitWeaverSimpleFade` | Implements fade-in and fade-out on a `Renderer` or `CanvasGroup`. Drop-on, configure duration. |

---

## How Expression Application Works

Regardless of which path you use, the expression application sequence is the same:

1. The WitWeaver runner processes a dialogue line and determines which characters are present.
2. The UI calls `spawner.ResolveCharacter()` (canvas) or `behaviour.ResolvePresence()` (3D) to obtain an `IWitWeaverCharacterDisplay` for each character.
3. The UI calls `display.BindRepresentation(representationAsset)` on the returned display. The display builds an internal lookup table keyed by expression display name at this point.
4. The UI calls `display.ApplyExpression(expressionId)` with the GUID of the expression selected in the YAML line.
5. The display component translates the GUID into a concrete visual change: an Animator parameter, a blend shape weight, or nothing (if `WitWeaverActionOnlyDisplay`).
6. Separately, the UI foundation's expression-action pass invokes `PrefabCharacterRepresentationData.ApplyExpression()`, which fires any `BaseExpressionAction` ScriptableObjects attached to the expression mapping. This is a different method from the display's `ApplyExpression` in step 4 — display visuals and expression actions are two independent calls that both happen while the line is presented.

```
YAML dialogue line
  ├── CharacterID: "Guard"
  └── Expression:  "Angry"
            │
            ▼
  PrefabCharacterRepresentationData   (ScriptableObject asset)
  └── Expression Mappings
        └── "Angry"
              ├──▶  BaseExpressionAction[]        (audio, VFX, events, etc.)
              │          fired by the representation asset
              │
              └──▶  IWitWeaverCharacterDisplay    (component on the prefab)
                         └── WitWeaverAnimatorDisplay
                                 └── Animator.SetInteger("EmotionState", 2)
```

:::note
Expression display names are used as the join key between the `PrefabCharacterRepresentationData` asset and the display component's inspector mappings. The GUID is only used at runtime lookup. This means you configure display components using the same human-readable name you gave the expression in the representation asset.
:::

---

## Next Steps

| I want to… | Go here |
|---|---|
| Set up canvas-space prefab characters | [Canvas UI →](canvas-ui) |
| Set up world-space prefab characters | [3D UI →](3d-ui) |
| Understand character behaviour types and when to use each | [Character Behaviours →](presence-types) |
| Configure expression driving on a prefab | [Display Components →](display-components) |
