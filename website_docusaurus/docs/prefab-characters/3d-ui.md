---
sidebar_position: 3
title: 3D UI
---

# 3D UI

`WitWeaverSampleUI3D` is the world-space dialogue UI. It handles dialogue text, speaker names, and choice buttons, but has no concept of character slots. Instead, it delegates all character placement to **character behaviours** — `WitWeaverCharacterBehaviour` ScriptableObjects that decide where each character goes and how long they stay there.

Use this class when your characters are GameObjects living in the scene, not elements rendered inside a Canvas.

---

## How It Works

```
WitWeaverSampleUI3D
       │
       │  conversation starts
       ▼
WitWeaverCharacterBehaviour.OnConversationBegin()
       │
       │  each line begins
       ▼
WitWeaverCharacterBehaviour.ResolvePresence(rep, context, spawner)
       │                    returns IWitWeaverCharacterDisplay
       ▼
display.BindRepresentation(representationAsset)
display.ApplyDisplayOptions(lineOptions)
display.ApplyExpression(expressionId)
       │
       │  conversation ends
       ▼
WitWeaverCharacterBehaviour.OnConversationEnd()
```

The UI calls `OnConversationBegin()` when the conversation starts, then calls `ResolvePresence()` for each character on each line to get an `IWitWeaverCharacterDisplay` to apply expressions to. When the conversation ends it calls `OnConversationEnd()`, which is where behaviours tear down spawned instances.

---

## Where Behaviours Live

Unlike a single global "presence" field, character behaviours are assigned per **configuration entry** on each character's `PrefabCharacterRepresentationData` asset. Each entry holds a list of `WitWeaverCharacterBehaviour` assets.

```
PrefabCharacterRepresentationData
  └── Configuration Entries
        ├── "Default"
        │     ├── Character Prefab: GuardPrefab
        │     └── Character Behaviours: [ WorldPointBehaviour_GuardPost ]
        │
        └── "CloseUp"
              ├── Character Prefab: GuardPrefab
              └── Character Behaviours: [ CameraRelativeBehaviour_FrontRight ]
```

This means different entries on the same character can use entirely different placement strategies. The active entry is resolved per line using the three-level resolution chain: per-line override → participant default → asset default.

---

## Setup

### 1. Create character behaviour assets

Right-click in the Project panel → **Create → WitWeaver → Character Behaviour** → choose the type that matches your scene setup. See [Character Behaviours →](presence-types) for guidance on which type to use.

### 2. Assign behaviours to configuration entries

Open your `PrefabCharacterRepresentationData` asset. In **Configuration Entries**, expand the entry you want to configure and add one or more behaviour assets to the **Character Behaviours** list.

### 3. Add the spawner and pool

`WitWeaverSampleUI3D` needs a `WitWeaverPrefabRepresentationSpawner` for behaviours that spawn characters.

1. Select the GameObject that holds your `WitWeaverSampleUI3D` component.
2. **Add Component → WitWeaverPrefabPool**.
3. **Add Component → WitWeaverPrefabRepresentationSpawner**. Drag `WitWeaverPrefabPool` into the **Pool** field.
4. Drag `WitWeaverPrefabRepresentationSpawner` into the **Prefab Representation Spawner** field on `WitWeaverSampleUI3D`.

:::note
Not all behaviours use the spawner. `ExternalBehaviour` for scene-resident characters never calls it. The spawner is a required field on `WitWeaverSampleUI3D` so that behaviours which do spawn characters — such as `WorldPointBehaviour` or `FollowTargetBehaviour` — can operate without additional setup when you switch behaviour types. If you are certain you will only ever use `ExternalBehaviour`, the spawner will simply sit idle.
:::

### 4. Assign the remaining fields

Fill in the standard dialogue UI fields: dialogue text, speaker name, choice panel, and continue button.

---

## Character Persistence Across Lines

