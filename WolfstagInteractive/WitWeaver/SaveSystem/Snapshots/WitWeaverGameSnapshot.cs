using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    /// <summary>
    /// Serializable snapshot of the full game save state, including all conversation progress
    /// and global variables. Assembled and restored by <see cref="WitWeaverSaveManager"/>
    /// via an <see cref="IWitWeaverSaveProvider"/>.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1SaveSystem_1_1WitWeaverGameSnapshot.html")]
[Serializable]
    public class WitWeaverGameSnapshot
    {
        /// <summary>
        /// Schema version written by the current runtime. 1.1 added Collection variables
        /// (serialized as list-of-pairs inside <see cref="WitWeaverVariable"/>).
        /// </summary>
        public const string CurrentSchemaVersion = "1.1";

        public string SchemaVersion = CurrentSchemaVersion;
        public List<WitWeaverVariableEntry> GlobalVariables = new List<WitWeaverVariableEntry>();
        public List<ConversationSnapshot> Conversations = new List<ConversationSnapshot>();
        public long SaveTimestamp;
    }
}