---
sidebar_position: 3
title: Custom Actions
---

# Custom Actions

Custom dialogue actions let you run any game logic you want in sync with individual dialogue lines. They are `ScriptableObject` assets that extend `BaseDialogueLineAction`, so they are reusable across conversations, configurable per-asset in the inspector, and version-controlled like any other project asset.

---

## Step 1: Create the Script

Create a new C# script in your project (outside the WitWeaver™ package folder). Extend `BaseDialogueLineAction`, add a `[CreateAssetMenu]` attribute, and override the two action methods.

The simplest possible custom action looks like this:

```csharp
using System.Collections;
using UnityEngine;
using WolfstagInteractive.WitWeaver;

[CreateAssetMenu(
    menuName = "WitWeaver/Actions/Log Message",
    fileName = "LogMessageAction")]
public class LogMessageAction : BaseDialogueLineAction
{
    [SerializeField] private string _message = "Dialogue line reached!";

    protected override IEnumerator ExecuteLineAction()
    {
        Debug.Log(_message);
        yield return null;
    }

    protected override IEnumerator ExecuteOnReversedLineAction()
    {
        // Nothing to undo for a log message.
        yield return null;
    }
}
```

A more complete example that stores and restores state for reversal:

```csharp
using System.Collections;
using UnityEngine;
using WolfstagInteractive.WitWeaver;

[CreateAssetMenu(
    menuName = "WitWeaver/Actions/Flash Object Color",
    fileName = "FlashObjectColorAction")]
public class FlashObjectColorAction : BaseDialogueLineAction
{
    [SerializeField] private GameObject _targetObject;
    [SerializeField] private Color _flashColor = Color.red;
    [SerializeField] private float _holdDuration = 0.5f;

    // Instance-only state (not serialized to the asset).
    private Color _originalColor;
    private Renderer _renderer;

    public override IEnumerator ExecuteLineAction()
    {
        _renderer = _targetObject.GetComponent<Renderer>();
        if (_renderer == null)
        {
            Debug.LogError($"[FlashObjectColorAction] No Renderer found on {_targetObject.name}.");
            yield break;
        }

        _originalColor = _renderer.material.color;
        _renderer.material.color = _flashColor;
        yield return new WaitForSeconds(_holdDuration);
        _renderer.material.color = _originalColor;
    }

    public override IEnumerator ExecuteOnReversedLineAction()
    {
        if (_renderer != null)
        {
            _renderer.material.color = _originalColor;
        }
        yield return null;
    }
}
```

---

## Step 2: Set the CreateAssetMenu Attribute

The `[CreateAssetMenu]` attribute registers your action in the Project panel's right-click menu.

```csharp
[CreateAssetMenu(
    menuName = "WitWeaver/Actions/Flash Object Color",
    fileName = "FlashObjectColorAction")]
```

- **`menuName`** - the path in the right-click menu. Use `"WitWeaver/Actions/..."` so your custom actions appear alongside the built-in ones.
- **`fileName`** - the default file name when creating a new asset. Name it after the action.

---

## Step 3: Implement ExecuteLineAction

`ExecuteLineAction` is the forward action. It runs when the conversation reaches the line this action is attached to (as a before-action, it runs before the text displays; as an after-action, it runs after the player advances).

Key points:

- The method signature is `public override IEnumerator ExecuteLineAction()`.
- It must be a coroutine. Use `yield return` to wait. The runner holds here until the coroutine completes.
- Use `yield return new WaitForSeconds(n)` to pause for `n` seconds.
- Use `yield return null` to wait one frame (useful when you need state to settle before continuing).
- Use `yield break` to exit early without waiting.
- Store any state you will need for reversal as instance fields - not local variables.

---

## Step 4: Implement ExecuteOnReversedLineAction

`ExecuteOnReversedLineAction` is the undo path. It runs when the player calls `ReverseOneLine()` and the runner needs to undo this action's effects.

```csharp
public override IEnumerator ExecuteOnReversedLineAction()
{
    // Restore whatever ExecuteLineAction changed.
    if (_renderer != null)
        _renderer.material.color = _originalColor;
    yield return null;
}
```

:::warning
The base implementation of `ExecuteOnReversedLineAction` in `BaseDialogueLineAction` does nothing; it yields once and returns. If you do not override it, reversal silently does nothing. For any action that modifies visible scene state (colors, positions, active states, spawned objects), always implement this method so the scene remains consistent when the player reverses.
:::

If your action has no state to undo (logging, triggering analytics, etc.), just `yield return null` and move on.

---

## Step 5: Configure RunOnlyOncePerConversation

`RunOnlyOncePerConversation` is a serialized field on `BaseDialogueLineAction` that you set on the action asset in the inspector. When enabled, WitWeaver skips the action if the player reverses to the line and reaches it a second time during the same conversation session.

Enable it for:

- Spawning props that persist in the scene.
- Triggering quest flags or analytics events.
- Playing non-interruptible cinematics.
- Any action where running twice would cause a visible glitch.

Disable it (the default) for:

- Fade-in actions on a character that the player should see again after reversing.
- Camera moves that should reset the shot when the line is revisited.
- Any action where re-running on revisit is the correct behavior.

---

## Step 6: Create the Asset

Once your script compiles, right-click in the Project panel:

**Create → WitWeaver → Actions → Flash Object Color**