3D characters are persistent. Once a character appears in a conversation, they remain in the scene for the entire conversation. WitWeaver does not despawn a character just because they aren't listed on the current line.

When the runner processes a line:
- Characters on that line have their expressions updated via `ApplyExpression()`.
- Characters **not** on that line are left exactly as they are — their last applied expression remains active.

Despawning is a conversation-end operation. Each behaviour handles it in `OnConversationEnd()`.

---

## No Slot System

`WitWeaverSampleUI3D` does not have slot anchors, a slot list, or any concept of Left/Center/Right positioning. The behaviours are the only thing that decides where characters stand.

`DialogueLineDisplayOptions` fields like `CharacterPosition` and `SlotId` are ignored by the 3D UI. The `DisplayOptions` struct is available to behaviours via `CharacterBehaviourContext` if a behaviour wants to read flip or scale data, but placement is never driven by those fields.

---

## The CharacterBehaviourContext

When `WitWeaverSampleUI3D` calls `ResolvePresence()`, it passes a `CharacterBehaviourContext` struct alongside the representation and spawner:

| Field | Type | Description |
|---|---|---|
| `CharacterIndex` | `int` | Zero-based index of this character in the current line's representation list. |
| `TotalCharacters` | `int` | Total number of characters on this line. |
| `CharacterId` | `string` | The CharacterID from the conversation participant. Used for registry lookups and caching. |
| `ConfigurationEntryName` | `string` | The resolved configuration entry name for this line. Null or empty means use the asset default. |
| `DisplayOptions` | `DialogueLineDisplayOptions` | The line's display options, or `null` if none were set. |

Behaviours can use any of these fields — for example, `WorldPointBehaviour` uses `CharacterIndex` to select which authored spawn point to use, and `ExternalBehaviour` uses `CharacterId` to look up the character in the scene registry.

---

## ResolvePresence Returns Null

Some behaviours return `null` from `ResolvePresence()` — for example, `ExternalBehaviour` for a character that isn't registered in the scene registry. When this happens:

- The spawner is not called.
- No parenting occurs.
- No display component is bound.
- Expression application is skipped for that character, with a warning in the Console.

This is intentional and safe. The warning tells you which character was skipped and why, so you can decide whether to register the character or adjust the behaviour configuration.

---

## Scene-Resident Characters

If your characters are already in the scene, use `ExternalBehaviour` or `TransformLerpBehaviour` and register each character with `WitWeaverSceneCharacterRegistry`.

1. Add **WitWeaverSceneCharacterRegistry** to any GameObject in the scene.
2. Add **WitWeaverSceneCharacterRegistrant** to the scene character's root GameObject.
3. Set the **Character Id** on the registrant to match the character ID used in the conversation YAML.

No further configuration is needed. `WitWeaverPrefabRepresentationSpawner` finds the registry automatically via `WitWeaverSceneCharacterRegistry.Instance`. The **Scene Character Registry** field on the spawner is optional — only assign it when you have multiple registries in the scene and need to target a specific one.

Scene-resident characters are never spawned, pooled, or destroyed by the 3D UI. No source-mode flag is required on the representation asset; the spawner always checks the scene registry by character ID first and falls back to spawning a prefab only if no match is found.

---

## Spawn Timing

Each participant slot in the conversation data has a **Spawn Timing** setting:

| Setting | Behaviour |
|---|---|
| `OnConversationBegin` | The character's behaviours are activated as soon as the conversation starts, before any line is shown. |
| `OnFirstAppearance` | The character's behaviours are activated on the first line where that character appears. |

Use `OnConversationBegin` when characters should already be in position when the first line plays. Use `OnFirstAppearance` when a character should only appear mid-conversation.

---

## Next Steps

| I want to… | Go here |
|---|---|
| Understand which character behaviour type fits your setup | [Character Behaviours →](presence-types) |
| Configure expression driving on a prefab | [Display Components →](display-components) |
| Use canvas-space prefab characters instead | [Canvas UI →](canvas-ui) |
