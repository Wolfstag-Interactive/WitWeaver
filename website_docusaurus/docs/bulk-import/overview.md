---
sidebar_position: 1
title: Bulk YAML Import
---

# Bulk YAML Import

The Bulk Import window lets you scan a folder of YAML files, preview every conversation key found, and batch-create or batch-update `WitWeaverConversationData` ScriptableObject assets in one click. Each entry is processed independently — a parse error in one file never blocks the rest.

Use this tool when setting up a new project with many existing YAML files, or when you want to re-sync a batch of assets after making changes to the source files outside Unity.

---

## Opening the Window

**Tools → Wolfstag Interactive → WitWeaver → Bulk Import**

The window remembers your last input and output folder selections across sessions.

---

## Step 1 — Configuration

The Configuration screen collects the source folder, destination folder, and import options before scanning.

### Input YAML Folder

The folder WitWeaver will scan for `.yml` and `.yaml` files. Assign the folder using the object field (drag a folder from the Project panel) or click **Browse** to open a folder picker.

:::warning
The input folder must be inside your project's `Assets/` directory. The Scan button is disabled while the folder is invalid or empty.
:::

### Output Folder for New Assets

Where newly created `WitWeaverConversationData` assets will be saved. The folder is created automatically if it does not exist — you do not need to create it first.

When you set an input folder, the output folder defaults to a `Conversations/` sibling folder at the same level. Once you manually change the output folder, auto-defaulting stops.

:::tip
Choosing the same folder for input and output is allowed. Your YAML files and conversation assets coexist without conflict — a warning is shown as a reminder.
:::

### Recursive

When enabled (default), WitWeaver scans the input folder and all subfolders. Disable this to scan only the top-level folder.

### Asset Naming

Controls how the filename of each new `WitWeaverConversationData` asset is derived.

| Mode | Filename pattern | Best for |
|---|---|---|
| **YAML File and Key** *(default)* | `FileName_ConversationKey.asset` | Projects where the same conversation key appears in multiple YAML files — the filename makes the source file obvious. |
| **Conversation Key Only** | `ConversationKey.asset` | Projects where each conversation key is globally unique and you prefer shorter filenames. |

Both modes sanitise the name by replacing illegal characters (`/ \ : * ? " < > |`) with underscores. If a file at the generated path already exists, a numeric suffix (`_1`, `_2`, …) is appended automatically.

:::note
Asset naming only applies to **new** assets created during the import. Existing assets that are being updated retain their current filename and location.
:::

### Scanning

Click **Scan** to analyse the input folder. WitWeaver reads every YAML file, parses each one, and builds a manifest of every conversation key found. The window moves to the Preview screen with the results.

---

## Step 2 — Preview

The Preview screen shows a row for every conversation key found across all scanned YAML files.

### Summary Bar

```
Found 12 conversations in 4 YAML files. 9 new, 2 updates, 1 conflict, 0 errors.
```

An info box appears if any YAML file contains more than one conversation key, as each key produces a separate asset.

### Row Status

Each row is colour-coded to show what will happen during import.

| Icon | Status | Meaning |
|---|---|---|
| ● Green | **New** | No existing asset has this conversation key. A new asset will be created. |
| ↑ Blue | **Update** | An existing `WitWeaverConversationData` asset with this key was found. It will be re-synced from the YAML. |
| ⚠ Yellow | **Conflict** | The same conversation key appears in more than one YAML file. The row is excluded from import. |
| ✕ Red | **Error** | The file could not be read, failed to parse, or the conversation key has no dialogue lines. The row is excluded from import. |

### Table Columns

| Column | Description |
|---|---|
| Checkbox | Whether this entry is included in the import. Unchecked and disabled for Conflict and Error rows. |
| Status icon | Colour-coded status (see above). |
| Conversation Key | The key read from the YAML file. |
| Source YAML | The filename of the YAML file containing this key. |
| Lines | Number of dialogue lines parsed for this key. |
| Detail | For Conflict and Error rows, click the status badge to show the full detail message below the table. |

