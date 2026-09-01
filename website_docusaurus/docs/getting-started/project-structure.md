---
sidebar_position: 3
title: Project Structure
---

# Project Structure

This page explains how WitWeaver's files are organized inside the package itself, where to put your own project files, and how the framework's assembly structure works. Understanding this layout will help you avoid common file-placement mistakes and make it easier to navigate the codebase as your project grows.

---

## Package contents

When WitWeaver is installed, its files live under the `WolfstagInteractive/WitWeaver/` folder inside Unity's package cache (or in your local clone, if installed from disk). You will not normally need to edit anything in here, but knowing what each subfolder contains is useful for understanding how the framework is organized.

```
WolfstagInteractive/WitWeaver/
├── Scripts/          # Runtime C# code (the core framework)
├── Editor/           # Unity editor tools
├── Samples~/         # Optional sample assets
├── ThirdParty/       # Bundled third-party libraries
└── package.json      # UPM manifest
```

### `Scripts/`

All runtime C# code lives here. This is everything that gets compiled into your game build. Key subfolders:

| Subfolder | Contents |
|---|---|
| `WitWeaverYaml/` | YAML parsing (`WitWeaverYamlParser.cs`) and file loading/watching |
| `WitWeaverContainers/` | Runtime conversation context and container wrappers |
| `UI/` | Base classes for building your display layer (`WitWeaverUIFoundation`, history UI) |
| `SampleActions/` | Example dialogue actions you can reference when writing your own |

### `Editor/`

Unity editor tools that are **not** included in game builds. These are the scripts that power the custom inspectors, asset creation menu items, YAML hot-reload watcher, and editor windows. You reference this folder's code only from editor scripts in your own project.

### `Samples~/`

Optional sample scenes, prefabs, and assets. The tilde (`~`) in the folder name is a Unity convention that tells the importer to **skip this folder by default**; the contents are not added to your project automatically.

:::note
The tilde suffix (`Samples~`) tells Unity not to auto-import the folder. To import a sample, open **Window → Package Manager**, find WitWeaver in the list, click on it, and go to the **Samples** tab. Each sample has an **Import** button. Importing copies the sample files into `Assets/Samples/WitWeaver/<version>/` in your project, where you can freely edit them.
:::

### `ThirdParty/`

Contains **YamlDotNet**, the open-source YAML parser library that WitWeaver uses internally. It is bundled with the package so you do not need to install it separately. You generally do not need to interact with YamlDotNet directly.

### `package.json`

The UPM manifest file. It declares the package name, version, display name, dependencies, and other metadata that Unity reads when installing the package.

---

## Your project's files

WitWeaver does not dictate where you put your own files, but the following layout is recommended for keeping things organized as your project grows.

### YAML dialogue files

Place your `.yml` files anywhere in your `Assets/` folder. There is no required location: the YAML text is embedded into each Conversation Data asset in the editor, so nothing needs to live in a `Resources/` or `StreamingAssets/` folder for runtime loading. Two common conventions:

- `Assets/Dialogue/` - simple flat structure for smaller projects
- `Assets/Dialogue/<Chapter>/` - grouped by chapter or area for larger projects

:::tip
If you have many conversations, consider organizing them by chapter, level, or character: `Assets/Dialogue/Chapter1/`, `Assets/Dialogue/NPCs/`, etc. YAML files are just text assets; Unity treats them the same regardless of where they live.
:::

### Character Profile assets

Recommended location: `Assets/WitWeaver/Characters/`

Each Character Profile is a ScriptableObject asset that stores the character's ID, display name, portrait sprites, and other metadata. Keeping them in a dedicated folder makes them easy to find and assign in inspectors.

### Conversation Data assets

Recommended location: `Assets/WitWeaver/Conversations/`

These ScriptableObjects hold the compiled dialogue data for each conversation (parsed from your YAML). They reference the YAML TextAsset and the Character Profile assets for each participant.

### WitWeaverSettings

This asset **must** live at exactly:

```
Assets/Resources/WitWeaverSettings.asset
```

WitWeaver loads it at runtime using `Resources.Load("WitWeaverSettings")`. If the asset is moved out of `Resources/`, the framework will not find it and will create a new default one in its place (losing any customizations you made). Do not move or rename this file.

