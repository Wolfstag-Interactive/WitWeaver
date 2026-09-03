// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    /// <summary>
    /// Serializable snapshot of a single conversation's progress: the active line ID,
    /// the set of visited line IDs, completion status, and any conversation-scoped variables.
    /// Stored as an element inside a <see cref="WitWeaverGameSnapshot"/>.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1SaveSystem_1_1ConversationSnapshot.html")]
    [Serializable]
    public class ConversationSnapshot
    {
        public string ConversationId;
        public string ActiveLineId;
        public bool IsComplete;
        public long SaveTimestamp;
        public List<string> VisitedLineIds = new List<string>();
        public List<WitWeaverVariableEntry> Variables = new List<WitWeaverVariableEntry>();
    }
}