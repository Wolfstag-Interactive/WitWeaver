---
sidebar_position: 11
title: Extending WitWeaver
---

# Extending WitWeaver

WitWeaver is built top-to-bottom for extensibility. Every layer — dialogue actions, character visuals, character placement, UI display, and save storage — uses abstract base classes or interfaces that you replace or extend without touching the core package. This page maps every extension point and links to the detailed guide for each one.

---

## Why This Matters

Out of the box, WitWeaver plays dialogue. What it does *with* that dialogue — what appears on screen, which game systems react, how character visuals update, where characters stand in the world — is entirely your code. This is intentional: WitWeaver does not know or care whether you are making a visual novel, a 3D RPG cutscene, an interactive museum exhibit, or a procedurally generated story. The extension points below are where your game logic plugs in.

---

## Extension Points at a Glance

| Extension | What you replace or extend | Docs |
|---|---|---|
| **Dialogue actions** | Before/after hooks on individual lines — trigger any game logic | [Custom Actions](dialogue-actions/custom-actions) |
| **Character representations** | How characters look — sprites, prefabs, Spine, VRM, or any visual system | [Character Representations](characters/character-representations) |
| **Animation backends** | How animated portraits play: flipbooks, Animator prefabs, Live2D, or any animation system | [Animated Representations](characters/animated-representations#writing-a-custom-animation-backend) |
| **Character behaviours** | Where 3D characters are placed and how they move — spawn points, follow targets, camera-relative, or anything else | [Character Behaviours](prefab-characters/presence-types) |
| **UI layer** | The entire dialogue display — text boxes, portraits, choice buttons, 3D panels | [Building a Custom UI](ui/building-a-ui) |
| **Save provider** | Where save data is stored — JSON file, YAML, cloud, PlayerPrefs, encrypted | [Save Providers](save-system/save-providers) |
| **Expression actions** | Per-expression logic — update an animator, blend shapes, or shader when an emotion changes | [Custom Actions](dialogue-actions/custom-actions#extending-baseexpressionaction) |

---

## Dialogue Actions — Before and After Every Line

Dialogue actions are ScriptableObject assets that fire custom coroutines before a line is displayed or after the player advances past it. This is how you connect dialogue to the rest of your game.

**Before-line actions** run before the text appears. Use them to:
- Move the camera to a new shot.
- Spawn or enable a character in the scene.
- Fade in a portrait.
- Set a quest flag.

**After-line actions** run after the player advances. Use them to:
- Trigger an animation.
- Award an item.
- Send an analytics event.
- Transition to a cutscene.

A single action asset can be reused across dozens of conversations. Create an `EnableNPC` action once, configure it per-scene, and assign it wherever you need an NPC to appear mid-dialogue.

```csharp
[CreateAssetMenu(menuName = "WitWeaver/Actions/My Custom Action")]
public class MyCustomAction : BaseDialogueLineAction
{
    [SerializeField] private GameObject _target;

    protected override IEnumerator ExecuteLineAction()
    {
        _target.SetActive(true);
        yield return new WaitForSeconds(0.5f);
    }

    protected override IEnumerator ExecuteOnReversedLineAction()
    {
        _target.SetActive(false);
        yield return null;
    }
}
```

[Full guide: Custom Actions](dialogue-actions/custom-actions)

---

## Character Representations — Any Visual System

The built-in representation types (Sprite, Prefab, and Animated) cover most 2D and 3D setups. For anything else — Spine animations, VRM avatars, VTuber rigs, dynamic texture systems, or fully procedural characters — extend `CharacterRepresentationBase` and implement its three members (`ProcessExpression`, `ApplyExpression`, `GetExpressionMappingByGuid`):

```csharp
[CreateAssetMenu(menuName = "WitWeaver/Character Representation/My Representation")]
public class MyRepresentationData : CharacterRepresentationBase
{
    public override void ApplyExpression(
        string expressionId,
        WitWeaver runtime,
        WitWeaverConversationData conversation,
        int lineIndex,
        IWitWeaverCharacterDisplay display)
    {
        // Apply the correct visual state for this expression.
        // For example: trigger an animation, update a material, swap a texture.
    }
    // ...plus ProcessExpression and GetExpressionMappingByGuid.
}
```

Inline editor previews are opt-in: implement `IEditorPreviewableRepresentation` inside an `#if UNITY_EDITOR` block only if you want one.

One profile can hold multiple representation variants — a character can have a `"Default"` sprite set, an `"Armored"` sprite set, and a `"3D Prefab"` variant all living in the same profile.

[Full guide: Character Representations](characters/character-representations)

---

## Character Behaviours — Custom Conversation Lifecycle Hooks

`WitWeaverCharacterBehaviour` is a ScriptableObject with three override points that span the entire conversation lifecycle. Extending it is not limited to placement — it is the right hook for any logic that needs to run alongside a character's involvement in a conversation: setting up animator state, registering audio listeners, enabling VFX, driving IK targets, or anything else that needs to react when a conversation begins, resolves a character per line, or ends.

The three methods are:

| Method | When it is called | What it is for |
|---|---|---|
| `OnConversationBegin()` | Once when the conversation starts, before any line is shown | Pre-compute positions, cache scene references, transition Animator state, enable components |
| `ResolvePresence(rep, context, spawner)` | Once per character per dialogue line | Return the `IWitWeaverCharacterDisplay` to use for this line — or `null` to skip expression application |
| `OnConversationEnd()` | Once when the conversation ends | Release spawned instances, restore Animator state, clean up any scene changes |

You can implement any combination of these. A behaviour that only drives an Animator at conversation boundaries doesn't need to touch `ResolvePresence` beyond returning the scene character. A behaviour that only controls placement doesn't need to interact with the Animator at all.

```csharp
using WolfstagInteractive.WitWeaver;
using UnityEngine;

[CreateAssetMenu(menuName = "WitWeaver/Character Behaviour/My Custom Behaviour")]
public class MyCustomBehaviour : WitWeaverCharacterBehaviour
{
    [SerializeField] private string _sceneCharacterId;
    [SerializeField] private string _talkingAnimParam = "IsTalking";

    // Runtime-only — not serialized, resolved fresh each conversation.
    [System.NonSerialized] private IWitWeaverCharacterDisplay _cachedDisplay;

    public override void OnConversationBegin()
    {
        // Resolve the scene character and drive any setup logic — animator state,
        // IK targets, VFX, audio, anything that needs to happen before line one.
        _cachedDisplay = null;
    }

    public override IWitWeaverCharacterDisplay ResolvePresence(
        PrefabCharacterRepresentationData representation,
        CharacterBehaviourContext context,
        WitWeaverPrefabRepresentationSpawner spawner)
    {
        // Return the same cached display on every line for scene-resident characters.
        if (_cachedDisplay != null) return _cachedDisplay;

        // context.CharacterId            — CharacterID from the conversation participant
        // context.CharacterIndex         — zero-based index of this character on the current line
        // context.ConfigurationEntryName — which configuration entry is active for this line
        // context.DisplayOptions         — per-line display overrides (scale, flip), or null

        if (!spawner.TryGetSceneResident(_sceneCharacterId, out var display))
        {
            Debug.LogWarning($"[MyCustomBehaviour] '{_sceneCharacterId}' not found in registry.");
            return null;
        }

        // Example: drive an Animator parameter when the character first appears.
        var mono = display as MonoBehaviour;
        mono?.GetComponentInChildren<Animator>()?.SetBool(_talkingAnimParam, true);

        _cachedDisplay = display;
        return display;
    }

    public override void OnConversationEnd()
    {
        // Restore any state changed during the conversation.
        var mono = _cachedDisplay as MonoBehaviour;
        mono?.GetComponentInChildren<Animator>()?.SetBool(_talkingAnimParam, false);
        _cachedDisplay = null;
    }
}
```

Because character behaviours are ScriptableObjects, they cannot hold direct serialized references to scene objects. Use `WitWeaverSceneCharacterRegistry` or `WitWeaverSpawnPointRegistry` to resolve scene objects by ID at runtime, and cache live references in `[System.NonSerialized]` fields that are cleared in `OnConversationEnd()`.

Behaviours are assigned per **configuration entry** on each character's `PrefabCharacterRepresentationData` asset. Each entry holds a list of behaviours that all run together — the first one that returns a non-null display drives expression application for that character on that line. A single entry might pair a placement behaviour with a separate animator-hook behaviour, keeping each asset focused on one concern.

[Full guide: Character Behaviours](prefab-characters/presence-types)

---

## UI Layer — Build Any Display

`WitWeaverUIFoundation` is an abstract MonoBehaviour with six virtual methods. Override the ones you need and WitWeaver calls them at the right moment:

| Method | When WitWeaver calls it |
|---|---|
| `InitializeUI(runner)` | Once when the conversation starts |
| `UpdateDialogueUI(line, text, speaker, representation, profile)` | Every time a new line is ready to display |
| `WaitForUserInput()` | Coroutine — must block until the player advances |
| `PresentChoices(options, labels, result)` | Coroutine — display choice buttons, write selection to `result.SelectedIndex` |
| `HideDialogue()` | When the conversation ends |
| `UpdateForLanguageChange(text, code)` | When the player switches language mid-conversation |

You can use Unity UI (uGUI), UI Toolkit, TextMeshPro, a custom renderer, a 3D world-space panel, or any combination. WitWeaver does not know or care what system you use.

[Full guide: Building a Custom UI](ui/building-a-ui) | [Sample UI](ui/sample-ui)

---

## Save Provider — Store Data Anywhere

The save system ships with JSON and YAML file providers. For cloud saves, PlayerPrefs, or encrypted storage, implement `IWitWeaverSaveProvider`:

```csharp
public class MyCloudSaveProvider : IWitWeaverSaveProvider
{
    public WitWeaverGameSnapshot Load(string slot) { /* ... */ }
    public void Save(string slot, WitWeaverGameSnapshot snapshot) { /* ... */ }
    public void Delete(string slot) { /* ... */ }
    public bool SlotExists(string slot) { /* ... */ }
    public List<string> GetAllSlots() { /* ... */ }
}
```

Assign your custom provider to the `WitWeaverSaveManager` asset in the Inspector.

[Full guide: Save Providers](save-system/save-providers)

---

## Expression Actions — React to Emotion Changes

For logic that should trigger specifically when a character's expression changes — updating an animator, blending facial shapes, switching materials — extend `BaseExpressionAction` instead of `BaseDialogueLineAction`. It is synchronous (no coroutine) and receives the full expression context:

```csharp
[CreateAssetMenu(menuName = "WitWeaver/Expressions/My Expression Action")]
public class MyExpressionAction : BaseExpressionAction
{
    public override void ExecuteAction(ExpressionActionContext context)
    {
        // context.Representation — the character's representation
        // context.ExpressionId   — the GUID of the expression being applied
        // context.Runtime        — the WitWeaver runner
    }
}
```

[Full guide: Custom Actions](dialogue-actions/custom-actions#extending-baseexpressionaction)

---

## Best Practices — Choosing the Right Extension Point

The most common design question when adding new logic to WitWeaver is whether it belongs in a **dialogue action** or a **character behaviour**. They overlap in capability — both are ScriptableObjects, both fire at conversation time — but they have fundamentally different scopes.

### The core distinction

| | Dialogue Action | Character Behaviour |
|---|---|---|
| **Scope** | A specific line | The full conversation |
| **Subject** | The conversation itself — story events, game state | A specific character and their presence in the scene |
| **Granularity** | Authoring per-line in the YAML | Authoring per-configuration-entry on the representation asset |
| **Runs on** | Every line it is attached to | Every line a character appears in the conversation |
| **Reuse pattern** | One asset, many conversations — "PlayVO", "SetFlag", "FadeCamera" | One asset, many characters — "WorldPointBehaviour", "FollowTarget" |
| **Has coroutine support** | Yes — can block line display until complete | No — lifecycle methods are synchronous |

### Use a dialogue action when…

- The logic is tied to a **specific moment in the script** — a camera cut, a sound cue, a flag being set, an item being awarded.
- The trigger is **editorial**: someone writing the conversation decides when it happens, line by line.
- The effect is **not specific to one character** — it could be a door opening, a lighting change, an analytics event.
- The logic needs to **block progression** until it finishes (a fade, a camera move, a timed pause). Dialogue actions support coroutines; character behaviours do not.
- The same asset will be dropped onto **many different lines across many conversations**.

**Examples:** `PlayVoiceOver`, `TriggerQuestFlag`, `MoveCameraToShot`, `FadeOutMusic`, `AwardItem`, `EnableGameObject`.

### Use a character behaviour when…

- The logic is **tied to a character's presence** across an entire conversation — not to a single line.
- The setup and teardown are **symmetric**: something that starts when the conversation begins needs to be undone when it ends (moving a character to a spot and back, enabling an Animator state and restoring it, spawning a prefab and releasing it).
- The logic needs to know **which character** is being resolved and **which configuration entry** is active — information dialogue actions do not receive.
- The effect is **invisible to the dialogue author**: the character just "works correctly" in every conversation that uses this representation without per-line configuration.
- You are managing **runtime scene state** that lives for the duration of the conversation — cached display references, marker Transforms, spawned instances.

**Examples:** `WorldPointBehaviour`, `FollowTargetBehaviour`, a custom `CrowdFormationBehaviour` that arrays extras around a main character, an `IKLookAtBehaviour` that makes a character look at a target while they are in a conversation.

### The overlap zone — and how to decide

Some effects sit in the middle. An NPC stepping forward when a conversation starts *could* be a before-line action on line 0, or it *could* be `TransformLerpBehaviour`. The right choice depends on ownership:

- If **the dialogue author decides** when and for which conversations the NPC steps forward, use a dialogue action — they author it into the YAML.
- If **the character always does this** for any conversation that uses a particular configuration entry, use a character behaviour — the rule lives on the asset, not in the script.

A useful test: **can you describe the logic without naming a specific line?** "When this character enters any conversation using the 'TalkingMode' entry, play the greeting animation and restore idle on exit" — that's a character behaviour. "On line 4 of the tavern scene, play the laughing animation" — that's a dialogue action.

### Combining both

They are not mutually exclusive. A common pattern for a cinematic scene:

- A **character behaviour** handles placing two characters at authored spawn points and restoring their positions at conversation end — this is structural and belongs on the representation asset.
- A **dialogue action** on a specific mid-conversation line triggers a gesture animation — this is editorial and belongs in the YAML.

Neither system needs to know about the other.
