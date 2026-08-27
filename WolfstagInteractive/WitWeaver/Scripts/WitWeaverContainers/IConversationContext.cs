namespace WolfstagInteractive.ConvoCore
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