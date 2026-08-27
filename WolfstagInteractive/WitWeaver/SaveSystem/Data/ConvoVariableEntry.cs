using System;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    /// <summary>
    /// Container pairing a <see cref="ConvoCoreVariable"/> with its scope and read-only flag.
    /// Used as the serialized element type in <see cref="ConvoVariableStore"/> persistent entry lists.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SaveSystem_1_1ConvoVariableEntry.html")]
[Serializable]
    public class ConvoVariableEntry
    {
        public ConvoCoreVariable CoreVariable;
        public ConvoVariableScope Scope;
        public bool IsReadOnly;

        // Set by ConvoVariableStore whenever a Collection mutation touches this key; read by
        // the inspector to highlight the row. The flag IS the live diff — no content
        // comparison — so a write that restores the authored value still counts as dirty.
        // Reset on authored-defaults snapshot recapture and on ResetVariable.
        [NonSerialized] private bool _isDirtySinceSnapshot;

        /// <summary>
        /// True when a Collection mutation has touched this entry's key since the last
        /// authored-defaults snapshot. Editor-diff signal only; never serialized.
        /// </summary>
        public bool IsDirtySinceSnapshot => _isDirtySinceSnapshot;

        internal void MarkCollectionDirty() => _isDirtySinceSnapshot = true;
        internal void ClearCollectionDirty() => _isDirtySinceSnapshot = false;
    }
}