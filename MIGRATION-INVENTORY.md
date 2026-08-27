# ConvoCore → WitWeaver Migration Inventory

Generated from the merged tree (`main` @ `5f0cb86` + baseline reserialization commit) before any rename change. This is the discovery deliverable required by the migration brief.

## Repo layout

| Path | Role |
|---|---|
| `WolfstagInteractive/ConvoCore/` | UPM package source (the product being renamed) → `WolfstagInteractive/WitWeaver/` |
| `ConvoCoreDev/` | Dev Unity project (6000.5.6f1); consumes package via `file:` path in `Packages/manifest.json` — **folder name retained** (protected token) |
| `ConvoCoreTest/` | Secondary test project (6000.0.73f1); consumes package via GitHub URL — **folder name retained** |
| `website_docusaurus/` | Docs site (authored docs in scope; `build/` and Doxygen API mirror are generated) |
| `Docs/`, `html/`, `latex/` | Generated Doxygen output (excluded; regenerates) |

## Namespaces (7, by file count)

| Namespace | Files | → New name |
|---|---|---|
| `WolfstagInteractive.ConvoCore` | 96 | `WolfstagInteractive.WitWeaver` |
| `WolfstagInteractive.ConvoCore.Editor` | 41 | `WolfstagInteractive.WitWeaver.Editor` |
| `WolfstagInteractive.ConvoCore.SaveSystem` | 20 | `WolfstagInteractive.WitWeaver.SaveSystem` |
| `WolfstagInteractive.ConvoCore.GraphEditor` | 14 | `WolfstagInteractive.WitWeaver.GraphEditor` |
| `WolfstagInteractive.ConvoCore.SaveSystem.Editor` | 6 | `WolfstagInteractive.WitWeaver.SaveSystem.Editor` |
| `WolfstagInteractive.ConvoCoreEditor` (typo outlier, `RepresentationMappingListEditor.cs`) | 1 | `WolfstagInteractive.WitWeaverEditor` (renamed in place, not fixed) |
| `WolfstagInteractive.ConvoCore.SaveSystem.Tests` | 1 | `WolfstagInteractive.WitWeaver.SaveSystem.Tests` |

## Assembly definitions (6 first-party)

| asmdef | Name → New | Name-based refs to update |
|---|---|---|
| `Scripts/ConvoCore.asmdef` | `ConvoCore` → `WitWeaver` | — (GUID refs only) |
| `Editor/ConvoCoreEditor.asmdef` | `ConvoCoreEditor` → `WitWeaverEditor` | — (GUID ref) |
| `Editor/GraphEditor/ConvoCoreGraphEditor.asmdef` | `ConvoCoreGraphEditor` → `WitWeaverGraphEditor` | `"ConvoCore"`, `"ConvoCoreEditor"`; defineConstraints + versionDefines `CONVOCORE_GRAPHTOOLKIT` |
| `SaveSystem/WolfstagInteractive.ConvoCore.SaveSystem.asmdef` | `...ConvoCore.SaveSystem` → `...WitWeaver.SaveSystem` | — (GUID refs) |
| `SaveSystem/Editor/....SaveSystem.Editor.asmdef` | same substitution | `"WolfstagInteractive.ConvoCore.SaveSystem"`, `"ConvoCoreEditor"` |
| `ConvoCoreDev/Assets/Tests/Editor/....SaveSystem.Tests.Editor.asmdef` | same substitution | — (GUID refs) |

All other cross-references are by GUID and are rename-safe. `InternalsVisibleTo("ConvoCoreEditor")` in runtime code updates with rule 1.

## Package identity

- `package.json`: `com.wolfstaginteractive.convocore` → `com.wolfstaginteractive.witweaver`; `displayName` `ConvoCore` → `WitWeaver`; description ×2; samples paths `Samples~/ConvoCore/...` and defines in sample descriptions.
- `ConvoCoreDev/Packages/manifest.json`: `file:` path (last segment) + package id key; `packages-lock.json` entry.
- `ConvoCoreTest/Packages/manifest.json`: id key + `https://github.com/Wolfstag-Interactive/ConvoCore.git?path=WolfstagInteractive/ConvoCore` → WitWeaver form (unresolvable until the GitHub repo rename — external step).

