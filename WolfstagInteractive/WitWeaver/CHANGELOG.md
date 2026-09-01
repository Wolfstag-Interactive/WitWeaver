# Changelog

All notable changes to the WitWeaver package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Removed

- **Addressables-by-key and Resources YAML sources.** `TextSourceKind` now contains only `AssignedTextAsset` and `Persistent`. The removed sources were text-only patch channels made redundant by the embedded `EmbeddedYaml` sub-asset: bundled content ships inside the Conversation Data asset, DLC or remote content ships by making the Conversation Data asset itself addressable (the embedded YAML travels with it), and post-ship text overrides go through the Persistent source. The Addressables-by-key path also never activated in player builds (loader settings were only assigned in editor code), so no shipped project can depend on it. Along with it: `WitWeaverSettings.AddressablesEnabled`, `WitWeaverSettings.AddressablesKeyTemplate`, the unused `WitWeaverSettings.resourcesRoot`, `WitWeaverAddressablesUtil`, and the `WITWEAVER_ADDRESSABLES` scripting define. Old `SourceOrder` entries for the removed kinds are skipped at runtime and pruned by `OnValidate`.
  - **Migration:** projects that loaded YAML from a Resources folder should open each Conversation Data asset and use **Link & Embed** so the YAML is embedded; projects that wanted Addressables delivery should mark the Conversation Data asset itself as addressable.

### Added

