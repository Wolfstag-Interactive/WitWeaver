---
sidebar_position: 1
title: ConvoCore Settings
---

# ConvoCore Settings

`ConvoCoreSettings` is a sealed `ScriptableObject` that controls global ConvoCore configuration. It is the single configuration file for the entire framework. Language support, YAML source resolution order, save system keys, debug logging, and history renderer profiles are all managed here.

---

## Opening the Settings

**Tools → ConvoCore → Open Settings**

This menu item finds the existing `ConvoCoreSettings.asset` in your project and pings it in the Project panel, then selects it so it appears in the inspector. If no asset exists, it creates one automatically at `Assets/Resources/ConvoCoreSettings.asset`.

:::tip
You rarely need to create this asset manually. ConvoCore auto-creates it with sensible defaults the first time it is needed. Use **Tools → ConvoCore → Open Settings** as your entry point; it always finds the correct asset regardless of where it lives in your project.
:::

---

## How ConvoCore Finds the Settings at Runtime

ConvoCore uses the following resolution order to find `ConvoCoreSettings`:

1. **Already loaded**: if `ConvoCoreSettings.Instance` is already set in memory, that value is returned immediately (no I/O).
2. **Resources folder**: `Resources.Load<ConvoCoreSettings>("ConvoCoreSettings")`. This is the path that works in builds.
3. **AssetDatabase search** (editor only): `AssetDatabase.FindAssets("t:ConvoCoreSettings")`, using the first result found anywhere in the project.
4. **Auto-create** (editor only): if nothing is found, a new asset is created at `Assets/Resources/ConvoCoreSettings.asset` and a warning is logged.

For **builds**, ensure the asset is inside a `Resources/` folder so Unity includes it in the build output. An asset outside `Resources/` will not be found at runtime and ConvoCore will log an error.

---

## All Settings Fields

### Language Settings

| Field | Type | Default | Description |
|---|---|---|---|
| **Supported Languages** | `List<string>` | `["EN"]` | The full list of language codes your project supports. `SetLanguage` only accepts codes that appear in this list. Drives language selector UIs. |
| **Current Language** | `string` | `"EN"` | The language active when the game starts. Must be a value from Supported Languages; if it is not, `OnValidate` resets it to the first entry in the list. |

### YAML Loading

