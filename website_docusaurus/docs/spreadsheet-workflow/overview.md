---
sidebar_position: 1
title: Excel Workflow
---

# Excel Workflow

WitWeaver supports authoring dialogue in `.xlsx` spreadsheets as an alternative to hand-editing YAML. The spreadsheet is the source of truth: every time you save the file in Excel (or any spreadsheet editor), Unity reimports it, generates LineIDs, writes them back to the `.xlsx`, and rebuilds the `WitWeaverConversationData` ScriptableObject automatically.

---

## Why Use a Spreadsheet

The YAML workflow gives you full control but requires comfort with the format. The spreadsheet workflow trades that flexibility for:

- Familiar tooling for writers and narrative designers who don't use code editors
- Side-by-side columns for all language translations — no scrolling between files
- Excel features like filtering, sorting, conditional formatting, and comments for review annotations
- Simpler handoff to external localisation teams who deliver updated sheets

Both workflows use the same runtime data — a spreadsheet import produces exactly the same `WitWeaverConversationData` as a YAML import.

---

## Spreadsheet Format

Each **worksheet tab** in the workbook maps to one **conversation key**. The tab name becomes the key used to look up that conversation.

### Required Columns

| Column | Header (configurable) | Description |
|---|---|---|
| Character ID | `CharacterID` | The character speaking the line. Must match a `CharacterID` registered on the `WitWeaverConversationData` asset. |
| Line ID | `LineID` | A stable unique identifier for each line. Leave blank on first import — WitWeaver generates and writes back IDs automatically. |
| Language columns | Any 2–5 letter code (e.g. `en`, `fr`, `es`) | One column per language. The header is used as the language code. Any column whose header matches the pattern `[a-zA-Z]{2,5}(-[a-zA-Z]{2,4})?` is treated as a language column. |

Any other columns are ignored by the parser (they are preserved in the file untouched).

### Example Layout

| CharacterID | LineID | en | fr | es |
|---|---|---|---|---|
| Ava | | Let's go. | Allons-y. | Vamos. |
| Jared | | Are you sure? | Tu es sûr ? | ¿Estás seguro? |
| Wolfstag | | *Howls.* | *Hurle.* | *Aúlla.* |

After the first import:

| CharacterID | LineID | en | fr | es |
|---|---|---|---|---|
| Ava | L_04d64b899837 | Let's go. | Allons-y. | Vamos. |
| Jared | L_cb8d04d73eed | Are you sure? | Tu es sûr ? | ¿Estás seguro? |
| Wolfstag | L_16a17bec0dec | *Howls.* | *Hurle.* | *Aúlla.* |

### Header Row

