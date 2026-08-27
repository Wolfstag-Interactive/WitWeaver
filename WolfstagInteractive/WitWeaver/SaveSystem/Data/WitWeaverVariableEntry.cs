using System;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    /// <summary>
    /// Container pairing a <see cref="WitWeaverVariable"/> with its scope and read-only flag.
    /// Used as the serialized element type in <see cref="WitWeaverVariableStore"/> persistent entry lists.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1SaveSystem_1_1WitWeaverVariableEntry.html")]
[Serializable]
    public class WitWeaverVariableEntry
    {
        public WitWeaverVariable CoreVariable;
        public WitWeaverVariableScope Scope;
        public bool IsReadOnly;

        // Set by WitWeaverVariableStore whenever a Collection mutation touches this key; read by
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