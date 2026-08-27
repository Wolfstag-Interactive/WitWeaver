using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    /// <summary>
    /// A strongly-typed named variable that can hold a <c>string</c>, <c>int</c>, <c>float</c>,
    /// or <c>bool</c> value. Variables are stored in a <see cref="ConvoVariableStore"/> and
    /// persisted to save data by <see cref="ConvoCoreSaveManager"/>.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SaveSystem_1_1ConvoVariable.html")]
[Serializable]
    public class ConvoCoreVariable : ISerializationCallbackReceiver
    {
        public string Key;
        public ConvoVariableType Type;
        public string Description;
        public string[] Tags;

        [SerializeField] private string _stringValue;
        [SerializeField] private int _intValue;
        [SerializeField] private float _floatValue;
        [SerializeField] private bool _boolValue;

        // Serialized Collection storage. Public so Unity serialization, JsonUtility and the
        // YAML provider (which only sees public members) can all round-trip it. Treat as
        // serialization data only — mutations must go through ConvoVariableStore so change
        // events fire and copy-on-write stays intact.
        public List<CollectionIntPair> CollectionIntPairs = new List<CollectionIntPair>();
        public List<CollectionStringPair> CollectionStringPairs = new List<CollectionStringPair>();

        // Runtime lookup mirrors of the pair lists, rebuilt lazily after deserialization.
        [NonSerialized] private Dictionary<string, int> _collectionIntRuntime;
        [NonSerialized] private Dictionary<string, string> _collectionStringRuntime;

        public string GetString() => _stringValue;
        public int GetInt() => _intValue;
        public float GetFloat() => _floatValue;
        public bool GetBool() => _boolValue;

        public ConvoCoreVariable SetString(string value)
        {
            _stringValue = value;
            return this;
        }

        public ConvoCoreVariable SetInt(int value)
        {
            _intValue = value;
            return this;
        }

        public ConvoCoreVariable SetFloat(float value)
        {
            _floatValue = value;
            return this;
        }

        public ConvoCoreVariable SetBool(bool value)
        {
            _boolValue = value;
            return this;
        }

        public bool TryGetValue<T>(out T result)
        {
            switch (Type)
            {
                case ConvoVariableType.String when typeof(T) == typeof(string):
                    result = (T)(object)_stringValue;
                    return true;
                case ConvoVariableType.Int when typeof(T) == typeof(int):
                    result = (T)(object)_intValue;
                    return true;
                case ConvoVariableType.Float when typeof(T) == typeof(float):
                    result = (T)(object)_floatValue;
                    return true;
                case ConvoVariableType.Bool when typeof(T) == typeof(bool):
                    result = (T)(object)_boolValue;
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        public string AsString()
        {
            switch (Type)
            {
                case ConvoVariableType.String:
                    return _stringValue ?? string.Empty;
                case ConvoVariableType.Int:
                    return _intValue.ToString();
                case ConvoVariableType.Float:
                    return _floatValue.ToString();
                case ConvoVariableType.Bool:
                    return _boolValue.ToString();
                case ConvoVariableType.CollectionInt:
                case ConvoVariableType.CollectionString:
                    return $"Collection({CollectionCount})";
                default:
                    return string.Empty;
            }
        }

        public ConvoCoreVariable Clone()
        {
            return new ConvoCoreVariable
            {
                Key = Key,
                Type = Type,
                Description = Description,
                Tags = Tags != null ? (string[])Tags.Clone() : null,
                _stringValue = _stringValue,
                _intValue = _intValue,
                _floatValue = _floatValue,
                _boolValue = _boolValue,
                // Pair structs copy by value, so copying the lists is a deep copy.
                CollectionIntPairs = new List<CollectionIntPair>(CollectionIntPairs ?? new List<CollectionIntPair>()),
                CollectionStringPairs = new List<CollectionStringPair>(CollectionStringPairs ?? new List<CollectionStringPair>())
            };
        }

        // ----- Collection Access (store-mediated) -----
        // Internal on purpose: ConvoVariableStore is the only entry point for Collection
        // reads and writes so change events and copy-on-write cannot be bypassed.

        internal int CollectionCount
        {
            get
            {
                EnsureCollectionRuntime();
                return Type == ConvoVariableType.CollectionInt
                    ? _collectionIntRuntime.Count
                    : Type == ConvoVariableType.CollectionString
                        ? _collectionStringRuntime.Count
                        : 0;
            }
        }

        internal bool TryGetCollectionIntValue(string subKey, out int value)
        {
            EnsureCollectionRuntime();
            if (subKey != null && _collectionIntRuntime.TryGetValue(subKey, out value))
                return true;
            value = default;
            return false;
        }

        internal bool TryGetCollectionStringValue(string subKey, out string value)
        {
            EnsureCollectionRuntime();
            if (subKey != null && _collectionStringRuntime.TryGetValue(subKey, out value))
                return true;
            value = default;
            return false;
        }

        internal bool HasCollectionSubKey(string subKey)
        {
            if (subKey == null) return false;
            EnsureCollectionRuntime();
            return Type == ConvoVariableType.CollectionInt
                ? _collectionIntRuntime.ContainsKey(subKey)
                : Type == ConvoVariableType.CollectionString && _collectionStringRuntime.ContainsKey(subKey);
        }

        internal List<string> GetCollectionKeysCopy()
        {
            EnsureCollectionRuntime();
            var result = new List<string>();
            if (Type == ConvoVariableType.CollectionInt)
                result.AddRange(_collectionIntRuntime.Keys);
            else if (Type == ConvoVariableType.CollectionString)
                result.AddRange(_collectionStringRuntime.Keys);
            return result;
        }

        internal void SetCollectionIntEntry(string subKey, int value)
        {
            EnsureCollectionRuntime();
            _collectionIntRuntime[subKey] = value;

            // Update the first matching pair in place (first occurrence wins on rebuild,
            // so it is the one the runtime dictionary reflects); append if absent.
            for (int i = 0; i < CollectionIntPairs.Count; i++)
            {
                if (CollectionIntPairs[i].SubKey == subKey)
                {
                    CollectionIntPairs[i] = new CollectionIntPair { SubKey = subKey, Value = value };
                    return;
                }
            }
            CollectionIntPairs.Add(new CollectionIntPair { SubKey = subKey, Value = value });
        }

        internal void SetCollectionStringEntry(string subKey, string value)
        {
            EnsureCollectionRuntime();
            _collectionStringRuntime[subKey] = value;

            for (int i = 0; i < CollectionStringPairs.Count; i++)
            {
                if (CollectionStringPairs[i].SubKey == subKey)
                {
                    CollectionStringPairs[i] = new CollectionStringPair { SubKey = subKey, Value = value };
                    return;
                }
            }
            CollectionStringPairs.Add(new CollectionStringPair { SubKey = subKey, Value = value });
        }

        internal bool RemoveCollectionSubKey(string subKey)
        {
            if (subKey == null) return false;
            EnsureCollectionRuntime();
            bool removed = Type == ConvoVariableType.CollectionInt
                ? _collectionIntRuntime.Remove(subKey)
                : Type == ConvoVariableType.CollectionString && _collectionStringRuntime.Remove(subKey);

            if (removed)
            {
                CollectionIntPairs.RemoveAll(p => p.SubKey == subKey);
                CollectionStringPairs.RemoveAll(p => p.SubKey == subKey);
            }
            return removed;
        }

        internal void ClearCollectionEntries()
        {
            EnsureCollectionRuntime();
            _collectionIntRuntime.Clear();
            _collectionStringRuntime.Clear();
            CollectionIntPairs.Clear();
            CollectionStringPairs.Clear();
        }

        private void EnsureCollectionRuntime()
        {
            if (_collectionIntRuntime != null && _collectionStringRuntime != null)
                return;

            _collectionIntRuntime = new Dictionary<string, int>();
            _collectionStringRuntime = new Dictionary<string, string>();

            if (CollectionIntPairs == null) CollectionIntPairs = new List<CollectionIntPair>();
            if (CollectionStringPairs == null) CollectionStringPairs = new List<CollectionStringPair>();

            for (int i = 0; i < CollectionIntPairs.Count; i++)
            {
                var p = CollectionIntPairs[i];
                if (p.SubKey == null) continue;
                if (_collectionIntRuntime.ContainsKey(p.SubKey))
                {
                    Debug.LogWarning($"[ConvoVariableStore] Collection '{Key}' contains duplicate sub-key '{p.SubKey}'. Keeping the first occurrence.");
                    continue;
                }
                _collectionIntRuntime.Add(p.SubKey, p.Value);
            }

            for (int i = 0; i < CollectionStringPairs.Count; i++)
            {
                var p = CollectionStringPairs[i];
                if (p.SubKey == null) continue;
                if (_collectionStringRuntime.ContainsKey(p.SubKey))
                {
                    Debug.LogWarning($"[ConvoVariableStore] Collection '{Key}' contains duplicate sub-key '{p.SubKey}'. Keeping the first occurrence.");
                    continue;
                }
                _collectionStringRuntime.Add(p.SubKey, p.Value);
            }
        }

        // ----- ISerializationCallbackReceiver -----

        public void OnBeforeSerialize()
        {
            // Pair lists are kept in sync eagerly on every mutation; nothing to do here.
        }

        public void OnAfterDeserialize()
        {
            // Serialized pairs may have changed (inspector edit, asset reload); drop the
            // runtime mirrors so the next Collection access rebuilds them.
            _collectionIntRuntime = null;
            _collectionStringRuntime = null;
        }
    }
}