### Selecting Entries

**Select All** and **Deselect All** act only on New and Update rows — Conflict and Error rows are never included.

Uncheck individual rows to skip specific conversations without removing them from the manifest.

### Importing

Click **Import Selected (N)** to begin. A progress bar tracks each conversation as it is processed. When the import finishes, the window moves to the Results screen.

Clicking **Back** returns to Configuration without losing your manifest — you can adjust options and re-scan.

---

## Step 3 — Results

The Results screen summarises what happened.

```
Import complete. 9 created, 2 updated, 0 failed.
```

Each row shows the outcome icon, the conversation key, and the path to the created or updated asset. Click an asset path to ping it in the Project panel.

| Icon | Outcome |
|---|---|
| ✓ Green | Asset was created successfully. |
| ✓ Blue | Asset was updated successfully. |
| ✕ Red | The entry failed. The error message column shows the reason. |

**Show in Project** pings the output folder in the Project panel.

**New Import** clears the manifest and results and returns to Configuration, ready for another batch.

---

## How the Import Works

For each selected entry, WitWeaver runs the same pipeline used by the per-asset YAML workflow:

1. **Embed** — the YAML file's text is stored as a `TextAsset` sub-asset named `EmbeddedYaml` on the `WitWeaverConversationData`. LineIDs are generated for any line that is missing one and written back to the source file.
2. **Import** — the embedded YAML is parsed and `DialogueLineInfo` objects are created for every dialogue line under the matching conversation key.
3. **Save** — the asset is marked dirty and saved. The `SourceYamlAssetPath` is recorded so the YAML Watcher can auto-sync the asset when the source file changes later.

If an update fails partway through, the asset's dialogue lines are restored to their previous state automatically.

:::note
After a bulk import, the YAML Watcher is active for every created or updated asset. Saving a linked YAML file in Unity will automatically re-sync the corresponding asset, exactly as it does for assets set up through the standard per-asset workflow.
:::

---

## Troubleshooting

### "No YAML files found in the selected folder"

WitWeaver found no `.yml` or `.yaml` files under the input path. Check that:

- The input folder path shown under the folder field is the folder you intended (not a parent folder).
- The **Recursive** toggle is enabled if your YAML files are in subdirectories.
- The files have a `.yml` or `.yaml` extension (not `.txt` or another extension).

### A file shows as Error with "Could not read file"

The file exists on disk but could not be opened. This is unusual inside the `Assets/` folder. Check that the file is not locked by another process, and that Unity has read permission on the path.

### A conversation key shows as Conflict

The same key string appears in two or more YAML files in the scanned folder. Bulk Import cannot determine which file is authoritative, so both rows are excluded. Resolve the conflict by removing the duplicate key from one of the files, then click **Scan** again.

### A conversation key shows as Error with "has no dialogue lines"

The key exists in the YAML but its dialogue block is empty. Add at least one dialogue line to the key in the source file before importing.

### An asset was created but has no dialogue lines in the Inspector

This can happen if an error occurred during the embed or import step after the asset file was created. The Console will have a matching error. Select the asset, check the Console for detail, fix the YAML, and re-run the bulk import — the entry will appear as **Update** on the next scan.

### After import, edits to a YAML file are not auto-syncing

Confirm that `SourceYamlAssetPath` is set on the `WitWeaverConversationData` asset (visible in the Inspector under the YAML section). If the field is blank, open the Bulk Import window, scan the same folder, and run the import again — the path is written during import.

---

| I want to… | Go here |
|---|---|
| Understand the YAML format for dialogue files | [YAML Overview →](../yaml-reference/yaml-overview) |
| Set up YAML auto-sync for a single asset | [YAML Loading →](../yaml-reference/yaml-loading) |
| Author dialogue in a spreadsheet instead | [Excel Workflow →](../spreadsheet-workflow/overview) |
| Configure WitWeaver-wide settings | [WitWeaver Settings →](../settings/witweaver-settings) |
