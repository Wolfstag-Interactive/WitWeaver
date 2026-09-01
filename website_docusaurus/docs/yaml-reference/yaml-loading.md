---
sidebar_position: 5
title: YAML Loading
---

# YAML Loading

At runtime, WitWeaver needs the YAML text for a conversation before it can parse and play it. The `WitWeaverYamlLoader` resolves that text from two sources in a configurable priority order: the YAML embedded in the Conversation Data asset, and an optional device-side override file. This page explains both sources, the load order, the loading API, and how to ship dialogue as DLC or remote content.

---

## What runtime YAML loading can (and cannot) change

When a conversation initializes, WitWeaver parses the loaded YAML and merges only the localized dialogue text into the already-serialized `DialogueLines`, matched by `LineID`. Conversation structure, choices, actions, character representations, and audio references all come from the Conversation Data asset itself.

This means any runtime YAML override can fix typos, rewrite lines, or update translations, but it cannot add lines or change conversation flow. To change structure after shipping, deliver an updated Conversation Data asset instead (see [Shipping conversations as DLC or remote content](#shipping-conversations-as-dlc-or-remote-content)).

---

## The two sources

### AssignedTextAsset (default)

The YAML text is embedded directly into the `WitWeaverConversationData` ScriptableObject as a `TextAsset` sub-asset named `EmbeddedYaml`. When Unity builds your game, the embedded text is bundled inside the asset. At runtime, reading the dialogue involves no file I/O; the text is already in memory.

In the Unity editor, the `WitWeaverYamlWatcher` keeps this TextAsset up to date automatically. Every time you save a `.yml` file that is linked to a Conversation Data asset, the watcher:

1. Reads the new file content and validates it (parse check, LineID check).
2. Replaces the `EmbeddedYaml` sub-asset and assigns it as the `ConversationYaml` reference.
3. Saves and reimports the asset.

You do not need to press a button or trigger a reimport. The embedded content stays in sync with the source file while the editor is open.

**This is the default and recommended source.** Zero runtime I/O, no dependency on file paths, folders, or external systems, and no build configuration required. Because the embedded YAML is a sub-asset, it also travels with the Conversation Data asset into asset bundles and Addressables groups automatically.

---

### Persistent (post-ship text overrides)

Loads YAML from the device's local storage at the following path:

```
Application.persistentDataPath/WitWeaver/Dialogue/<FilePath>.yml
```

If no `.yml` file exists there, the loader also tries the `.yaml` extension. `FilePath` is the value of the **File Path** field in the **Persistent Override** section of the Conversation Data asset, without an extension.

Two conditions gate this source:

- **Allow Persistent Overrides** must be enabled on the Conversation Data asset (it is on by default).
- A file must actually exist at the resolved path. If not, the loader falls through to the next source in the priority order.

**Use case:** hotfixing dialogue text in a live game without shipping a patch. Write updated YAML files to the device's persistent storage (via a download system, a debug tool, or a live-ops pipeline) and WitWeaver picks them up on the next conversation start. It is also handy for on-device iteration: push a `.yml` file to a test device and see the new text without rebuilding.

:::warning
Persistent storage is writable and device-specific. If you ship a dialogue patch this way and the player clears their app data or reinstalls, the patched files are gone and WitWeaver falls back to the embedded content. Build your live-ops pipeline to re-download active patches on app start.
:::

:::note
Remember the merge rule above: a persistent override can only change dialogue text, not conversation structure.
:::

---

## Source priority order

The order in which WitWeaver tries the sources is configured in **WitWeaverSettings** under the **Source Order** list. WitWeaver works through the list from top to bottom and uses the first source that returns content.

The default order is:

1. AssignedTextAsset
2. Persistent

Reorder the list to make device-side overrides win (put Persistent first for live-ops-heavy projects), or remove Persistent entirely if your project never uses overrides. To disable overrides for a single conversation instead, turn off **Allow Persistent Overrides** on that asset.

:::note
Projects upgrading from earlier WitWeaver versions may have `Addressables` or `Resources` entries in a saved Source Order. Those source kinds no longer exist; the loader skips the stale entries and the settings inspector removes them the next time the asset is validated. See the [migration notes](#migration-from-earlier-versions) below.
:::

---

## The loading API

`WitWeaverYamlLoader` exposes three entry points. All three resolve the sources in priority order and return the raw YAML text as a string (`null` if nothing was found). They do **not** parse the YAML or populate `DialogueLines`; that happens in `WitWeaverConversationData.InitializeDialogueData()`, which calls the loader internally and then merges the parsed text into the serialized lines.

### Synchronous load

```csharp
string yaml = WitWeaverYamlLoader.Load(conversationData);
```

The primary entry point. Both sources resolve synchronously (in-memory text or a small local file read), so this is safe to call anywhere.

### Async load (Task-based)

```csharp
string yaml = await WitWeaverYamlLoader.LoadAsync(conversationData);
```

Kept for API compatibility with async initialization flows. It completes synchronously; there is no longer any asynchronous source behind it.

### Coroutine-based load

```csharp
yield return WitWeaverYamlLoader.LoadCoroutine(conversationData, text => { /* ... */ });
```

Kept for API compatibility with coroutine-based flows. The callback is an `Action<string>` that receives the loaded YAML text, or `null` if no source returned content:

```csharp
private IEnumerator LoadAndPlay(WitWeaverConversationData conversationData)
{
    string yaml = null;
    yield return StartCoroutine(
        WitWeaverYamlLoader.LoadCoroutine(conversationData, text => yaml = text));

    if (!string.IsNullOrEmpty(yaml))
        _conversationRunner.PlayConversation();
    else
        Debug.LogError("Failed to load dialogue for: " + conversationData.name);
}
```

In practice you rarely call the loader yourself: `PlayConversation` triggers `InitializeDialogueData()`, which loads, parses, and merges in one step.

:::note
When no source returns content, the loader logs a warning only if **Verbose Logs** is enabled in WitWeaverSettings. `InitializeDialogueData()` always logs an error in that case.
:::

---

## Shipping conversations as DLC or remote content

Because the embedded YAML is a sub-asset of the Conversation Data ScriptableObject, the whole conversation (text, structure, actions, references) is one self-contained asset. To deliver conversations outside the base build:

1. Mark the `WitWeaverConversationData` asset as Addressable (or place it in an asset bundle), using whatever grouping fits your delivery strategy (one group per DLC pack, per chapter, per language pack, and so on).
2. Load the asset through your content system (`Addressables.LoadAssetAsync<WitWeaverConversationData>` or your bundle loader).
3. Hand it to the conversation runner as usual.

No WitWeaver-specific configuration is required: no scripting defines, no key templates, no loader settings. The embedded YAML travels with the asset, and updating the asset through your content pipeline updates both text and structure.

Choosing between the two post-ship mechanisms:

| Need | Mechanism |
|---|---|
| Fix or retranslate dialogue text in the live game | Persistent override file |
| Add or change conversations, lines, choices, or actions | Updated Conversation Data asset via Addressables or bundles |

---

## YAML Watcher (editor only)

`WitWeaverYamlWatcher` runs exclusively in the Unity editor. It is not compiled into builds.

The watcher receives callbacks whenever assets are imported or modified. When a `.yml` file change is detected and that file is linked to a Conversation Data asset:

1. The watcher reads the new file content.
2. It parses and validates the YAML, ensuring every line has a `LineID` (it refuses to embed if IDs cannot be ensured).
3. It replaces the `EmbeddedYaml` sub-asset and assigns it to `ConversationYaml`.
4. It refreshes the localized text of the compiled `DialogueLines` for lines that match by `LineID`, so the inspector reflects your edit immediately. This sync is text-only: it never adds, removes, or reorders lines and never touches actions, representations, or audio references.
5. It saves the asset so the change persists.

If you added or removed lines in the YAML, the text sync cannot represent that by itself: the console and the Conversation Data inspector show a structure-drift warning, and you run **Import From YAML For Key** on the asset to rebuild the line list (authored per-line settings are carried over by LineID).

At build time, the embedded TextAsset in each Conversation Data asset is bundled with the game. The watcher never runs and is not referenced in the build; the AssignedTextAsset source reads the already-embedded text directly from the ScriptableObject.

:::info[For Advanced Users]
`WitWeaverYamlLoader.Settings` is a static property holding the active `WitWeaverSettings` instance. It resolves lazily through `WitWeaverSettings.Instance`, which loads the asset via `Resources.Load<WitWeaverSettings>("WitWeaverSettings")` in builds. You can override it at application startup by assigning a custom instance:

```csharp
WitWeaverYamlLoader.Settings = myCustomSettings;
```

This is useful when the settings asset itself is delivered as downloadable content, for example a language pack that ships its own source order configuration. Assign the downloaded settings early in your boot sequence, before any conversations are loaded.
:::

---

## Migration from earlier versions

Earlier versions of WitWeaver offered two additional sources that loaded raw YAML text by path: **Resources** (`Resources.Load` using the File Path field) and **Addressables-by-key** (an Addressables text lookup behind the `WITWEAVER_ADDRESSABLES` define). Both have been removed:

- They could only ever patch text (see the merge rule above), which the Persistent source already covers with far less setup.
- Delivering conversations remotely is better served by making the Conversation Data asset itself Addressable, which updates text and structure together.
- The Addressables-by-key path never activated in player builds due to a settings-resolution bug, so no shipped project can depend on it.

If your project loaded YAML from a `Resources/` folder: open each Conversation Data asset and use **Link & Embed** (or simply resave the linked `.yml` file with the editor open) so the YAML is embedded, then remove the copies from `Resources/`. If you planned to deliver YAML through Addressables keys: mark the Conversation Data assets as Addressable instead. The `WITWEAVER_ADDRESSABLES` define, `Addressables Enabled`, `Addressables Key Template`, and `Resources Root` settings no longer exist and can be removed from your project.