- **Unified "Dialogue Source" inspector section with the spreadsheet workflow as a first-class source.** The Conversation Data inspector's YAML Linking box and Excel Source foldout are replaced by one Dialogue Source section with YAML | Spreadsheet tabs (selection persists via EditorPrefs). Both formats can stay linked simultaneously. A shared block below the tabs always shows the embedded YAML, an "Embedded From" provenance line (which linked source produced the current embed, tracked via new hidden editor-only fields), a cross-source staleness warning when the *other* linked source file is newer than the embed (last writer wins is now visible instead of silent), a dual-link summary, and the source-neutral sync status. The Spreadsheet tab gains Ping Source and Clear Link buttons, and its import-result box now also shows results from watcher-triggered auto-imports (with a relative timestamp), not just the manual button.
- **Generic "Spreadsheet" terminology, including code identifiers.** All user-facing text (inspector labels and tabs, menu items, console messages, settings labels, tooltips, docs) now says "Spreadsheet" instead of "Excel"; the file format remains .xlsx. The "Export to Excel" menu items are now "Export to Spreadsheet". Code identifiers were migrated as well, as a breaking rename with no serialization shim: classes `WitWeaverExcelUtilities`/`Watcher`/`Parser`/`Writer`/`ImportStatus`/`ExportMenuItems` and `WitWeaverYamlToExcelExporter` are now `WitWeaverSpreadsheet*` and `WitWeaverYamlToSpreadsheetExporter`; the conversation-data fields `SourceExcelAsset`/`SourceExcelAssetPath` are now `SourceSpreadsheetAsset`/`SourceSpreadsheetAssetPath`; the settings fields `Excel*` (CharacterIDHeader, LineIDHeader, SkipSheetPrefix, HeaderRowIndex, SkipEmptyRows, WarnOnUnrecognizedColumns, FormulaCellBehavior) and the `ExcelFormulaCellBehavior` enum are now `Spreadsheet*`. Serialized keys in the shipped settings and sample assets were renamed in place; external projects with values under the old field names would lose them (none exist for this pre-release path, hence no `FormerlySerializedAs`).
- **Automatic dialogue-text sync on YAML save.** When the YAML watcher (or the inspector's Link & Embed / Sync From Source button) embeds updated YAML, the compiled `DialogueLines` now get their localized text refreshed in the same pass, so the inspector matches the source file without pressing Reload YAML Now. The sync is text-only by design: it matches lines by LineID (legacy index fallback), preserves authored per-language `AudioClip` references, never adds/removes/reorders lines, never touches actions or representations, and is skipped during play mode. When the YAML's structure has drifted (lines added or removed), a console warning and a persistent warning box on the Conversation Data inspector point to **Import From YAML For Key**; the inspector also offers **Sync Text Now** when only text is behind. The inspector embed button now routes through the watcher's validated embed path (parse + LineID checks) instead of its own weaker copy.

### Changed

- The spreadsheet import pipeline now embeds through the same validated embed helper as the YAML path and records embed provenance. Spreadsheet-initiated imports no longer write generated LineIDs back to a linked YAML source file (`ImportFromYamlForKey` gained an optional `suppressSourceWriteBack` parameter); they were already written back to the .xlsx itself. Multi-sheet workbooks now log a warning that only the last sheet's lines are retained (pre-existing limitation, now visible).
- The standalone "Import From YAML For Key" button appears contextually: in the sync-status drift warning when an embed is present, or in Line Data Controls only when no embed exists (FilePath-based loading). Graph-authored assets show the drift warning without the button and are pointed to the graph bake instead.
- The Persistent Override "File Path" field now strips accidental `.yml`/`.yaml` extensions on edit and shows a warning with a fix button for pre-existing values (an extension made the override lookup resolve to `<name>.yml.yml` and never match).
- The YAML watcher now also repairs the `SourceYaml` object reference and embed provenance when the linked file is moved or renamed (parity with the spreadsheet watcher).
- `WitWeaverYamlLoader.Settings` is now a property that lazily resolves through `WitWeaverSettings.Instance` (`Resources.Load` in builds). Previously it was a field that was never assigned outside the editor, so custom `SourceOrder` and `VerboseLogs` were silently ignored in player builds. Assigning it at boot still overrides resolution.
- `WitWeaverConversationData.FilePath` is now exclusively the persistent-override stem, resolved as `persistentDataPath/WitWeaver/Dialogue/<FilePath>.yml` (or `.yaml`). Auto-fill now uses the bare file name instead of a `WitWeaver/Dialogue/` prefixed path (the old prefix produced a doubled directory in the persistent lookup). Existing prefixed values keep working; their override files just live one level deeper.
- Conversation Data inspector: the File Path section is now "Persistent Override (optional)" with the `Allow Persistent Overrides` toggle and the computed override path; the stale StreamingAssets browse flow is gone (StreamingAssets was never a load source).
- `WitWeaverYamlLoader.LoadAsync` and `LoadCoroutine` remain for API compatibility but now complete synchronously; both remaining sources involve no asynchronous work.
- **Renamed product from ConvoCore to WitWeaver.** Full API rename — namespaces, class prefixes (including bare `Convo`-stem types), assembly definitions, package id (`com.wolfstaginteractive.witweaver`), menu paths, settings asset, save keys/directory/file extensions, scripting defines (`WITWEAVER_*`), and docs URLs; no behavior changes. Pre-release, no migration required.

### Added

- **Collection variables** (`CollectionInt`, `CollectionString`) in `WitWeaverVariableStore`: named groups of sub-entries (string sub-key → int or string) for inventory-style state such as item counts, relationship maps, discovered locations, etc.
  - Full sub-key API on the store: `SetCollectionInt/String`, `TryGetCollectionInt/String`, `HasCollectionEntry`, `RemoveCollectionEntry`, `GetCollectionCount`, `GetCollectionKeys`, `ClearCollection`, `ResetVariable`. The backing dictionary is never exposed.
  - Authored Collections are never modified at runtime; the first change works on an in-memory copy, and `ResetVariable` restores the authored defaults.
  - Change events fire once per mutation with the affected sub-entry value as payload; `ClearCollection` fires a single event.
  - Inspector support: reorderable Collection Defaults list with duplicate/empty sub-key validation, plus a Play Mode summary row that highlights orange once the Collection has been modified during the session.
  - Save-snapshot schema bumped **1.0 → 1.1** (list-of-pairs representation). Existing 1.0 saves load unchanged through a pass-through migration step.

### Changed

- Variable Store inspector: the read-only column is now labeled **Read Only** (previously **R/O**).