By default, the first row (row index `0`) is treated as the header. Change this via `ExcelHeaderRowIndex` in [Spreadsheet Settings](#spreadsheet-settings) if your sheet has title rows above the column headers.

### Multiple Conversations in One File

Use multiple tabs. Each tab produces one conversation key in the `WitWeaverConversationData`.

### Skipping Sheets

Any tab whose name starts with the `ExcelSkipSheetPrefix` (default: `_`) is ignored entirely. Use this for documentation sheets, lookup tables, or anything that should not be parsed as dialogue.

```
Conversation1      ← parsed
Conversation2      ← parsed
_README            ← skipped
_LookupTable       ← skipped
```

---

## Setup

### 1. Create a WitWeaverConversationData asset

Right-click in the Project panel and choose **Create → WitWeaver → New Conversation**. This creates a `WitWeaverConversationData` ScriptableObject.

### 2. Link the spreadsheet

Select the `WitWeaverConversationData` asset. In the Inspector, find the **Excel Source** section and assign your `.xlsx` file to the **Source Excel Asset** field.

The path is stored as a Unity asset path (e.g. `Assets/Dialogue/ForestScene.xlsx`). Moving or renaming the file inside the Unity Project panel automatically updates the stored path — the link is not broken by file renames.

### 3. Save the spreadsheet

Save the `.xlsx` in your spreadsheet editor. Unity detects the change and runs the full import pipeline automatically. Watch the Console for the result message.

:::tip
You can also trigger an import manually at any time by clicking **Import from Excel** in the **Excel Source** section of the inspector, without needing to re-save the file.
:::

---

## How the Pipeline Works

Every import runs these steps in order:

1. **Parse** — the `.xlsx` is opened as a ZIP archive. Shared strings, the workbook sheet map, and each worksheet's XML are read. Each data row is converted to a `DialogueYamlConfig` (CharacterID, LineID, LocalizedDialogue map) paired with its 1-based xlsx row number.

2. **Generate LineIDs** — any row whose `LineID` cell is empty receives a new unique ID (`L_` + first 12 hex chars of a deterministic hash).

3. **Write back LineIDs** — if any IDs were generated, the `.xlsx` file is updated atomically. Only the LineID cells that changed are modified; all other cell content, formatting, column widths, and styles are left exactly as they are. Unity reimports the updated file.

4. **Build YAML** — the parsed data is serialised into a YAML string. Each `LocalizedDialogue` block is written as a YAML flow-style mapping (`{en: "...", fr: "...", es: "..."}`) to guarantee round-trip safety.

5. **Validate YAML** — the generated YAML is parsed immediately to verify it is well-formed before it is committed anywhere.

6. **Embed YAML** — the YAML string is stored as a `TextAsset` sub-asset named `EmbeddedYaml` on the `WitWeaverConversationData` asset, and `ConversationYaml` is pointed at it.

7. **Import** — `ImportFromYamlForKey` runs for each conversation key, populating the ScriptableObject's dialogue data exactly as a YAML import would.

8. **Save** — the asset is marked dirty and saved to disk.

---

## LineID Writeback and Custom Formatting

The writeback step modifies **only the LineID column cells** in the xlsx. All other content in the file is preserved exactly, including:

- **Column widths** you have set in Excel
- **Cell styles**, number formats, fonts, and fill colours
- **Merged cells**, frozen panes, and other sheet-level properties
- **Non-dialogue sheets** (e.g. `_README`) — they are copied byte-for-byte into the output file
- **Any other columns** not used by WitWeaver

When a cell already has a LineID written in it, only the cell's text value and type are updated. The cell's style index (`s` attribute) is preserved, so conditional formatting and column formatting rules continue to apply. When a cell is blank and needs a new LineID inserted, the style of the nearest sibling cell in the same column is applied to the new cell.

:::warning
Do not manually edit LineID values. WitWeaver treats them as stable identifiers that persist across reimports and are referenced by save data. If a LineID changes or disappears, any save data referencing that line will break. Let WitWeaver generate and manage them.
:::

---

## Auto-Sync on File Save

`WitWeaverExcelWatcher` is an `AssetPostprocessor` that watches for `.xlsx` changes in the project. When an `.xlsx` file that is linked to a `WitWeaverConversationData` asset is imported (which happens automatically when the file is saved), the full pipeline runs without any manual action.

The watcher also handles **file renames and moves** inside the Unity Project panel. If you rename or move the `.xlsx` in the Project panel, the `SourceExcelAssetPath` and `SourceExcelAsset` reference on the linked `WitWeaverConversationData` are updated automatically to reflect the new path.

The watcher performs no work when no `.xlsx` files are involved in an import (e.g. when Unity reimports a texture or a script), so it adds negligible overhead to the normal import process.

---

## Formula Cells

Excel formulas (`=A1&B1`, `=UPPER(C2)`, etc.) are handled based on the `ExcelFormulaCellBehavior` setting:

| Behaviour | Effect |
|---|---|
| `UseCachedValue` *(default)* | Reads the last calculated value stored in the cell. This value is only current if the file was saved after the formula was last evaluated in Excel. LibreOffice and Google Sheets also cache values when saving as `.xlsx`. |
| `SkipRow` | Skips any row that contains at least one formula cell in a CharacterID or language column. The row does not appear in the parsed output. |
| `UseEmptyString` | Treats formula cells as empty strings. The row is included but the formula result is discarded. |

:::tip
For static dialogue content, avoid formulas in CharacterID and language columns. Formulas work well for metadata or helper columns that WitWeaver ignores, since those columns are never read.
:::

---

## Spreadsheet Settings

All spreadsheet settings live in `WitWeaverSettings` under the **Spreadsheet** tab. Open via **Tools → WitWeaver → Open Settings**.

| Field | Default | Description |
|---|---|---|
| **Excel Character ID Header** | `CharacterID` | Column header used to identify the character column. Case-insensitive. |
| **Excel Line ID Header** | `LineID` | Column header used to identify the LineID column. Case-insensitive. |
| **Excel Skip Sheet Prefix** | `_` | Sheet tabs whose names start with this prefix are not parsed. Set to empty string to disable skipping. |
| **Excel Header Row Index** | `0` | Zero-based index of the header row within each sheet. Set to `1` if row 1 is a title and row 2 contains the column headers. |
| **Excel Skip Empty Rows** | `true` | Rows where the CharacterID cell is blank are skipped. Disable to treat blank CharacterID rows as errors rather than silently skipping them. |
| **Excel Warn On Unrecognized Columns** | `false` | Logs a warning for any column whose header is not CharacterID, LineID, or a recognised language code. Useful for auditing unexpected content during initial setup. |
| **Excel Formula Cell Behavior** | `UseCachedValue` | Controls how formula cells are handled. See [Formula Cells](#formula-cells). |

---

## Troubleshooting

### "No conversation data found in 'file.xlsx'"

The parser read the file but found no usable dialogue rows. Common causes:

- The sheet tab name does not match what you expected — check for trailing spaces or special characters in the tab name.
- The `CharacterID` column header does not match `ExcelCharacterIDHeader` in settings (case-insensitive match, so `CharacterId` and `CHARACTERID` both work, but `Character ID` with a space does not match `CharacterID`).
- All rows have a blank `CharacterID` and `ExcelSkipEmptyRows` is true.
- The header row index is wrong — check `ExcelHeaderRowIndex`.

### "LineIDs were generated but could not be written back"

The `.xlsx` file could not be saved back. This usually means the file is open in Excel at the moment of import. Close the file in Excel and re-save (or click **Import from Excel** in the Inspector) to trigger a fresh run. The Console message will say the file is "out of sync" — your conversation data was imported successfully, but the LineIDs were not persisted to the spreadsheet.

### "Internal YAML generation error"

This is a WitWeaver bug. The pipeline generated YAML from the parsed data but the YAML could not be re-parsed. This should not happen with normal dialogue text. If you encounter it, check the Console for the parse error detail and report it with the contents of the affected cell(s).

### Edits to the xlsx are not triggering a reimport

Unity only fires the `AssetPostprocessor` when it detects a file change. If you are editing the file externally and Unity's auto-refresh is disabled, trigger a manual refresh via **Assets → Refresh** (or press `Ctrl+R`). Alternatively, click **Import from Excel** in the Inspector to force a pipeline run without reimporting the file.

### A character's dialogue is missing after import

Check that the `CharacterID` values in the spreadsheet exactly match the IDs registered on the `WitWeaverConversationData` asset's Participants list. The CharacterID match is case-sensitive at the data level (the column header match is case-insensitive, but the cell values are used as-is).
