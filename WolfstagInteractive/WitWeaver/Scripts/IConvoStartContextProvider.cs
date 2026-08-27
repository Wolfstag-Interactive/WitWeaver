using System.Collections.Generic;

namespace WolfstagInteractive.ConvoCore
{
    public enum ConvoStartMode
    {
        /// <summary>Ignore any saved snapshot and start the conversation from the first line.</summary>
        Fresh,

        /// <summary>Resume from the last recorded active line with visited-line history applied.</summary>
        Resume,

        /// <summary>Restore saved variables but restart from the first line.</summary>
        Restart
    }

    public struct ConvoStartContext
    {
        public ConvoStartMode     Mode;
        public string             StartLineId;
        public List<string>       VisitedLineIds;
    }

    /// <summary>
    /// Implement on a component alongside <see cref="ConvoCore"/> to control how a conversation
    /// is started. <see cref="ConvoCore.StartConversation"/> queries this interface via
    /// <c>GetComponent</c> before beginning playback.
    /// </summary>
    public interface IConvoStartContextProvider
    {
        ConvoStartContext GetStartContext();
    }
}
