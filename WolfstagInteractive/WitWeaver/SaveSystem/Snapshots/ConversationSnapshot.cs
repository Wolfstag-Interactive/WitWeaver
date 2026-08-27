using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    /// <summary>
    /// Serializable snapshot of a single conversation's progress: the active line ID,
    /// the set of visited line IDs, completion status, and any conversation-scoped variables.
    /// Stored as an element inside a <see cref="ConvoCoreGameSnapshot"/>.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SaveSystem_1_1ConversationSnapshot.html")]
    [Serializable]
    public class ConversationSnapshot
    {
        public string ConversationId;
        public string ActiveLineId;
        public bool IsComplete;
        public long SaveTimestamp;
        public List<string> VisitedLineIds = new List<string>();
        public List<ConvoVariableEntry> Variables = new List<ConvoVariableEntry>();
    }
}