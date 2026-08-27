using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    [CreateAssetMenu(fileName = "NewVariableStore", menuName = "WitWeaver/Runtime/Variable Store")]
    public class WitWeaverVariableStore : ScriptableObject
    {
        // Authored, serialized, editor-visible. Holds Global and Conversation scoped variables.
        // Designers pre-declare these with defaults, descriptions, and tags before the game runs.
        [SerializeField] private List<WitWeaverVariableEntry> _persistentEntries = new List<WitWeaverVariableEntry>();

        // Runtime only — never serialized, never editable in inspector.
        // Holds Session scoped variables exclusively. They only exist after runtime code sets them.
        [NonSerialized] private List<WitWeaverVariableEntry> _sessionEntries;

        // Key-based change listeners — runtime only, not serialized.
        [NonSerialized] private Dictionary<string, Action<WitWeaverVariable>> _keyListeners;

        private List<WitWeaverVariableEntry> SessionEntries
        {
            get
            {
                if (_sessionEntries == null)
                    _sessionEntries = new List<WitWeaverVariableEntry>();
                return _sessionEntries;
            }
        }

        private Dictionary<string, Action<WitWeaverVariable>> KeyListeners
        {
            get
            {
                if (_keyListeners == null)
                    _keyListeners = new Dictionary<string, Action<WitWeaverVariable>>();
                return _keyListeners;
            }
        }

        /// <summary>
        /// Fired on every variable change as (key, oldValue, newValue).
        /// For scalar variables both values are the scalar rendered via <see cref="WitWeaverVariable.AsString"/>.
        /// For Collection variables the payload is the affected sub-entry value (ints rendered
        /// as strings), not the whole Collection: a Set carries the old and new sub-entry values
        /// (old is null when the sub-key is new), RemoveCollectionEntry carries (oldValue, null),
        /// and ClearCollection fires exactly once with (null, null).
        /// </summary>
        public Action<string, string, string> OnVariableChanged;

        // ----- List Routing -----

        private List<WitWeaverVariableEntry> ListForScope(WitWeaverVariableScope scope)
        {
            return scope == WitWeaverVariableScope.Session ? SessionEntries : _persistentEntries;
        }

        private static WitWeaverVariableEntry FindInList(List<WitWeaverVariableEntry> list, string key)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].CoreVariable != null && list[i].CoreVariable.Key == key)
                    return list[i];
            }
            return null;
        }

        // Session layer wins: Collections copied-on-write into the session layer (and any
        // session-scoped entry) shadow the authored persistent entry with the same key.
        private WitWeaverVariableEntry GetEntry(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return FindInList(SessionEntries, key) ?? FindInList(_persistentEntries, key);
        }

        private static bool IsCollectionType(WitWeaverVariableType type)
        {
            return type == WitWeaverVariableType.CollectionInt || type == WitWeaverVariableType.CollectionString;
        }

        // ----- SetInternal -----

        private bool SetInternal(string key, Action<WitWeaverVariableEntry> apply,
            WitWeaverVariableType type, WitWeaverVariableScope scope)
        {
            // Never silently replace a Collection with a scalar.
            var existing = GetEntry(key);
            if (existing != null && IsCollectionType(existing.CoreVariable.Type))
            {
                Debug.LogWarning($"[WitWeaverVariableStore] Variable '{key}' is a {existing.CoreVariable.Type} Collection; scalar {type} write ignored.");
                return false;
            }

            var list = ListForScope(scope);
            WitWeaverVariableEntry entry = FindInList(list, key);

            if (entry == null)
            {
                entry = new WitWeaverVariableEntry
                {
                    CoreVariable = new WitWeaverVariable { Key = key, Type = type },
                    Scope = scope,
                    IsReadOnly = false
                };
                list.Add(entry);
            }

            if (entry.IsReadOnly)
            {
                Debug.LogWarning($"[WitWeaverVariableStore] Variable '{key}' is marked read-only.");
                return false;
            }

            string oldValue = entry.CoreVariable.AsString();
            apply(entry);
            string newValue = entry.CoreVariable.AsString();

            if (oldValue != newValue)
            {
                OnVariableChanged?.Invoke(key, oldValue, newValue);
                if (KeyListeners.TryGetValue(key, out var listener))
                    listener?.Invoke(entry.CoreVariable);
            }

            return true;
        }

        // ----- Write Methods -----

        public bool SetString(string key, string value, WitWeaverVariableScope scope = WitWeaverVariableScope.Global)
            => SetInternal(key, e => e.CoreVariable.SetString(value), WitWeaverVariableType.String, scope);

        public bool SetInt(string key, int value, WitWeaverVariableScope scope = WitWeaverVariableScope.Global)
            => SetInternal(key, e => e.CoreVariable.SetInt(value), WitWeaverVariableType.Int, scope);

        public bool SetFloat(string key, float value, WitWeaverVariableScope scope = WitWeaverVariableScope.Global)
            => SetInternal(key, e => e.CoreVariable.SetFloat(value), WitWeaverVariableType.Float, scope);

        public bool SetBool(string key, bool value, WitWeaverVariableScope scope = WitWeaverVariableScope.Global)
            => SetInternal(key, e => e.CoreVariable.SetBool(value), WitWeaverVariableType.Bool, scope);

        // ----- Read Methods -----

        public bool TryGetString(string key, out string value)
        {
            var entry = GetEntry(key);
            if (entry != null && entry.CoreVariable.Type == WitWeaverVariableType.String)
            {
                value = entry.CoreVariable.GetString();
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetInt(string key, out int value)
        {
            var entry = GetEntry(key);
            if (entry != null && entry.CoreVariable.Type == WitWeaverVariableType.Int)
            {
                value = entry.CoreVariable.GetInt();
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetFloat(string key, out float value)
        {
            var entry = GetEntry(key);
            if (entry != null && entry.CoreVariable.Type == WitWeaverVariableType.Float)
            {
                value = entry.CoreVariable.GetFloat();
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetBool(string key, out bool value)
        {
            var entry = GetEntry(key);
            if (entry != null && entry.CoreVariable.Type == WitWeaverVariableType.Bool)
            {
                value = entry.CoreVariable.GetBool();
                return true;
            }
            value = default;
            return false;
        }

        public WitWeaverVariable GetVariable(string key)
        {
            return GetEntry(key)?.CoreVariable;
        }

        public bool HasVariable(string key)
        {
            return GetEntry(key) != null;
        }

        // ----- Collection Methods -----
        //
        // Collections are named groups of typed sub-entries (string sub-key -> int or string).
        // The backing dictionary is never exposed; every read and write goes through these
        // sub-key methods so change events fire and authored defaults stay untouched.
        //
        // Copy-on-write: authored Collections live in _persistentEntries. The first mutating
        // operation on one deep-copies it into the session layer and mutates the copy, so the
        // authored data is never modified at runtime. Reads check the session layer first.

        /// <summary>
        /// Sets a sub-entry on an int Collection. Creates the Collection variable (in the
        /// session layer) if the top-level key does not exist. No-op with a warning if the
        /// key exists but is not an int Collection, or if the entry is read-only.
        /// </summary>
        public void SetCollectionInt(string key, string subKey, int value, WitWeaverVariableScope scope)
        {
            if (string.IsNullOrEmpty(key) || subKey == null) return;

            var entry = GetOrCreateCollectionEntryForWrite(key, WitWeaverVariableType.CollectionInt, scope);
            if (entry == null) return;

            string oldValue = entry.CoreVariable.TryGetCollectionIntValue(subKey, out var old) ? old.ToString() : null;
            entry.CoreVariable.SetCollectionIntEntry(subKey, value);
            MarkCollectionDirty(key, entry);
            NotifyCollectionChanged(key, entry, oldValue, value.ToString());
        }

        /// <summary>
        /// Sets a sub-entry on a string Collection. Creates the Collection variable (in the
        /// session layer) if the top-level key does not exist. No-op with a warning if the
        /// key exists but is not a string Collection, or if the entry is read-only.
        /// </summary>
        public void SetCollectionString(string key, string subKey, string value, WitWeaverVariableScope scope)
        {
            if (string.IsNullOrEmpty(key) || subKey == null) return;

            var entry = GetOrCreateCollectionEntryForWrite(key, WitWeaverVariableType.CollectionString, scope);
            if (entry == null) return;

            string oldValue = entry.CoreVariable.TryGetCollectionStringValue(subKey, out var old) ? old : null;
            entry.CoreVariable.SetCollectionStringEntry(subKey, value);
            MarkCollectionDirty(key, entry);
            NotifyCollectionChanged(key, entry, oldValue, value);
        }

        /// <summary>
        /// Reads a sub-entry from an int Collection. Returns false if the Collection does not
        /// exist, the sub-key does not exist, or the variable is not an int Collection.
        /// </summary>
        public bool TryGetCollectionInt(string key, string subKey, out int value)
        {
            var entry = GetEntry(key);
            if (entry != null && entry.CoreVariable.Type == WitWeaverVariableType.CollectionInt)
                return entry.CoreVariable.TryGetCollectionIntValue(subKey, out value);
            value = default;
            return false;
        }

        /// <summary>
        /// Reads a sub-entry from a string Collection. Returns false if the Collection does not
        /// exist, the sub-key does not exist, or the variable is not a string Collection.
        /// </summary>
        public bool TryGetCollectionString(string key, string subKey, out string value)
        {
            var entry = GetEntry(key);
            if (entry != null && entry.CoreVariable.Type == WitWeaverVariableType.CollectionString)
                return entry.CoreVariable.TryGetCollectionStringValue(subKey, out value);
            value = default;
            return false;
        }

        /// <summary>
        /// True if the Collection exists and contains the given sub-key.
        /// </summary>
        public bool HasCollectionEntry(string key, string subKey)
        {
            var entry = GetEntry(key);
            return entry != null && IsCollectionType(entry.CoreVariable.Type)
                && entry.CoreVariable.HasCollectionSubKey(subKey);
        }

        /// <summary>
        /// Removes a sub-entry from a Collection. Returns true if the sub-key was removed.
        /// Removing the last sub-key leaves an empty Collection — the variable itself is
        /// never deleted (an empty inventory is valid state). No-op with a warning on a
        /// read-only entry.
        /// </summary>
        public bool RemoveCollectionEntry(string key, string subKey)
        {
            if (string.IsNullOrEmpty(key) || subKey == null) return false;

            var existing = GetEntry(key);
            if (existing == null || !IsCollectionType(existing.CoreVariable.Type))
                return false;
            if (!existing.CoreVariable.HasCollectionSubKey(subKey))
                return false;

            var entry = GetOrCreateCollectionEntryForWrite(key, existing.CoreVariable.Type, existing.Scope);
            if (entry == null) return false;

            string oldValue = entry.CoreVariable.Type == WitWeaverVariableType.CollectionInt
                ? (entry.CoreVariable.TryGetCollectionIntValue(subKey, out var oi) ? oi.ToString() : null)
                : (entry.CoreVariable.TryGetCollectionStringValue(subKey, out var os) ? os : null);

            if (!entry.CoreVariable.RemoveCollectionSubKey(subKey))
                return false;

            MarkCollectionDirty(key, entry);
            NotifyCollectionChanged(key, entry, oldValue, null);
            return true;
        }

        /// <summary>
        /// Number of sub-entries in the Collection, or 0 if the key is missing or not a Collection.
        /// </summary>
        public int GetCollectionCount(string key)
        {
            var entry = GetEntry(key);
            if (entry != null && IsCollectionType(entry.CoreVariable.Type))
                return entry.CoreVariable.CollectionCount;
            return 0;
        }

        /// <summary>
        /// Returns the Collection's sub-keys as a new copied list on every call — never the
        /// live key collection, so callers can safely enumerate while the Collection is
        /// mutated. Returns an empty list if the Collection does not exist.
        /// </summary>
        public IReadOnlyList<string> GetCollectionKeys(string key)
        {
            var entry = GetEntry(key);
            if (entry != null && IsCollectionType(entry.CoreVariable.Type))
                return entry.CoreVariable.GetCollectionKeysCopy();
            return new List<string>();
        }

        /// <summary>
        /// Removes every sub-entry from the Collection but keeps the variable itself
        /// (<see cref="HasVariable"/> remains true). Fires a single change event with both
        /// payload values null rather than one event per removed sub-key. No-op if the key
        /// is missing, not a Collection, already empty, or read-only.
        /// </summary>
        public void ClearCollection(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            var existing = GetEntry(key);
            if (existing == null || !IsCollectionType(existing.CoreVariable.Type))
                return;
            if (existing.CoreVariable.CollectionCount == 0)
                return;

            var entry = GetOrCreateCollectionEntryForWrite(key, existing.CoreVariable.Type, existing.Scope);
            if (entry == null) return;

            entry.CoreVariable.ClearCollectionEntries();
            MarkCollectionDirty(key, entry);
            NotifyCollectionChanged(key, entry, null, null);
        }

        /// <summary>
        /// Resets a Collection variable: an authored Collection reverts to its authored
        /// sub-entries (the session copy is discarded), a runtime-created Collection is
        /// removed entirely. Clears the dirty-since-snapshot flag. Fires a single change
        /// event with both payload values null. Scalar variables are not supported and
        /// log a warning.
        /// </summary>
        public void ResetVariable(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            var session = FindInList(SessionEntries, key);
            var persistent = FindInList(_persistentEntries, key);
            var any = session ?? persistent;
            if (any == null) return;

            if (!IsCollectionType(any.CoreVariable.Type))
            {
                Debug.LogWarning($"[WitWeaverVariableStore] ResetVariable currently supports Collection variables only; '{key}' is a {any.CoreVariable.Type}.");
                return;
            }

            if (session != null)
                SessionEntries.Remove(session);

            persistent?.ClearCollectionDirty();

            OnVariableChanged?.Invoke(key, null, null);
            if (persistent != null && KeyListeners.TryGetValue(key, out var listener))
                listener?.Invoke(persistent.CoreVariable);
        }

        /// <summary>
        /// Clears the dirty-since-snapshot flag on every Collection entry. Called by the
        /// inspector when the authored-defaults snapshot is recaptured on entering Play Mode.
        /// </summary>
        public void ClearAllCollectionDirtyFlags()
        {
            for (int i = 0; i < _persistentEntries.Count; i++)
                _persistentEntries[i].ClearCollectionDirty();
            for (int i = 0; i < SessionEntries.Count; i++)
                SessionEntries[i].ClearCollectionDirty();
        }

        // Resolves the entry a Collection mutation should apply to:
        // 1. An existing session-layer entry is mutated directly.
        // 2. An authored persistent entry is deep-copied into the session layer first
        //    (copy-on-write) and the copy is mutated. Authored data is never touched.
        // 3. Otherwise a new empty Collection is created in the session layer with the
        //    caller-supplied scope.
        // Returns null (with a warning) on type mismatch or read-only entries.
        private WitWeaverVariableEntry GetOrCreateCollectionEntryForWrite(string key,
            WitWeaverVariableType type, WitWeaverVariableScope scope)
        {
            var session = FindInList(SessionEntries, key);
            if (session != null)
            {
                if (session.CoreVariable.Type != type)
                {
                    Debug.LogWarning($"[WitWeaverVariableStore] Variable '{key}' is a {session.CoreVariable.Type}; {type} Collection write ignored.");
                    return null;
                }
                if (session.IsReadOnly)
                {
                    Debug.LogWarning($"[WitWeaverVariableStore] Variable '{key}' is marked read-only.");
                    return null;
                }
                return session;
            }

            var persistent = FindInList(_persistentEntries, key);
            if (persistent != null)
            {
                if (persistent.CoreVariable.Type != type)
                {
                    Debug.LogWarning($"[WitWeaverVariableStore] Variable '{key}' is a {persistent.CoreVariable.Type}; {type} Collection write ignored.");
                    return null;
                }
                if (persistent.IsReadOnly)
                {
                    Debug.LogWarning($"[WitWeaverVariableStore] Variable '{key}' is marked read-only.");
                    return null;
                }

                var copy = new WitWeaverVariableEntry
                {
                    CoreVariable = persistent.CoreVariable.Clone(),
                    Scope = persistent.Scope,
                    IsReadOnly = persistent.IsReadOnly
                };
                SessionEntries.Add(copy);
                return copy;
            }

            var created = new WitWeaverVariableEntry
            {
                CoreVariable = new WitWeaverVariable { Key = key, Type = type },
                Scope = scope,
                IsReadOnly = false
            };
            SessionEntries.Add(created);
            return created;
        }

        private void MarkCollectionDirty(string key, WitWeaverVariableEntry mutated)
        {
            mutated.MarkCollectionDirty();
            // Mirror onto the authored entry (its data is untouched by copy-on-write) so the
            // inspector's authored row can read the flag without resolving the session layer.
            FindInList(_persistentEntries, key)?.MarkCollectionDirty();
        }

        private void NotifyCollectionChanged(string key, WitWeaverVariableEntry entry,
            string oldValue, string newValue)
        {
            OnVariableChanged?.Invoke(key, oldValue, newValue);
            if (KeyListeners.TryGetValue(key, out var listener))
                listener?.Invoke(entry.CoreVariable);
        }

        // ----- Query Methods -----

        public IReadOnlyList<WitWeaverVariableEntry> GetByScope(WitWeaverVariableScope scope)
        {
            var list = ListForScope(scope);
            var result = new List<WitWeaverVariableEntry>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Scope == scope)
                    result.Add(list[i]);
            }
            return result;
        }

        public IReadOnlyList<WitWeaverVariableEntry> GetByTag(string tag)
        {
            var result = new List<WitWeaverVariableEntry>();
            if (string.IsNullOrEmpty(tag)) return result;

            CollectByTag(_persistentEntries, tag, result);
            CollectByTag(SessionEntries, tag, result);
            return result;
        }

        private static void CollectByTag(List<WitWeaverVariableEntry> list, string tag,
            List<WitWeaverVariableEntry> result)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var tags = list[i].CoreVariable?.Tags;
                if (tags == null) continue;
                for (int t = 0; t < tags.Length; t++)
                {
                    if (tags[t] == tag)
                    {
                        result.Add(list[i]);
                        break;
                    }
                }
            }
        }

        // ----- Subscription Methods -----

        public void Subscribe(string key, Action<WitWeaverVariable> callback)
        {
            if (string.IsNullOrEmpty(key) || callback == null) return;

            if (KeyListeners.ContainsKey(key))
                KeyListeners[key] += callback;
            else
                KeyListeners[key] = callback;
        }

        public void Unsubscribe(string key, Action<WitWeaverVariable> callback)
        {
            if (string.IsNullOrEmpty(key) || callback == null) return;

            if (KeyListeners.ContainsKey(key))
            {
                KeyListeners[key] -= callback;
                if (KeyListeners[key] == null)
                    KeyListeners.Remove(key);
            }
        }

        // ----- Snapshot Methods -----

        public List<WitWeaverVariableEntry> ExportByScope(WitWeaverVariableScope scope)
        {
            // Session variables are runtime-only — never written to a save file.
            if (scope == WitWeaverVariableScope.Session)
                return new List<WitWeaverVariableEntry>();

            var result = new List<WitWeaverVariableEntry>();
            for (int i = 0; i < _persistentEntries.Count; i++)
            {
                var entry = _persistentEntries[i];
                if (entry.Scope != scope) continue;

                // Collections mutate a session-layer copy (copy-on-write); export the copy's
                // current values when one exists, not the untouched authored defaults.
                var source = entry;
                if (entry.CoreVariable != null && IsCollectionType(entry.CoreVariable.Type))
                {
                    var overlay = FindInList(SessionEntries, entry.CoreVariable.Key);
                    if (overlay != null && overlay.CoreVariable.Type == entry.CoreVariable.Type)
                        source = overlay;
                }

                result.Add(new WitWeaverVariableEntry
                {
                    CoreVariable = source.CoreVariable.Clone(),
                    Scope = entry.Scope,
                    IsReadOnly = entry.IsReadOnly
                });
            }

            // Runtime-created Collections with a persistent scope live only in the session
            // layer; include them so they survive a save/load round-trip.
            for (int i = 0; i < SessionEntries.Count; i++)
            {
                var entry = SessionEntries[i];
                if (entry.Scope != scope || entry.CoreVariable == null) continue;
                if (!IsCollectionType(entry.CoreVariable.Type)) continue;
                if (FindInList(_persistentEntries, entry.CoreVariable.Key) != null) continue;

                result.Add(new WitWeaverVariableEntry
                {
                    CoreVariable = entry.CoreVariable.Clone(),
                    Scope = entry.Scope,
                    IsReadOnly = entry.IsReadOnly
                });
            }
            return result;
        }

        public void RestoreEntries(List<WitWeaverVariableEntry> entries)
        {
            if (entries == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                var incoming = entries[i];
                if (incoming.CoreVariable == null) continue;

                // Session scope is never saved, so it is never restored.
                if (incoming.Scope == WitWeaverVariableScope.Session)
                    continue;

                // Collections restore into the session layer so authored defaults in
                // _persistentEntries stay untouched — same layering as copy-on-write.
                if (IsCollectionType(incoming.CoreVariable.Type))
                {
                    var authored = FindInList(_persistentEntries, incoming.CoreVariable.Key);
                    if (authored != null && authored.CoreVariable.Type != incoming.CoreVariable.Type)
                    {
                        Debug.LogWarning($"[WitWeaverVariableStore] Saved Collection '{incoming.CoreVariable.Key}' conflicts with authored {authored.CoreVariable.Type} entry; skipping restore.");
                        continue;
                    }

                    var overlay = FindInList(SessionEntries, incoming.CoreVariable.Key);
                    if (overlay != null)
                        SessionEntries.Remove(overlay);

                    var restored = new WitWeaverVariableEntry
                    {
                        CoreVariable = incoming.CoreVariable.Clone(),
                        Scope = incoming.Scope,
                        IsReadOnly = incoming.IsReadOnly
                    };
                    SessionEntries.Add(restored);
                    MarkCollectionDirty(incoming.CoreVariable.Key, restored);
                    continue;
                }

                bool found = false;
                for (int j = 0; j < _persistentEntries.Count; j++)
                {
                    if (_persistentEntries[j].CoreVariable != null &&
                        _persistentEntries[j].CoreVariable.Key == incoming.CoreVariable.Key)
                    {
                        _persistentEntries[j].CoreVariable = incoming.CoreVariable.Clone();
                        _persistentEntries[j].Scope = incoming.Scope;
                        _persistentEntries[j].IsReadOnly = incoming.IsReadOnly;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    _persistentEntries.Add(new WitWeaverVariableEntry
                    {
                        CoreVariable = incoming.CoreVariable.Clone(),
                        Scope = incoming.Scope,
                        IsReadOnly = incoming.IsReadOnly
                    });
                }
            }
        }

        public void ClearByScope(WitWeaverVariableScope scope)
        {
            if (scope == WitWeaverVariableScope.Session)
            {
                // The session layer also carries Collection copy-on-write overlays with
                // persistent scopes — clearing the Session scope must not revert those.
                SessionEntries.RemoveAll(e => e.Scope == WitWeaverVariableScope.Session);
            }
            else
            {
                // Authored Collection rows are never removed from the persistent layer;
                // dropping their session overlays reverts them to authored defaults.
                _persistentEntries.RemoveAll(e =>
                    e.Scope == scope && (e.CoreVariable == null || !IsCollectionType(e.CoreVariable.Type)));
                SessionEntries.RemoveAll(e => e.Scope == scope);
            }
        }

        // ----- Internal Access -----

        public List<WitWeaverVariableEntry> GetRawEntries()
        {
            return _persistentEntries;
        }

        // ----- Editor-Only Access -----

#if UNITY_EDITOR
        public IReadOnlyList<WitWeaverVariableEntry> GetSessionEntries()
        {
            return SessionEntries;
        }
#endif
    }
}