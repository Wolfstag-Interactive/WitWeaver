using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WolfstagInteractive.WitWeaver.SaveSystem.Tests
{
    /// <summary>
    /// EditMode coverage for Collection variables on <see cref="WitWeaverVariableStore"/>:
    /// round-trips, copy-on-write, events, serialization providers, and migration.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1SaveSystem_1_1Tests_1_1WitWeaverVariableStoreCollectionTests.html")]
    public class WitWeaverVariableStoreCollectionTests
    {
        private WitWeaverVariableStore _store;

        [SetUp]
        public void SetUp()
        {
            _store = ScriptableObject.CreateInstance<WitWeaverVariableStore>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_store);
        }

        // ----- Helpers -----

        private WitWeaverVariableEntry AddAuthoredIntCollection(string key, WitWeaverVariableScope scope,
            bool readOnly, params (string subKey, int value)[] pairs)
        {
            var variable = new WitWeaverVariable { Key = key, Type = WitWeaverVariableType.CollectionInt };
            foreach (var (subKey, value) in pairs)
                variable.CollectionIntPairs.Add(new CollectionIntPair { SubKey = subKey, Value = value });

            var entry = new WitWeaverVariableEntry { CoreVariable = variable, Scope = scope, IsReadOnly = readOnly };
            _store.GetRawEntries().Add(entry);
            return entry;
        }

        private WitWeaverVariableEntry AddAuthoredStringCollection(string key, WitWeaverVariableScope scope,
            bool readOnly, params (string subKey, string value)[] pairs)
        {
            var variable = new WitWeaverVariable { Key = key, Type = WitWeaverVariableType.CollectionString };
            foreach (var (subKey, value) in pairs)
                variable.CollectionStringPairs.Add(new CollectionStringPair { SubKey = subKey, Value = value });

            var entry = new WitWeaverVariableEntry { CoreVariable = variable, Scope = scope, IsReadOnly = readOnly };
            _store.GetRawEntries().Add(entry);
            return entry;
        }

        // ----- 1. Set/TryGet round-trip, both types, all scopes -----

        [Test]
        public void SetAndTryGet_RoundTrips_ForBothTypesAndAllScopes()
        {
            foreach (WitWeaverVariableScope scope in System.Enum.GetValues(typeof(WitWeaverVariableScope)))
            {
                string intKey = "intCol_" + scope;
                string strKey = "strCol_" + scope;

                _store.SetCollectionInt(intKey, "sword", 3, scope);
                _store.SetCollectionString(strKey, "mood", "happy", scope);

                Assert.IsTrue(_store.TryGetCollectionInt(intKey, "sword", out int i), $"int read failed for {scope}");
                Assert.AreEqual(3, i);
                Assert.IsTrue(_store.TryGetCollectionString(strKey, "mood", out string s), $"string read failed for {scope}");
                Assert.AreEqual("happy", s);

                Assert.IsFalse(_store.TryGetCollectionInt(intKey, "missing", out _));
                Assert.IsFalse(_store.TryGetCollectionInt("noSuchKey", "sword", out _));
            }
        }

        // ----- 2. Copy-on-write leaves authored data untouched -----

        [Test]
        public void SessionWrite_ToAuthoredCollection_DoesNotMutateAuthoredEntries()
        {
            var authored = AddAuthoredIntCollection("inventory", WitWeaverVariableScope.Global, false, ("sword", 1));

            _store.SetCollectionInt("inventory", "sword", 2, WitWeaverVariableScope.Global);
            _store.SetCollectionInt("inventory", "shield", 5, WitWeaverVariableScope.Global);

            // Runtime reads see the session copy...
            Assert.IsTrue(_store.TryGetCollectionInt("inventory", "sword", out int sword));
            Assert.AreEqual(2, sword);
            Assert.AreEqual(2, _store.GetCollectionCount("inventory"));

            // ...while the authored persistent entry is byte-identical to what was authored.
            Assert.AreEqual(1, authored.CoreVariable.CollectionIntPairs.Count);
            Assert.AreEqual("sword", authored.CoreVariable.CollectionIntPairs[0].SubKey);
            Assert.AreEqual(1, authored.CoreVariable.CollectionIntPairs[0].Value);
            Assert.AreSame(authored, _store.GetRawEntries()[0]);
        }

        // ----- 3. Removing the last sub-key keeps the variable -----

        [Test]
        public void RemoveCollectionEntry_OnLastSubKey_KeepsEmptyVariable()
        {
            _store.SetCollectionInt("bag", "coin", 10, WitWeaverVariableScope.Global);

            Assert.IsTrue(_store.RemoveCollectionEntry("bag", "coin"));
            Assert.IsTrue(_store.HasVariable("bag"));
            Assert.AreEqual(0, _store.GetCollectionCount("bag"));
            Assert.IsFalse(_store.RemoveCollectionEntry("bag", "coin"), "removing an absent sub-key should return false");
        }

        // ----- 4. GetCollectionKeys returns a defensive copy -----

        [Test]
        public void GetCollectionKeys_ReturnsCopy_SafeAgainstMutation()
        {
            _store.SetCollectionInt("places", "town", 1, WitWeaverVariableScope.Global);
            _store.SetCollectionInt("places", "cave", 1, WitWeaverVariableScope.Global);

            var keys = _store.GetCollectionKeys("places");
            ((List<string>)keys).Add("bogus");
            Assert.AreEqual(2, _store.GetCollectionKeys("places").Count,
                "mutating the returned list must not affect the store");

            // Mutating the Collection while enumerating a previously returned list must not throw.
            var snapshot = _store.GetCollectionKeys("places");
            Assert.DoesNotThrow(() =>
            {
                foreach (var k in snapshot)
                    _store.RemoveCollectionEntry("places", k);
            });
            Assert.AreEqual(0, _store.GetCollectionCount("places"));

            Assert.AreEqual(0, _store.GetCollectionKeys("noSuchCollection").Count);
        }

        // ----- 5. Scalar/Collection type boundaries -----

        [Test]
        public void ScalarAccessors_OnCollectionKey_FailSafely()
        {
            AddAuthoredIntCollection("inventory", WitWeaverVariableScope.Global, false, ("sword", 1));

            Assert.IsFalse(_store.TryGetInt("inventory", out _));
            Assert.IsFalse(_store.TryGetString("inventory", out _));

            LogAssert.Expect(LogType.Warning, new Regex("Collection"));
            Assert.IsFalse(_store.SetInt("inventory", 5));
            Assert.AreEqual(WitWeaverVariableType.CollectionInt, _store.GetVariable("inventory").Type);

            // And the reverse: a Collection write on a scalar key is a logged no-op.
            _store.SetInt("gold", 100);
            LogAssert.Expect(LogType.Warning, new Regex("gold"));
            _store.SetCollectionInt("gold", "sub", 1, WitWeaverVariableScope.Global);
            Assert.IsTrue(_store.TryGetInt("gold", out int gold));
            Assert.AreEqual(100, gold);
        }

        // ----- 6. Read-only blocks every mutating operation -----

        [Test]
        public void ReadOnlyCollection_BlocksAllMutatingOperations()
        {
            AddAuthoredIntCollection("locked", WitWeaverVariableScope.Global, true, ("gem", 1));

            LogAssert.Expect(LogType.Warning, new Regex("read-only"));
            _store.SetCollectionInt("locked", "gem", 99, WitWeaverVariableScope.Global);

            LogAssert.Expect(LogType.Warning, new Regex("read-only"));
            Assert.IsFalse(_store.RemoveCollectionEntry("locked", "gem"));

            LogAssert.Expect(LogType.Warning, new Regex("read-only"));
            _store.ClearCollection("locked");

            Assert.IsTrue(_store.TryGetCollectionInt("locked", "gem", out int gem));
            Assert.AreEqual(1, gem);
            Assert.AreEqual(1, _store.GetCollectionCount("locked"));
        }

        // ----- 7. ResetVariable -----

        [Test]
        public void ResetVariable_RestoresAuthoredCollection_AndRemovesRuntimeCreated()
        {
            var authored = AddAuthoredIntCollection("inventory", WitWeaverVariableScope.Global, false, ("sword", 1));

            _store.SetCollectionInt("inventory", "sword", 9, WitWeaverVariableScope.Global);
            _store.SetCollectionInt("inventory", "shield", 4, WitWeaverVariableScope.Global);
            _store.ResetVariable("inventory");

            Assert.IsTrue(_store.TryGetCollectionInt("inventory", "sword", out int sword));
            Assert.AreEqual(1, sword);
            Assert.AreEqual(1, _store.GetCollectionCount("inventory"));
            Assert.IsFalse(authored.IsDirtySinceSnapshot, "reset must clear the dirty flag");

            _store.SetCollectionInt("loot", "gem", 1, WitWeaverVariableScope.Global);
            _store.ResetVariable("loot");
            Assert.IsFalse(_store.HasVariable("loot"), "runtime-created Collections are removed entirely");
        }

        // ----- 8. Event payloads -----

        [Test]
        public void MutatingOperations_FireExactlyOneEventWithDocumentedPayload()
        {
            var events = new List<(string key, string oldV, string newV)>();
            _store.OnVariableChanged = (k, o, n) => events.Add((k, o, n));

            _store.SetCollectionInt("inv", "sword", 2, WitWeaverVariableScope.Global);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(("inv", (string)null, "2"), events[0]);

            _store.SetCollectionInt("inv", "sword", 3, WitWeaverVariableScope.Global);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(("inv", "2", "3"), events[1]);

            _store.RemoveCollectionEntry("inv", "sword");
            Assert.AreEqual(3, events.Count);
            Assert.AreEqual(("inv", "3", (string)null), events[2]);

            _store.SetCollectionInt("inv", "a", 1, WitWeaverVariableScope.Global);
            _store.SetCollectionInt("inv", "b", 2, WitWeaverVariableScope.Global);
            events.Clear();
            _store.ClearCollection("inv");
            Assert.AreEqual(1, events.Count, "ClearCollection fires a single event, not one per sub-key");
            Assert.AreEqual(("inv", (string)null, (string)null), events[0]);

            // Dirty flag semantics: every mutation marks the entry dirty.
            var authored = AddAuthoredIntCollection("flags", WitWeaverVariableScope.Global, false, ("seen", 1));
            Assert.IsFalse(authored.IsDirtySinceSnapshot);
            _store.SetCollectionInt("flags", "seen", 1, WitWeaverVariableScope.Global);
            Assert.IsTrue(authored.IsDirtySinceSnapshot,
                "a write that restores the authored value still counts as touched");
        }

        // ----- 9. Provider round-trips (JSON and YAML) -----

        [Test]
        public void JsonProvider_RoundTrips_Collections()
        {
            AssertProviderRoundTrip(new JsonFileWitWeaverSaveProvider("WitWeaverSaves_Tests"), "collections_json_test");
        }

        [Test]
        public void YamlProvider_RoundTrips_Collections()
        {
            AssertProviderRoundTrip(new YamlFileWitWeaverSaveProvider("WitWeaverSaves_Tests"), "collections_yaml_test");
        }

        private void AssertProviderRoundTrip(IWitWeaverSaveProvider provider, string slot)
        {
            AddAuthoredStringCollection("relations", WitWeaverVariableScope.Global, false, ("elder", "friendly"));
            AddAuthoredIntCollection("emptyBag", WitWeaverVariableScope.Global, false);
            _store.SetCollectionString("relations", "guard", "hostile", WitWeaverVariableScope.Global);
            _store.SetCollectionInt("runtimeLoot", "gem", 7, WitWeaverVariableScope.Global);

            var snapshot = new WitWeaverGameSnapshot
            {
                GlobalVariables = _store.ExportByScope(WitWeaverVariableScope.Global)
            };

            try
            {
                provider.Save(slot, snapshot);
                var loaded = provider.Load(slot);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(WitWeaverGameSnapshot.CurrentSchemaVersion, loaded.SchemaVersion);

                var restoredStore = ScriptableObject.CreateInstance<WitWeaverVariableStore>();
                try
                {
                    restoredStore.RestoreEntries(loaded.GlobalVariables);

                    Assert.IsTrue(restoredStore.TryGetCollectionString("relations", "elder", out string elder));
                    Assert.AreEqual("friendly", elder);
                    Assert.IsTrue(restoredStore.TryGetCollectionString("relations", "guard", out string guard));
                    Assert.AreEqual("hostile", guard);
                    Assert.IsTrue(restoredStore.TryGetCollectionInt("runtimeLoot", "gem", out int gem));
                    Assert.AreEqual(7, gem);
                    Assert.IsTrue(restoredStore.HasVariable("emptyBag"), "empty Collections must survive the round-trip");
                    Assert.AreEqual(0, restoredStore.GetCollectionCount("emptyBag"));
                }
                finally
                {
                    Object.DestroyImmediate(restoredStore);
                }
            }
            finally
            {
                provider.Delete(slot);
            }
        }

        // ----- 10. Migration 1.0 -> 1.1 -----

        [Test]
        public void Migrator_Upgrades_1_0_Snapshot_To_1_1()
        {
            var old = new WitWeaverGameSnapshot { SchemaVersion = "1.0" };
            old.GlobalVariables.Add(new WitWeaverVariableEntry
            {
                CoreVariable = new WitWeaverVariable { Key = "gold", Type = WitWeaverVariableType.Int }.SetInt(12),
                Scope = WitWeaverVariableScope.Global
            });

            var migrated = WitWeaverSnapshotMigrator.Migrate(old);

            Assert.AreEqual("1.1", migrated.SchemaVersion);
            Assert.AreEqual(1, migrated.GlobalVariables.Count, "1.0 data passes through unchanged");
            Assert.AreEqual(12, migrated.GlobalVariables[0].CoreVariable.GetInt());
        }

        // ----- 11. Duplicate sub-keys: first wins, warning, no throw -----

        [Test]
        public void DuplicateSubKeys_FirstOccurrenceWins_WithWarning()
        {
            AddAuthoredIntCollection("dupes", WitWeaverVariableScope.Global, false, ("sword", 1), ("sword", 9));

            LogAssert.Expect(LogType.Warning, new Regex("duplicate sub-key"));
            Assert.IsTrue(_store.TryGetCollectionInt("dupes", "sword", out int v));
            Assert.AreEqual(1, v, "first occurrence wins");
            Assert.AreEqual(1, _store.GetCollectionCount("dupes"));
        }
    }
}
