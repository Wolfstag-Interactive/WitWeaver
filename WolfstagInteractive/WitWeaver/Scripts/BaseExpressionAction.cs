// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Abstract base ScriptableObject for custom expression actions. Extend this class to run
    /// game logic whenever a character expression is applied to a line — for example, triggering
    /// an animation, particle effect, or audio cue when a specific emotion is displayed.
    ///
    /// Contract:
    /// <list type="bullet">
    /// <item><b>Guaranteed to run.</b> <see cref="WitWeaverUIFoundation"/> runs the actions for
    /// every visible character after the UI renders each line — a custom UI subclass does not
    /// need to do anything for them to fire.</item>
    /// <item><b>Idempotent by contract.</b> Actions re-fire every time the expression is applied,
    /// including when the player navigates back to a previous line or revisits one. Treat
    /// execution as state application: running twice must be harmless. There is no run-once gate
    /// and no reverse hook — logic that must block the line, run exactly once, or be undone on
    /// back-navigation belongs in a <see cref="BaseDialogueLineAction"/> instead.</item>
    /// <item><b>Executed on the shared asset.</b> Unlike line actions, no per-run instance is
    /// created — keep actions stateless (or explicitly session-scoped) so state never leaks
    /// between runs or into the saved asset.</item>
    /// <item><b>Synchronous.</b> <see cref="ExecuteAction"/> returns before the dialogue
    /// continues; it never pauses the line. For fire-and-forget async work, a coroutine can be
    /// started on <see cref="ExpressionActionContext.Runtime"/>.</item>
    /// </list>
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1BaseExpressionAction.html")]
    public abstract class BaseExpressionAction : ScriptableObject
    {
        /// <summary>
        /// Executes this action. Must be safe to call repeatedly (see the class contract).
        /// </summary>
        /// <param name="context">Runtime context for the application. <c>Display</c> is only
        /// populated when the UI ran the actions itself with a resolved display (the prefab
        /// path); on the automatic fallback pass and the sprite/animated paths it is null.</param>
        public abstract void ExecuteAction(ExpressionActionContext context);
    }
    /// <summary>
    /// Context passed to BaseExpressionAction when a character expression is applied.
    /// </summary>
    public struct ExpressionActionContext
    {
        // Dialogue side
        public WitWeaver Runtime;
        public WitWeaverConversationData Conversation;
        public int LineIndex;

        // Representation side
        public CharacterRepresentationBase Representation;
        public IWitWeaverCharacterDisplay Display;

        // Expression info
        public string ExpressionId;
    }
}