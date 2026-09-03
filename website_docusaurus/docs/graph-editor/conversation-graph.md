---
sidebar_position: 1
title: Conversation Graph (Experimental)
---

# Conversation Graph (Experimental)

WitWeaver™ can author conversations visually as a **node graph**, built on Unity's Graph Toolkit (a built-in editor module in Unity 6.4 and later, so it is available in every Unity version WitWeaver supports). Each dialogue line is a node; wires define ordering, player choices, intra-conversation jumps, container branches, and endings — enabling **nonlinear conversations** without splitting content across multiple assets.

:::warning[Experimental]
Unity's Graph Toolkit API is experimental and may change between Unity versions. WitWeaver isolates all graph code in its own editor assembly (`WitWeaverGraphEditor`): if the API ever breaks, only the graph tooling disappears — conversations, YAML, and the runtime are unaffected. Deleting a graph asset never breaks a conversation.
:::

---

## One asset to manage

You only ever work with the `WitWeaverConversationData` asset. Each conversation has an **authoring mode**:

- **Linear List** (default): lines are edited in the inspector, exactly as before.
- **Graph**: the node graph is the *sole* editing surface for lines and flow. The inspector hides the line list (showing a baked-line summary instead) while keeping everything else: participants, presentation, audio manifest, language preview, the Dialogue Source section (YAML/Spreadsheet), validation tools.

Behind the scenes a companion `.WitWeaverConversationGraph` file holds the canvas (Graph Toolkit requires its own file format, so it cannot live inside the `.asset` itself), but it is fully managed for you: created on convert, renamed/moved/deleted together with the conversation asset, and opened by **double-clicking the conversation asset**. You never interact with it directly.

## The three layers of truth

| Layer | Owns | Lives in |
|---|---|---|
| **YAML** | Dialogue text, character IDs, LineIDs | Your `.yml` file / embedded copy |
| **Graph** (companion file) | Topology being authored: order, jumps, choices, branches | Auto-managed beside the conversation |
| **Conversation asset** | Everything the runtime plays (lines + `LineContinuationSettings`) | `WitWeaverConversationData` |

**Baking** is the explicit commit point: it writes the graph's text/character back to the YAML (only that conversation's section — other conversations in the same file are untouched), reimports it, and writes topology into the conversation asset. Per-line data that is *not* graph-authored — character representations, dialogue actions, audio clips, presentation and progression settings — is preserved across bakes by `LineID`.

---

## Getting started

- **Existing conversation**: select the `WitWeaverConversationData` asset and click **Convert to Graph Authoring** in its inspector. The graph is generated from the current lines and continuations, and becomes the conversation's editing surface. **Revert to Linear Authoring** switches back at any time (baked lines stay intact; keep or delete the graph).
- **New conversation**: `Assets → Create → WitWeaver → Conversation With Graph` creates a starter YAML, a linked conversation asset, and its graph in one step.
- Double-click a graph-authored conversation asset to open its graph.

## Node types

| Node | Purpose |
|---|---|
| **Conversation Start** | Entry point; exactly one per graph. Its `Next` wire marks the first line. |
| **Dialogue Line** | One line. Speaker and text are shown **read-only** (title = speaker, subtitle = preview, tooltip = full text) — the YAML owns what is said; the graph owns the flow. Identity is a hidden stable `LineID`. |
| **Player Choice** | Presents choices after the incoming line. Each choice is a reorderable block with per-language labels and a `Target` wire. |
| **Container Branch** | Leaves the conversation via a `ConversationContainer` (alias, push-return supported). |
| **End Conversation** | Explicit end. An unconnected `Next` bakes as "continue to the next line in order" (sequential default) — only the last line's unconnected `Next` bakes as an end. Ending mid-flow requires wiring an End node. |

**Line lifecycle lives in the YAML, not the canvas.** Adding a Dialogue Line node in the canvas creates an empty stub on the next bake for the writer to fill in the YAML. Deleting a line node is not allowed — a deleted node raises a **bake-blocking error** ("line nodes cannot be deleted"), and running **Refresh Graph From YAML** restores it (unwired; re-connect it). To remove a line for real, delete its entry from the YAML first and then refresh, which also removes its node.

Wiring a line's `Next` to the line that follows it bakes as a plain `Continue`; wiring it anywhere else bakes as a `GoToLine` jump to that line's `LineID`. Choice targets may be a line (intra-conversation jump), a Container Branch, or an End node.

## Baking and staleness

- **Bake Graph → Conversation** (inspector button, or `Assets → WitWeaver → Bake Conversation Graph`): validates the graph, reorders the conversation's YAML section to match the graph (existing lines keep their YAML text verbatim — bake never overwrites what a writer wrote; new nodes become empty stubs), reimports, and applies topology. The inspector shows a warning whenever the graph has unbaked changes.
- **Stale YAML blocks baking.** If the YAML was edited outside the graph after the last sync (text passes, translations, spreadsheet imports), baking refuses to run — there is no "bake anyway". A dialog offers **Refresh From YAML**; refresh, review, then bake. Staleness is detected by hashing the conversation's canonical YAML *section*, so formatting-only file changes (whitespace, other conversations in the same file) never trip the gate.
- **Refresh Graph From YAML**: pulls YAML changes into the graph. Nodes are matched by `LineID`; new lines appear as unwired nodes for you to connect; lines removed from the YAML have their nodes removed; deleted nodes are restored.

## Resolved follow-ups

- **Positional line references eliminated** (pre-release): `ConversationContainer` entries now target start lines by stable `StartLineID` (picked from a dropdown) instead of a raw list index; the legacy `StartLineIndex` remains only as a deserialization fallback for older assets. With `GoToLine` also LineID-based, no system reference breaks when lines are reordered or reimported.

## Playing a graph conversation

On a `WitWeaver` runner, the **Conversation Input** has a **Graph** tab: assign the graph-authored `WitWeaverConversationData` asset — nothing else. An **Open Graph** button jumps straight to editing. Dragging a graph-authored conversation onto the input selects the Graph tab automatically. Builds never reference the graph file itself — it is editor-only and stripped from builds; the baked `WitWeaverConversationData` is what ships.
