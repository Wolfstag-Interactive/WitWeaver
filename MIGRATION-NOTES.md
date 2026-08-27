# Migration Notes — issues noticed during the WitWeaver rename, NOT fixed

Per the migration brief, the rename is behavior-neutral. Everything below was observed during discovery/execution and deliberately left alone.

## Pre-existing defects (unrelated to the rename)

- **Two `Samples~` scripts have no `.meta` files**: `Samples~/Audio/FMODIntegration/...AudioProviderFMOD.cs` and `Samples~/Audio/WwiseIntegration/...AudioProviderWwise.cs`. Unity ignores `Samples~` at import, so users get fresh GUIDs at sample-import time — a latent packaging inconsistency (the third sample script *does* ship a meta).
- **Empty folder** `Scripts/.../ExpressionActions` (has a `.meta`, no files).
- **CLAUDE.md is stale**: claims Unity 2021.3+ support (package requires 6000.5) and predates the graph editor and save system.
- **Dev project ProjectSettings carry stale `ConvoCoreTest` identifiers** in bundle-id (`com.DefaultCompany.ConvoCoreTest`) and Metro fields; company is `DefaultCompany`.
- **MiniExcel DLL absent**: `Plugins/MiniExcel/` ships only `Microsoft.Bcl.AsyncInterfaces.dll` + the MiniExcel license; the Excel code uses raw `ZipArchive`/`XDocument`, so either the DLL or the license/folder is vestigial.
- **Namespace outliers** (renamed in place, placement/typo not fixed): `RepresentationMappingListEditor.cs` declares `WolfstagInteractive.WitWeaverEditor` (missing dot); `...YamlWatcher.cs` declares the `.Editor` namespace while living in the runtime `Scripts/` folder (editor-only via `#if`/AssetPostprocessor usage).
- **3 class/file name mismatches** for MonoBehaviours (`...CameraRelativePosition` in `CameraRelativeBehaviour.cs`, `...FollowTarget` in `FollowTargetBehaviour.cs`, `...TransformLerp` in `TransformLerpBehaviour.cs`) plus several harmless editor/interface mismatches (`EmotionIDSelectorDrawer.cs` → `ExpressionIdSelectorDrawer`, etc.).
- **Excel template internal branding**: `ExcelTemplates/...DialogueTemplateSource.xlsx` was file-renamed but its internal sheet content was not audited (binary); regenerate the template if it embeds the old name.
- **No LICENSE file** at repo root or in the package.

## Rename consequences accepted (pre-release, no compat requirement)

- Existing dev/beta saves are orphaned: save key prefix `convocore.` → `witweaver.`, save dir `ConvoCoreSaves` → `WitWeaverSaves`, extensions `.convo.json`/`.convo.yml` → `.witweaver.json`/`.witweaver.yml`.
- EditorPrefs/SessionState state under `ConvoCore*` keys is orphaned (editor UI state only).
- Users' `Resources/ConvoCore/Dialogue/` folders: the default `resourcesRoot` is now `WitWeaver/Dialogue` — the beta user must move their folder or update the setting.
- The beta user must replace `CONVOCORE_ADDRESSABLES`/`_FMOD`/`_WWISE` scripting defines with `WITWEAVER_*` and re-import samples.
- Doxygen-mangled HelpURL targets (`classWolfstagInteractive_1_1WitWeaver_1_1...`) 404 until the docs API is regenerated and redeployed.

## Intentional retentions

- `ConvoCoreDev` / `ConvoCoreTest` project folder names, `productName: ConvoCoreDev`, `.idea` project files, and the `ConvoCoreDevHelpers` menu root (dev-project tooling named after the dev project folder).
- Checkout root folder `D:\UnityProjects\WolfstagInteractive\ConvoCore` (and that segment of the `file:` path in `ConvoCoreDev/Packages/manifest.json`).
- Generated output left stale pending regeneration: `Docs/`, `html/`, `latex/`, `website_docusaurus/build/`, Doxygen API HTML under `website_docusaurus/static/witweaver/api/`.
- Root `.idea/.gitignore` line `/.idea.ConvoCore.iml` (Rider project named after the retained checkout-root folder).
- Colloquial local variables/parameters named `convo`, `convoData`, `convoKey`, `convoAssetPath`, `convoObject` — shorthand for "conversation", not product branding; left as-is (the Convo-stem TYPE rename decision covered type names and save-file extensions only).

## External follow-ups required

1. **Rename the GitHub repo** `Wolfstag-Interactive/ConvoCore` → `WitWeaver`. Until then `ConvoCoreTest` cannot resolve its package (its manifest already points at `WitWeaver.git?path=WolfstagInteractive/WitWeaver`); old `.git` URLs redirect after a GitHub rename, but the new URL only works once the rename happens. Don't open ConvoCoreTest before then.
2. **Regenerate Doxygen** into `website_docusaurus/static/witweaver/api` and redeploy the docs site (CI workflow already renamed); then run Tools → WitWeaverDevHelpers HelpURL injector and confirm a zero diff (idempotence check).
3. Optional future pass: rename `ConvoCoreDev`/`ConvoCoreTest`/checkout-root folders; requires re-editing the manifest `file:` path and reopening IDE/session workspaces.
