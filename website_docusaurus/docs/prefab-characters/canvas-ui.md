---
sidebar_position: 2
title: Canvas UI
---

# Canvas UI

`WitWeaverSampleUICanvas` is the canvas-space dialogue UI. It handles all dialogue text, speaker names, choice buttons, and character rendering — both sprites and prefabs — inside a Unity Canvas.

Use this class when your characters live on the canvas rather than in the 3D world.

---

## Setup

### 1. Add the spawner and pool

`WitWeaverSampleUICanvas` needs a `WitWeaverPrefabRepresentationSpawner` to instantiate prefab characters and a `WitWeaverPrefabPool` to manage instances.

1. Select the GameObject that holds your `WitWeaverSampleUICanvas` component.
2. **Add Component → WitWeaverPrefabPool**. This is the instance pool; leave it at default settings to start.
3. **Add Component → WitWeaverPrefabRepresentationSpawner**. Drag the `WitWeaverPrefabPool` component into the **Pool** field.
4. Drag the `WitWeaverPrefabRepresentationSpawner` component into the **Prefab Representation Spawner** field on `WitWeaverSampleUICanvas`.

### 2. Create slot anchors

Slot anchors are `RectTransform` GameObjects inside your Canvas hierarchy. Each anchor marks a position where a prefab character can appear.

1. Inside your Canvas, create empty GameObjects to act as anchors (right-click the Canvas → **UI → Empty** or **Create Empty**).
2. Name them descriptively: `SlotLeft`, `SlotCenter`, `SlotRight`, or whatever suits your layout.
3. Position each anchor using the `RectTransform` as you would any canvas element.
4. On `WitWeaverSampleUICanvas`, expand the **Slot Anchors** list and add each anchor `RectTransform` to the list.

:::tip
The order of anchors in the list matters. When a line's display options don't specify a named slot, the UI falls back to the list index: index 0 is used for the first character, index 1 for the second, and so on. Arrange the list in the order you want characters to appear left-to-right.
:::

### 3. Assign the remaining fields

Fill in the standard dialogue UI fields on `WitWeaverSampleUICanvas`: dialogue text, speaker name, choice panel, continue button, and any history UI elements.

---

## Slot Addressing

Each character on a dialogue line can be routed to a specific slot in two ways.

### Named slots (`SlotId`)

Set the **Slot Id** field on a `DialogueLineDisplayOptions` asset to match the name of an anchor in your slot list. Named addressing takes precedence over index fallback.

Use this when your layout has semantically distinct positions (e.g., a slot specifically for the player character, always on the right) that should not shift based on how many characters are visible.

### Index fallback (`CharacterPosition`)

If no `SlotId` is set, the canvas UI uses the `CharacterPosition` value (Left, Center, Right) to select a slot by index:

| CharacterPosition | Slot index used |
|---|---|
| Left | 0 |
| Center | 1 (or last if list has ≤ 2 entries) |
| Right | 1 (or 2 if list has 3+ entries) |

If your slot list has fewer entries than the number of characters on a line, the excess characters are not rendered.

:::note
`CharacterPosition` and `SlotId` are both on `DialogueLineDisplayOptions`, which you assign per representation slot in the conversation data inspector. They're optional — if you leave them at defaults, the canvas UI falls back to list order.
:::

---

## Mixed Representations

A single dialogue line can include both sprite characters and prefab characters. `WitWeaverSampleUICanvas` handles both without additional configuration.

- **Sprite characters** are rendered into `Image` components at their assigned slot positions.
- **Prefab characters** are spawned by the `WitWeaverPrefabRepresentationSpawner` and parented under the corresponding slot anchor `RectTransform`.

Because both types share the same slot list, a line that has one sprite character and one prefab character will place the sprite into one `Image` slot and the prefab under one anchor, simultaneously.

:::warning
Make sure the prefab you assign to `PrefabCharacterRepresentationData` is designed to work inside a Canvas. Canvas prefabs typically use `RectTransform` instead of `Transform` at the root, and should be scaled appropriately for screen-space rendering. A world-space character prefab will not lay out correctly under a canvas anchor.
:::

---

## Character Persistence and Release

Prefab characters spawned by the canvas UI are managed per-slot. When a new line begins:

- If the same character is assigned to the same slot as the previous line, the existing instance is reused and `BindRepresentation` is called again to refresh the binding.
- If a different character occupies the slot, the previous instance is released back to the pool before the new one is resolved.
- Characters not referenced in a line are released when the next line begins.

At conversation end, `WitWeaverPrefabRepresentationSpawner.ReleaseAll()` is called automatically by the canvas UI, returning all active instances to the pool.

Scene-resident characters (those using `CharacterSourceMode.SceneResident` on their `PrefabCharacterRepresentationData`) are **never** released, pooled, or destroyed by the spawner. The spawner simply locates them via `WitWeaverSceneCharacterRegistry` and treats them as externally managed.

---

## Scene-Resident Characters

If a character is already present in your scene — placed by hand, spawned by your own code, or persisted across scenes — register it with `WitWeaverSceneCharacterRegistry` so the canvas UI can find it.

1. Add **WitWeaverSceneCharacterRegistry** to any GameObject in the scene.
2. Add **WitWeaverSceneCharacterRegistrant** to the scene character's root GameObject.
3. Set the **Character Id** field on the registrant to match the `SceneCharacterId` on the character's `PrefabCharacterRepresentationData`.
4. Set the `CharacterSourceMode` on the `PrefabCharacterRepresentationData` to **Scene Resident**.
5. Drag the `WitWeaverSceneCharacterRegistry` into the **Scene Character Registry** field on your `WitWeaverPrefabRepresentationSpawner`.

When the spawner resolves a scene-resident character, it looks it up in the registry and returns it directly. No spawning, parenting, or pooling occurs.

---

## Fade In and Out

If you want characters to fade in when they appear and fade out when they are released, add a **WitWeaverSimpleFade** component to your character prefab. The spawner calls `FadeIn()` when a character is resolved and `FadeOut()` when it is released.

See [Display Components → WitWeaverSimpleFade](display-components#witweaversimplefade) for configuration details.

---

## Next Steps

| I want to… | Go here |
|---|---|
| Use world-space prefab characters instead | [3D UI →](3d-ui) |
| Configure expression driving on a prefab | [Display Components →](display-components) |
| Understand sprite character representations | [Character Representations →](../characters/character-representations) |
