// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Provides optional runtime context for branch evaluation.
    /// 
    /// This is intentionally minimal for now.
    /// Future versions may expose:
    /// • quest state
    /// • flags/variables
    /// • player/NPC references
    /// • time-of-day, location, etc.
    /// </summary>
    public interface IConversationContext
    {
        // Marker interface — no members yet.
    }

    /// <summary>
    /// Default no-op implementation of <see cref="IConversationContext"/>.
    /// Use this when no runtime gameplay context is needed for branch evaluation.
    /// </summary>
    public sealed class DefaultConversationContext : IConversationContext
    {
        public static readonly DefaultConversationContext Instance = new DefaultConversationContext();
        private DefaultConversationContext() { }
    }
}