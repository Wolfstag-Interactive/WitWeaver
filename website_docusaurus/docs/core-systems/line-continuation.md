---
sidebar_position: 7
title: Line Continuation
---

# Line Continuation

After WitWeaver displays a dialogue line, it needs to know what to do next. Should it advance to the following line? End the conversation? Jump to a different conversation? Display a set of options for the player? The answer is controlled by the **Line Continuation Mode**, a field set on each line in the `WitWeaverConversationData` inspector, not in the YAML file itself.

:::note[Why is this in the inspector, not the YAML?]
YAML is optimised for prose: it is where a writer authors dialogue quickly and legibly in any text editor. Branching logic belongs in the asset graph where it can be wired up visually, validated by the editor, and iterated without touching source text.

In practice this means a typical workflow looks like: write all the dialogue text in YAML, import it, then open the `WitWeaverConversationData` asset in Unity to configure continuation modes and hook up any branching. For linear conversations (the majority of lines), every line defaults to `Continue` and nothing needs touching in the inspector at all.
:::

---

## Setting the continuation mode

Open a `WitWeaverConversationData` asset in the Unity Inspector. Each dialogue line entry has a **Continuation Mode** dropdown. Select the mode that describes what should happen after that line finishes displaying.

The five available modes are described below.

---

## The five modes

### Continue (default)

After this line finishes, advance to the next line in the list.

This is the default for every newly added line and requires no additional configuration. The vast majority of lines in a linear conversation use this mode.

```
Line 1 → Continue → Line 2 → Continue → Line 3 → ...
```

---

### EndConversation

After this line finishes, stop the conversation entirely. WitWeaver fires the `CompletedConversation` event, which your game code can listen to in order to trigger a cutscene, unlock a quest step, return camera control to the player, and so on.

Use `EndConversation` on the final line of every conversation that has a definitive end. If the last line in a conversation list uses the default `Continue` mode and there is no next line, WitWeaver will also fire `CompletedConversation`, but setting `EndConversation` explicitly makes the intent clear and is the recommended practice.

---

### ContainerBranch

After this line finishes, jump to a specific conversation inside a `ConversationContainer` and begin playing from a designated entry point.

`ContainerBranch` requires two additional fields to be configured in the Inspector:

| Field | Description |
|---|---|
| **Target Container** | Drag the `ConversationContainer` asset that holds the destination conversation. Must be a **Selector** container — Playlist containers can't be branch targets, and the inspector warns if one is assigned. |
| **Target Alias Or Name** | The entry inside that container to jump to, picked from a dropdown of the container's entries ("(Let container decide)" defers to the container's selection mode). |
| **Push Return Point** | If checked, the current position is saved onto the return stack before branching. |

:::note
Think of `ContainerBranch` like choosing a chapter in a choose-your-own-adventure book. The current conversation is your main story. `ContainerBranch` opens a specific chapter in another part of the book and starts reading from there. If you enable **Push Return Point**, a bookmark is placed in your main story so that when the new chapter finishes, WitWeaver automatically flips back to where you left off and keeps reading.
:::

**PushReturnPoint in detail:**

When `Push Return Point` is checked:
1. WitWeaver saves the index of the line immediately after the current one onto an internal return stack.
2. It then jumps to the target conversation in the target container.
3. That conversation plays through normally.
4. When that conversation reaches an `EndConversation` line, WitWeaver checks the return stack.
5. If the stack has entries, it pops the top entry and resumes the original conversation from that saved position, rather than firing `CompletedConversation`.

This makes it possible to build reusable sub-dialogues, for example a character's backstory explanation that can be triggered from multiple different points in your main conversation, always returning to the caller when it ends.

:::warning
If you use `ContainerBranch` without checking **Push Return Point**, control transfers permanently to the target conversation. The original conversation will never resume. Only omit `Push Return Point` when you genuinely intend a one-way branch, for example a decision that permanently changes the conversation path.
:::

---

### GoToLine

After this line finishes, jump to another line **within the same conversation**, identified by its stable `LineID`. This is the building block for nonlinear conversations: loops, skips, and hub-and-spoke dialogue that stays inside one conversation asset.

| Field | Description |
|---|---|
| **Target Line** | The line in this conversation to jump to, picked from a dropdown of the conversation's lines. |
| **Push Return Point** | If checked, the line after the current one is saved onto the return stack before jumping. |

Because the target is the line's `LineID` (not its list index), jumps stay stable when lines are reordered or when the YAML is reimported.

:::note
Reversing (the "go back one line" feature) survives **backward** jumps — after looping back to a hub line, players can still step back through the lines leading up to it. **Forward** jumps reset the reverse history (the skipped lines would be holes), as does switching conversations.
:::

---

### PlayerChoice

After this line finishes, display a set of options for the player to choose from and wait for a selection. When the player picks an option, WitWeaver either jumps to a target line in the same conversation (the choice's **Target Line**) or branches to the conversation associated with that option via a container.

`PlayerChoice` is covered in full on the [Player Choices](player-choices) page.

---

## The return stack

The return stack is an internal list maintained by the active `WitWeaver` conversation runner. It enables nested branching:

- You can push multiple return points by using `ContainerBranch + Push Return Point` within a branched conversation that was itself reached via a push.
- When each branched conversation ends, WitWeaver pops one entry from the stack and resumes from that saved position.
- The stack is cleared when `CompletedConversation` fires (i.e., when an `EndConversation` is reached and the stack is empty).

A practical example of two-level nesting:

```
Main conversation
  Line A → ContainerBranch + PushReturnPoint → jumps to "SubA"

  SubA
    Line B → ContainerBranch + PushReturnPoint → jumps to "SubB"

    SubB
      Line C → EndConversation → pops stack → resumes SubA after Line B

    Line D → EndConversation → pops stack → resumes Main after Line A

  Line E → Continue → ...
```

:::tip
The return stack supports arbitrary nesting depth. There is no enforced maximum. However, deeply nested stacks are harder to reason about. Use them deliberately and document the branching intent with comments in your YAML or in the asset name.
:::

---

## Practical patterns

### Linear conversation (no branching)

Every line uses `Continue`. The final line uses `EndConversation`.

```
Greeting → Continue
Question → Continue
Response → Continue
Farewell → EndConversation
```

### Sub-dialogue that returns

Main conversation branches to an explanation, then continues.

```
Setup → Continue
Explain more? → ContainerBranch + PushReturnPoint (branches to "Explanation")
After explanation → Continue  <- resumes here when Explanation ends
Conclusion → EndConversation
```

The "Explanation" container ends with `EndConversation`, which pops the return stack and resumes at "After explanation".

### Permanent transfer to a new conversation

Player completes a quest milestone and the game moves permanently to the post-quest conversation, with no return.

```
Quest complete line → ContainerBranch (no PushReturnPoint)
```

Control transfers to the target conversation. The original conversation is never resumed. When the new conversation ends, `CompletedConversation` fires as normal.