If the file does not exist when you first open the settings window, WitWeaver creates it automatically at the correct path.

---

## Assembly definitions

WitWeaver ships with two **assembly definition** (`.asmdef`) files that control how the framework's scripts are compiled.

| Assembly | Included in builds | Purpose |
|---|---|---|
| `WolfstagInteractive.WitWeaver` | Yes (all platforms) | The runtime framework - everything your game needs at runtime |
| `WolfstagInteractive.WitWeaverEditor` | No (editor-only) | Inspector tools, asset creators, YAML watcher - stripped automatically |

:::note
**What are assembly definitions?** Assembly definitions group scripts into separate compilation units. Think of each assembly as its own mini-DLL: it compiles independently from the rest of your project, and you control exactly which other assemblies it can reference. The main practical benefits are faster compile times (only changed assemblies recompile) and clear boundaries between editor-only code and runtime code.

Most small projects never need to think about them; Unity handles it automatically. You only need to act if you write **custom scripts that reference WitWeaver types** and get a "type not found" or "unknown namespace" error, which usually means your script's assembly is missing a reference to `WolfstagInteractive.WitWeaver`.

To add the reference: select your own `.asmdef` asset → Inspector → **Assembly Definition References** → click **+** → pick `WolfstagInteractive.WitWeaver`.
:::

### Referencing WitWeaver from your own assembly

If your project uses assembly definitions (or if you create one for your custom dialogue code), add a reference to `WolfstagInteractive.WitWeaver` in your assembly's definition file. Editor-only scripts that use WitWeaver's editor tools also need a reference to `WolfstagInteractive.WitWeaverEditor`, and that assembly definition must have **Editor** checked under Platforms so it is stripped from builds.

:::warning
Do **not** reference `WolfstagInteractive.WitWeaverEditor` from a runtime (non-editor) assembly. Editor assemblies are stripped from builds - any runtime code that references them will fail to compile when you build your project. Keep editor references inside scripts under an `Editor/` folder that has its own editor-only assembly definition.
:::

:::info[For Advanced Users]
The Save System ships in a third assembly: `WolfstagInteractive.WitWeaver.SaveSystem`. The dependency direction is strictly one-way:

```
SaveSystem  →  WitWeaver  (allowed)
WitWeaver   →  SaveSystem (never - would create a circular dependency)
```

If you are building custom systems that need to communicate with both the core runner and the save system, keep this direction in mind. Any **interface** that both assemblies need to share must live in the core `WolfstagInteractive.WitWeaver` assembly; for example, `IWitWeaverStartContextProvider` is defined there and implemented by `WitWeaverConversationSaveManager` in the SaveSystem assembly. The same pattern applies to any extension points you create that cross this boundary.

The Save System editor tools live in a fourth assembly (`WolfstagInteractive.WitWeaver.SaveSystem.Editor`) that references all three of the above, plus `WolfstagInteractive.WitWeaverEditor` and YamlDotNet. Note that assembly references are **not transitive**: if your editor assembly needs YamlDotNet, you must reference it explicitly even if a runtime assembly you depend on already does.
:::

---

## Summary

| Location | What goes there |
|---|---|
| `WolfstagInteractive/WitWeaver/Scripts/` | WitWeaver runtime source (do not edit) |
| `WolfstagInteractive/WitWeaver/Editor/` | WitWeaver editor tools (do not edit) |
| `Assets/Dialogue/` | Your YAML dialogue files (recommended) |
| `Assets/WitWeaver/Characters/` | Your Character Profile assets (recommended) |
| `Assets/WitWeaver/Conversations/` | Your Conversation Data assets (recommended) |
| `Assets/Resources/WitWeaverSettings.asset` | Global settings - do not move this file |

---

## Next steps

- [Quick Start →](./quick-start) - If you haven't already, walk through creating your first conversation.
- [YAML Format →](../yaml-reference/yaml-format) - Full reference for the dialogue YAML syntax.
- [WitWeaver Component →](../core-systems/witweaver-component) - Deep dive into the main conversation runner.