| Field | Type | Default | Description |
|---|---|---|---|
| **Source Order** | `TextSourceKind[]` | `[AssignedTextAsset, Persistent, Addressables, Resources]` | Ordered list of sources ConvoCore tries when loading YAML text. The first source that succeeds wins. See [Source Order](#source-order) below for details. |
| **Resources Root** | `string` | `"ConvoCore/Dialogue"` | Base path prepended when loading YAML from the Resources folder, using the `FilePath` set on a `ConvoCoreConversationData` asset. For example, a `FilePath` of `"Intro"` loads `Resources/ConvoCore/Dialogue/Intro`. |
| **Addressables Enabled** | `bool` | `false` | Enable Addressables-based YAML loading. Requires the `CONVOCORE_ADDRESSABLES` scripting define to be set in **Project Settings → Player → Scripting Define Symbols**. |
| **Addressables Key Template** | `string` | `"{filePath}.yml"` | Format string for the Addressables address. `{filePath}` is replaced with the `FilePath` value from the `ConvoCoreConversationData` asset. For example, `"dialogue/{filePath}.yaml"` with `FilePath = "ForestIntro"` yields `"dialogue/ForestIntro.yaml"`. |

#### Source Order

The `TextSourceKind` enum controls which source ConvoCore tries first. The available values are:

| Value | What it does |
|---|---|
| `AssignedTextAsset` | Uses the `ConversationYaml` TextAsset directly assigned on the `ConvoCoreConversationData` asset. Works in editor and builds. The most direct option. |
| `Persistent` | Loads from `Application.persistentDataPath` using the `FilePath` as the filename. Allows device-side YAML overrides (hot-patching dialogue without a build). |
| `Addressables` | Loads via the Addressables system using the formatted key template. Requires `AddressablesEnabled = true` and the scripting define. |
| `Resources` | Loads via `Resources.Load` using `ResourcesRoot + "/" + FilePath`. Requires the YAML file to be inside a `Resources/` folder in your project. |

The runner tries each entry in `SourceOrder` in sequence and stops at the first that returns a valid result. If no source succeeds, the conversation data remains unpopulated and an error is logged.

### Debug

| Field | Type | Default | Description |
|---|---|---|---|
| **Verbose Logs** | `bool` | `false` | Enables detailed log output across all ConvoCore systems: conversation runner state transitions, YAML parsing steps, localization resolution, expression application, and save system operations. |

:::warning
**Verbose Logs** can produce hundreds of log entries per conversation. It is intended for development and debugging only. Disable it before making a production build; leaving it on will flood the console and may affect performance, particularly on mobile devices.
:::

### Save System

| Field | Type | Default | Description |
|---|---|---|---|
| **Save Key Prefix** | `string` | `"convocore."` | Prefix prepended to all keys written by the ConvoCore save system. Use this to namespace ConvoCore data within your game's save file. Only letters, digits, `.`, `_`, and `-` are allowed. An empty value resets to `"convocore."`. |
| **Enable Save System** | `bool` | `true` | Toggles the entire save system. When `false`, `ConvoCoreConversationSaveManager` components are inactive and no save/load operations occur. |
| **Enable Variable Store** | `bool` | `true` | Toggles `ConvoVariableStore` functionality. When `false`, runtime variable reads and writes are skipped. |
| **Enable Language System** | `bool` | `true` | Toggles `ConvoCoreLanguageManager` initialization. When `false`, the language system is not initialized and all dialogue falls back to the first available key in each line's `LocalizedDialogue` map. |

### Dialogue History Renderers

| Field | Type | Description |
|---|---|---|
| **History Renderer Profiles** | `List<ConvoCoreHistoryRendererProfile>` | The set of renderer profiles available to `ConvoCoreDialogueHistoryUI` components in your scenes. Each profile pairs a renderer type (e.g., plain text, rich text, prefab) with an output type (e.g., TMP text field, scrollable list). |

Renderer profiles are created as separate assets (**Create → ConvoCore → History Renderer Profile**) and added to this list. A `ConvoCoreDialogueHistoryUI` component references a profile by name to know how to render its history output.

---

## OnValidate Behavior

Whenever you modify the settings asset in the editor (or when Unity reimports it), `OnValidate` runs automatically. It enforces these constraints:

- **Supported Languages must not be empty.** If the list is empty, `"EN"` is added automatically.
- **Current Language must be in the list.** If `CurrentLanguage` is not found (case-insensitive) in `SupportedLanguages`, it is reset to `SupportedLanguages[0]`.
- **Save Key Prefix must be valid.** If the prefix is empty, it resets to `"convocore."`. If any character is not a letter, digit, `.`, `_`, or `-`, a warning is logged (the value is not automatically corrected; fix it manually).
- **History Renderer Profiles are cleaned.** Any `null` entries in the profiles list are removed.

---

## Static Instance

`ConvoCoreSettings` exposes a `static Instance` property that returns the currently loaded settings asset:

```csharp
ConvoCoreSettings settings = ConvoCoreSettings.Instance;
Debug.Log(settings.CurrentLanguage);
```

In the editor, `Instance` is also populated via `[InitializeOnLoadMethod]` so it is available immediately when the editor starts, without waiting for a scene to load.

---

## Best Practices

**Keep the asset in Resources.** Place `ConvoCoreSettings.asset` at `Assets/Resources/ConvoCoreSettings.asset`. This is the only path that works in builds. An asset elsewhere in the project is found by the editor via `AssetDatabase`, but will not be included in a build.

**One settings asset per project.** ConvoCore uses `AssetDatabase.FindAssets("t:ConvoCoreSettings")` and takes the first result. If you have multiple assets (e.g., from duplicating), the one used at runtime depends on the search result order, which is not deterministic. Delete any duplicate assets.

**Do not change Save Key Prefix after shipping.** Save data is keyed on the prefix. Changing the prefix in a shipped build means existing save data will not be found and players will lose their progress. If you need to rename the prefix, write a migration step that reads the old keys and writes them to the new ones before updating the setting.

:::info[For Advanced Users]
`ConvoCoreSettings` is `sealed`; you cannot subclass it. If you need to extend the configuration surface, the recommended pattern is to create your own `ScriptableObject` settings asset alongside `ConvoCoreSettings` and reference it from your custom systems. ConvoCore's systems read only from `ConvoCoreSettings` and will not be aware of your extended asset.

The `HistoryRendererProfiles` property returns an `IReadOnlyList<ConvoCoreHistoryRendererProfile>`. To add or remove profiles at runtime, use the `AddRendererProfile` and `CleanRendererProfiles` methods rather than casting to the internal list. This preserves the null-cleaning invariant that `OnValidate` enforces.
:::