## Types

- **110 distinct type names** contain `ConvoCore` (runtime, editor incl. 14 GraphEditor classes, SaveSystem, samples, dev tooling). All rename mechanically via rule 1.
- **20 bare-`Convo`-stem types** (renamed per user decision, stem-in-place → `WitWeaver`): `BaseConvoBranchCondition`, `ConvoAudioReference`, `ConvoInputPropertyDrawer`, `ConvoRestoreBehavior`, `ConvoSettingsState`, `ConvoStartContext`, `ConvoStartMode`, `ConvoVariableEntry`, `ConvoVariableScope`, `ConvoVariableStore`, `ConvoVariableStoreCollectionTests`, `ConvoVariableStoreEditor`, `ConvoVariableType`, `IConvoAudioProvider`, `IConvoBranchCondition`, `IConvoInput`, `IConvoSaveProvider`, `IConvoStartContextProvider`, `JsonFileConvoSaveProvider`, `YamlFileConvoSaveProvider`. Save-file extensions `.convo.json`/`.convo.yml` → `.witweaver.json`/`.witweaver.yml`.
- **camelCase identifiers** (93 occurrences; in C# fields/locals AND serialized scene/prefab field data — both sides renamed in the same window, no `FormerlySerializedAs` needed): `convoCoreSettings` (39), `convoCoreConversationData` (25), `convoCoreLanguageManager` (15), `convoCoreInstance` (8), `convoCore` (4), `convoCoreComponent` (2).
- 3 MonoBehaviours live in files named after a different (ScriptableObject) type — `ConvoCoreCameraRelativePosition` in `CameraRelativeBehaviour.cs`, `ConvoCoreFollowTarget` in `FollowTargetBehaviour.cs`, `ConvoCoreTransformLerp` in `TransformLerpBehaviour.cs`. Classes rename in content only; files untouched (no basename match), so `.meta` GUIDs and the existing file/class mismatch degree are preserved.

## Serialized type-name references (must be text-edited — the highest-risk items)

17 managed-reference lines carry `ns:`/`asm:` strings (Unity SerializeReference). Both fields — and for the graph, the `class:` — change; YAML schema keys stay untouched:

- `type: {class: SingleConversationInput, ns: WolfstagInteractive.ConvoCore, asm: ConvoCore}` — 7×: `ConvoCoreDev/Assets/Scenes/Testing scene.unity:420`, 2D sample scene `:411` and 3D sample scene `:179` in each of the three mirror sets (`ConvoCoreDev/Assets/SampleAssetsRepo/1.0.0`, package `Samples~/ConvoCore`, `ConvoCoreTest/Assets/Samples/ConvoCore/1.0.0`).
- `type: {class: FlipbookAnimationPayload, ns: WolfstagInteractive.ConvoCore, asm: ConvoCore}` — 2×: `ConvoCoreDev/Assets/TestAnimatedRepresentation.asset:36,44`.
- Graph asset `ConvoCoreDev/Assets/ConvoCoreSampleConversationData TEST.ConvoCoreConversationGraph` — 8×: `class: ConvoCoreConversationGraph` (:182), `ConversationStartNode` (:618), `DialogueLineNode` ×5, `EndConversationNode` (:668), all `ns: WolfstagInteractive.ConvoCore.GraphEditor, asm: ConvoCoreGraphEditor`.

Related hazard: `Editor/ConvoInputDrawer.cs` re-resolves `managedReferenceFullTypename` via `Type.GetType` and silently rewrites unresolvable refs — Unity must not open between the code rename and these asset edits.

Also: 6 stale-able `m_EditorClassIdentifier` strings (`ConvoCore::WolfstagInteractive.ConvoCore.*`) in ConvoCoreDev assets (RendererProfiles ×3, Resources settings, TestAnimatedRepresentation, TestCharacterProfile ×2) — pre-edited to avoid churn.

## Graph editor (merged into main via PR #31)

- 14 classes in `Editor/GraphEditor/` (+`Nodes/`), assembly `ConvoCoreGraphEditor`, namespace `WolfstagInteractive.ConvoCore.GraphEditor`.
- Custom asset extension: `ConvoCoreConversationGraph.AssetExtension = "ConvoCoreConversationGraph"` (const, used everywhere incl. `[Graph(AssetExtension)]`). One asset on disk (`ConvoCoreDev/Assets/ConvoCoreSampleConversationData TEST.ConvoCoreConversationGraph` + `.meta`) — file renamed with the const in the same window.
- Sync hashes (`LastSyncedYamlHash`, `LastBakedGraphHash`) are YAML/graph **content** hashes — unaffected by the rename (schema unchanged).

## String-literal audit categories (content substitution targets)

- **Type-name lookups (silent-failure class):** `AssetDatabase.FindAssets("t:WolfstagInteractive.ConvoCore.ConvoCoreConversationData")` (`ConvoCoreYamlWatcher.cs`, `ConvoCoreExcelWatcher.cs`), short-name `t:ConvoCoreConversationData` / `t:ConvoCoreSettings` filters (6 files), `Resources.Load("ConvoCoreSettings")` (3 sites), `"t:Texture2D ConvoCoreLogo"`.
- **Save system:** `ConvoCoreKeys.DefaultPrefix = "convocore."` (+ derived keys), `ConvoCoreSettings.SaveKeyPrefix` default + validation reset, serialized `SaveKeyPrefix: convocore.` in 3 settings assets, save dir `"ConvoCoreSaves"` (3 code sites + `"ConvoCoreSaves_Tests"`), `resourcesRoot = "ConvoCore/Dialogue"` (code + serialized + Excel utility).
- **Editor prefs/session keys:** `ConvoCore_PendingActionName`, `ConvoCore_PendingAssetPath`, `ConvoCore.SettingsEditor.ActiveTab`, `ConvoCore.ExcelSourceFoldout.*`, `ConvoCore_BulkImport_InputFolder`/`_OutputFolder`, `ConvoCoreAudioManifest_Page_`/`_Size_`.
- **Menus/UI:** 20 `[MenuItem]` paths, 27 `[CreateAssetMenu]` menuNames (all rooted `ConvoCore/`), ActionCreator code-gen template, window titles ("About ConvoCore", "ConvoCore Bulk Import"), labels/tooltips, `"ConvoCore Excel: "` and `"ConvoCore Bulk Import: "` log prefixes, 20 distinct `[ConvoCore*]` log tags.
- **HelpURLs:** 91 `[HelpURL]` attributes — `/convocore/` route AND Doxygen-mangled `_1_1ConvoCore_1_1` type names; generator `ConvoCoreDev/Assets/Editor/ConvoCoreHelpURLInjector.cs` hardcodes the package id ×3 + base URL.
- **Scripting defines:** `CONVOCORE_ADDRESSABLES` (3), `CONVOCORE_FMOD` (5), `CONVOCORE_WWISE` (5), `CONVOCORE_GRAPHTOOLKIT` (2, incl. asmdef defineConstraints/versionDefines) → `WITWEAVER_*`. Absent from both projects' ProjectSettings define lists.
- **YAML template:** `NewConversation.yml` header + `DefaultYamlTemplate` in `ConvoCoreYamlAssetCreator.cs`. Sample `.yml` content is clean (branding in filenames only). Schema keys unchanged.
- **Serialized misc:** `m_Name` values, GameObject names (`ConvoCore Conversation`, `_ConvoCore_WorldPoint_`), UnityEvent string args (`value: ConvoCore2DSampleUI`), `FilePath`/`SourceYamlAssetPath` values, `EditorBuildSettings` scene path, CodeCoverage assembly filter.
- **Docs/CI:** website_docusaurus authored docs (817 hits/50 files, 3 branded filenames), `docusaurus.config.ts` (route `convocore`), `llms.txt` (61), `robots.txt`, redirect stubs, `Doxyfile` (PROJECT_NAME, SITEMAP_URL), `.github/workflows/docs.yml` (10 hits), `README.md`, `CHANGELOG.md`, `CLAUDE.md`.

## Scale

- Case-insensitive `convocore` in hand-maintained scope (package, both projects' Assets/Packages/ProjectSettings, authored docs, CI, root docs): **3,018 hits in 295 files**.
- Case-variant split (package + project assets): `ConvoCore` 6,899 · `convocore` 151 · `convoCore` 93 · `CONVOCORE` 15.
- Tracked paths containing `convocore`: 3,145 (most in generated output; branded basenames in scope are renamed via `git mv` with `.meta` siblings).
- Package `.cs`/`.meta` pairing at baseline: complete except 2 known `Samples~` audio-provider scripts without metas (pre-existing defect, see MIGRATION-NOTES).
- GUID snapshot at baseline: 564 GUIDs (multiset must be identical after all renames).

## Intentional retentions (excluded from the final zero-hit grep)

- `ConvoCoreDev`, `ConvoCoreTest` folder/project names (and paths referencing them), incl. `productName`, `.idea` files, `ConvoCoreDevHelpers` menu root.
- The checkout root folder `D:\UnityProjects\WolfstagInteractive\ConvoCore` and its segment inside the `file:` manifest path.
- Generated output: `Docs/`, `html/`, `latex/`, `website_docusaurus/build/`, Doxygen API HTML under `website_docusaurus/static/*/api` (regenerates), `*.csproj`/`*.sln`, `Library/`.
- `ThirdParty/YamlDotNet` (no branded content; GUID-referenced).

## Verification section (Gate B results, 2026-08-26)

- **Final zero-hit grep** (case-insensitive `convocore`, hand-maintained scope; excludes generated output, ThirdParty, `*.csproj`/`*.sln`, the two MIGRATION-*.md deliverables, and the documented retentions `ConvoCoreDev`/`ConvoCoreTest`/checkout-root):
  - Content grep: **zero hits** apart from the retained `.idea/.gitignore` line `/.idea.ConvoCore.iml` (Rider project named after the retained checkout-root folder).
  - Tracked-path audit: **zero branded paths** outside retained project folders and generated output.
  - Residual bare-stem audit: **zero** `convo*` identifiers in C# source (colloquial locals `convo`/`convoData`/`convoKey`/... were renamed to `conversation*` in a follow-up commit; `cubemapConvolution`/"conversation" untouched).
- **Gate A** (after folder rename only): Unity 6000.5.6f1 batchmode exit 0, zero error patterns; package resolved at the new folder path.
- **Gate B** (after all rename commits): batchmode exit 0, zero error patterns (`error CS`, missing script, `Unknown managed type referenced`, GUID conflict, package resolution); all six renamed assemblies built (`WitWeaver`, `WitWeaverEditor`, `WitWeaverGraphEditor`, `WolfstagInteractive.WitWeaver.SaveSystem`(+`.Editor`, +`.Tests.Editor`)); stale `ConvoCore` assemblies removed from `Library/ScriptAssemblies`.
- **EditMode tests**: 13/13 passed, 0 failed (save-system collection tests exercise the renamed save keys/dir/extensions).
- **GUID integrity**: 564-entry GUID multiset identical before/after; every renamed `.cs` kept its `.meta`; all 1,388 file renames staged as 100%-similarity renames.
- **Serialized managed references**: all 17 `type: {class:, ns:, asm:}` lines verified in WitWeaver form (7× SingleConversationInput, 2× FlipbookAnimationPayload, 8× graph node/graph classes).
- **Mirror integrity**: dev `SampleAssetsRepo` ↔ package `Samples~` diff shows only the 2 known missing `Samples~` metas and pre-existing per-copy `SourceYamlAssetPath` values.
- **Manual in-editor smoke test**: run by the user 2026-08-26 — everything working as expected (Testing scene Input intact, sample scenes, YAML import watcher, Excel round-trip, save/load, settings from Resources, graph editor, menus).
