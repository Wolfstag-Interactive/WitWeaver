using UnityEngine;
using System;

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    /// <summary>
    /// Serialized sub-entry of a <see cref="WitWeaverVariableType.CollectionInt"/> variable.
    /// Serialization DTO only — public so Unity serialization and the YAML provider can
    /// round-trip it. Mutate Collections only through <see cref="WitWeaverVariableStore"/>.
    /// </summary>
[Serializable]
    public struct CollectionIntPair
    {
        public string SubKey;
        public int Value;
    }

    /// <summary>
    /// Serialized sub-entry of a <see cref="WitWeaverVariableType.CollectionString"/> variable.
    /// Serialization DTO only — public so Unity serialization and the YAML provider can
    /// round-trip it. Mutate Collections only through <see cref="WitWeaverVariableStore"/>.
    /// </summary>
    [Serializable]
    public struct CollectionStringPair
    {
        public string SubKey;
        public string Value;
    }
}