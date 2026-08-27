# Migrating a beta project from ConvoCore to WitWeaver

Operator checklist for performing the rename migration on a consumer project (written for the plugin author doing the migration on the beta tester's project, not for the tester). Follow the steps in order.

**The one hard rule: Unity stays closed from step 2 until step 9.** Opening the project in a half-migrated state lets the input property drawer silently reset stale SerializeReference data (the runner's Input field) to defaults with no warning.

What survives with zero action: conversation ScriptableObjects, character profiles, custom action assets, and all YAML/Excel content. Package script GUIDs did not change in the rename, so every `m_Script` reference in their scenes, prefabs, and assets still binds, and the YAML schema is unchanged.

## 1. Get the project and make it recoverable

Work on a copy or a fresh branch of the tester's project. Commit the untouched state first so every later step is diffable and reversible.

## 2. Close Unity

And keep it closed until step 9. Also confirm the project uses text serialization (`ProjectSettings/EditorSettings.asset` → `m_SerializationMode: 2`, or just check that a `.unity` file opens as readable YAML) — steps 4–5 depend on it.

## 3. Discovery scan

Before changing anything, inventory the work:

```bash
grep -ri --include="*.cs" --include="*.asmdef" --include="*.unity" --include="*.asset" --include="*.prefab" --include="*.json" "convocore" Assets/ Packages/ ProjectSettings/
```

This tells you: which of their scripts reference the plugin, whether any of their asmdefs reference `ConvoCore`/`ConvoCoreEditor` by name, whether `CONVOCORE_*` defines are set, and which serialized files carry branded strings. Also check specifically for the silent-failure case:

```bash
grep -rn "ns: WolfstagInteractive.ConvoCore" Assets/
```

Expected hit sites: scenes with a runner component (the Input field) and animated character representation assets. Conversation data assets will produce no hits — they contain no SerializeReference data.

## 4. Swap the package entry

In `Packages/manifest.json`, remove the old line and add the new one (the id and the in-repo path both changed, so this is a replace, not an edit):

```json
"com.wolfstaginteractive.witweaver": "https://github.com/Wolfstag-Interactive/WitWeaver.git?path=WolfstagInteractive/WitWeaver"
```

Delete `Packages/packages-lock.json`'s old `com.wolfstaginteractive.convocore` entry too, or just delete the lock file and let Unity regenerate it.

## 5. Fix serialized type-name references

On every `ns: WolfstagInteractive.ConvoCore` hit from step 3:

- `ns: WolfstagInteractive.ConvoCore` → `ns: WolfstagInteractive.WitWeaver`
- `asm: ConvoCore` → `asm: WitWeaver`

Leave `class:` values and the YAML keys alone. This is the step that preserves configured Input fields (especially any `ContainerInput`) and animation payloads. It must land before Unity opens.

## 6. Rename the settings asset, drop stale samples

- `Assets/Resources/ConvoCoreSettings.asset` → `WitWeaverSettings.asset`, **renaming the `.meta` alongside it** (preserves the GUID; the runtime now loads it by the new name). Do not recreate it — rename the pair.
- Delete `Assets/Samples/ConvoCore/` entirely (with its `.meta`). Re-import fresh samples from the package later if wanted.
- If they have a `Resources/ConvoCore/Dialogue/` folder: either rename it to `WitWeaver/Dialogue` **or** leave it and keep the serialized `resourcesRoot` value pointing at the old name — both work; renaming matches the new defaults.

## 7. Migrate their code, asmdefs, and defines

- Their scripts: `ConvoCore` → `WitWeaver` (namespaces and type names), and bare `Convo`-stem types if used: `IConvoInput` → `IWitWeaverInput`, `ConvoVariableStore` → `WitWeaverVariableStore`, etc. The step-3 scan is the worklist.
- Their asmdefs: name-based references `"ConvoCore"` → `"WitWeaver"`, `"ConvoCoreEditor"` → `"WitWeaverEditor"`. GUID references need nothing.
- `ProjectSettings/ProjectSettings.asset`: `CONVOCORE_FMOD`/`_WWISE`/`_ADDRESSABLES` → `WITWEAVER_*`. Skipping this doesn't error — those integrations just silently compile out — so verify it explicitly.

## 8. Zero-hit check

Re-run the step-3 grep. It should return nothing (apart from their own project/folder names if they happen to contain the string). Any remaining hit is unfinished work.

## 9. Open Unity once and verify

Let it resolve the package and recompile, then:

1. Console: no missing-script warnings, no `Unknown managed type referenced`.
2. A conversation ScriptableObject: lines, participants, and settings intact.
3. A scene runner's **Input** field: still shows the configured input (confirms step 5).
4. Play one conversation end to end.
5. If they use FMOD/Wwise/Addressables: confirm the integration is active (confirms step 7).
6. HelpURL buttons resolve (docs are live at `docs.wolfstaginteractive.com/witweaver/`).

## 10. Hand back

Deliver the migrated project with a short note: package is now `com.wolfstaginteractive.witweaver`; all `ConvoCore*`/`Convo*` API names are now `WitWeaver*`; menus live under `WitWeaver/`; if they add integrations later the defines are `WITWEAVER_*`; saves/prefs were not carried over (they had none). Their authoring workflow — YAML, Excel, conversation assets — is unchanged.