This creates an asset instance. Select it to configure the serialized fields in the inspector (`_targetObject`, `_flashColor`, `_holdDuration`). Assign the asset to a dialogue line's **Actions Before Dialogue Line** or **Actions After Dialogue Line** list.

You can create multiple asset instances from the same script; each is independently configured. One `FlashObjectColorAction` might flash red for a combat scene; another might flash yellow for a warning.

---

## Important: Instance vs Asset State

At runtime, WitWeaver calls `ScriptableObject.Instantiate()` on each action asset before executing it. Every execution gets its own in-memory copy of the asset. This means:

- Fields you set in `ExecuteLineAction` (like `_originalColor` in the example above) are stored on the **instance**, not on the **asset**. The asset's serialized data is never modified at runtime.
- If two dialogue lines reference the same action asset, each gets its own instance and they do not interfere with each other.
- After the conversation ends, the instances are garbage-collected. The asset is unchanged and ready for the next conversation.

This is why it is safe to store instance-only state in plain (non-serialized) fields: they exist only during execution.

---

## Line Action or Expression Action?

WitWeaver has two custom-action hooks, and many side effects (a sound, a particle burst, a camera
nudge) could be built with either. Pick by lifecycle, not by effect:

| | Dialogue line action (`BaseDialogueLineAction`) | Expression action (`BaseExpressionAction`) |
|---|---|---|
| Attached to | A specific line (before/after lists) | An expression mapping on a representation asset |
| Fires | On that line, in that conversation | Every time that expression is applied — on any line, in any conversation |
| Blocking | Yes — coroutine; the line waits | No — synchronous; the dialogue never pauses |
| Run-once support | `RunOnlyOncePerConversation` | None — **re-fires on back-navigation and revisits; must be idempotent** |
| Reversal | `ExecuteOnReversedLineAction` when the player steps back | None |
| Context received | None (methods take no arguments) | Full `ExpressionActionContext` (runner, conversation, line index, representation, display, expression ID) |
| Per-run instance | Yes — a fresh copy is instantiated per execution | No — the shared asset runs in place; keep it stateless |

Rules of thumb: anything that must **block the line, run exactly once, or be undone** on
back-navigation is a line action. Anything that is **re-appliable emotion state** — "whenever this
character looks angry, do X" — is an expression action.

## Extending BaseExpressionAction

```csharp
using UnityEngine;
using WolfstagInteractive.WitWeaver;

[CreateAssetMenu(menuName = "WitWeaver/Expression Actions/My Expression Action")]
public class MyExpressionAction : BaseExpressionAction
{
    public override void ExecuteAction(ExpressionActionContext context)
    {
        // context.Representation - the CharacterRepresentationBase being applied.
        // context.ExpressionId   - the expression GUID being applied.
        // context.Runtime        - the WitWeaver runner (usable to start coroutines).
        // context.Display        - the resolved prefab display, when the UI provided one; otherwise null.
        Debug.Log($"{context.ExpressionId} applied to {context.Representation.name}");
    }
}
```

Two shipped examples live in the package's `Scripts/WitWeaverExpressionActions/` folder:
`PlayOneShotAudioExpressionAction` (an audio cue tied to an emotion) and
`DebugLogExpressionAction` (a wiring check that logs every context field). Create instances via
**Create → WitWeaver → Expression Actions**, then add them to a mapping's **Expression Actions**
list on the representation asset.

The contract, in full:

- **Guaranteed to run.** `WitWeaverUIFoundation` runs the line's expression actions automatically
  after the UI renders each line — custom UIs built on the foundation need no extra wiring. (A UI
  that runs them itself via `RunExpressionActions`, as the sample UIs do on the prefab path to
  provide the display handle, is not double-run.)
- **Idempotent by contract.** Expression actions re-fire every time the expression is applied,
  including when the player navigates back or revisits a line. Treat execution as state
  application: running twice must be harmless.
- **Synchronous.** `ExecuteAction` is a void method; it cannot pause the line. For fire-and-forget
  async work, start a coroutine on `context.Runtime`.
- **Shared-asset execution.** Unlike line actions, no per-run copy is instantiated — instance
  fields persist across runs (and dirty the asset in the editor), so keep actions stateless.

:::info[For Advanced Users]
`BaseExpressionAction` and `BaseDialogueLineAction` are entirely separate hierarchies. Both are
`ScriptableObject` subclasses, but expression actions are invoked by the UI foundation as part of
applying a character's expression, while line actions are invoked by the conversation runner around
each line. You cannot assign a `BaseExpressionAction` to a line's action list, or a
`BaseDialogueLineAction` to an expression slot.
:::

---

## Checklist for New Custom Actions

Before shipping a custom action, confirm the following:

- `[CreateAssetMenu]` attribute is present and the `menuName` starts with `"WitWeaver/Actions/"`.
- `ExecuteLineAction` is marked `public override` and returns `IEnumerator`.
- The coroutine has at least one `yield` statement. A coroutine with no yield never pauses and exits immediately - which is fine, but make sure it is intentional.
- `ExecuteOnReversedLineAction` is overridden if the action changes any visible scene state.
- Any state needed for reversal is stored in instance fields, not local variables.
- `RunOnlyOncePerConversation` is set appropriately on the asset in the inspector.
- The action handles null references gracefully - do not assume scene objects are always present.
