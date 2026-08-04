using UnityEngine;
using System;

namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    /// <summary>
    /// Serialized sub-entry of a <see cref="ConvoVariableType.CollectionInt"/> variable.
    /// Serialization DTO only — public so Unity serialization and the YAML provider can
    /// round-trip it. Mutate Collections only through <see cref="ConvoVariableStore"/>.
    /// </summary>
[Serializable]
    public struct CollectionIntPair
    {
        public string SubKey;
        public int Value;
    }

    /// <summary>
    /// Serialized sub-entry of a <see cref="ConvoVariableType.CollectionString"/> variable.
    /// Serialization DTO only — public so Unity serialization and the YAML provider can
    /// round-trip it. Mutate Collections only through <see cref="ConvoVariableStore"/>.
    /// </summary>
    [Serializable]
    public struct CollectionStringPair
    {
        public string SubKey;
        public string Value;
    }
}