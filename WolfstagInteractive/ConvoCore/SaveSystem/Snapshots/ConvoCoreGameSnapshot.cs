using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    /// <summary>
    /// Serializable snapshot of the full game save state, including all conversation progress
    /// and global variables. Assembled and restored by <see cref="ConvoCoreSaveManager"/>
    /// via an <see cref="IConvoSaveProvider"/>.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SaveSystem_1_1ConvoCoreGameSnapshot.html")]
[Serializable]
    public class ConvoCoreGameSnapshot
    {
        /// <summary>
        /// Schema version written by the current runtime. 1.1 added Collection variables
        /// (serialized as list-of-pairs inside <see cref="ConvoCoreVariable"/>).
        /// </summary>
        public const string CurrentSchemaVersion = "1.1";

        public string SchemaVersion = CurrentSchemaVersion;
        public List<ConvoVariableEntry> GlobalVariables = new List<ConvoVariableEntry>();
        public List<ConversationSnapshot> Conversations = new List<ConversationSnapshot>();
        public long SaveTimestamp;
    }
}