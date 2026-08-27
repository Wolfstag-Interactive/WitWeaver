---
sidebar_position: 5
title: YAML Loading
---

# YAML Loading

At runtime, WitWeaver needs to find the YAML text for a conversation before it can parse and play it. The `WitWeaverYamlLoader` handles this by trying up to four different sources in a configurable priority order. This page explains each source, when to use it, how to configure the load order, and the three loading APIs available to your code.

---

## The four sources

### AssignedTextAsset (recommended for shipped games)

The YAML text is embedded directly into the `WitWeaverConversationData` ScriptableObject as a `TextAsset` reference. When Unity builds your game, the TextAsset is bundled with it. At runtime, reading the dialogue involves no file I/O - the text is already in memory.

In the Unity editor, the `WitWeaverYamlWatcher` keeps this TextAsset up to date automatically. Every time you save a `.yml` file, the watcher:

1. Reads the new file content.
2. Assigns it as the `ConversationYaml` TextAsset on the linked Conversation Data asset.
3. Calls `InitializeDialogueData()` to reparse and update the `DialogueLines` list.
4. Saves and reimports the asset.

You do not need to manually press a button or trigger a reimport. The embedded content is always in sync with the source file while the editor is open.

**This is the recommended source for shipped games.** Zero runtime I/O, no dependencies on file paths or external systems, and no build configuration required.

---

### Persistent

Loads YAML from the device's local storage at the following path:

```
Application.persistentDataPath/WitWeaver/Dialogue/<FilePath>.yml
```

`FilePath` is the value of the **File Path** field on the Conversation Data asset.

If a file exists at this path, WitWeaver reads it and uses it instead of any bundled content. If the file does not exist, this source is considered unavailable and WitWeaver falls through to the next source in the priority order.

**Use case:** hotfixing dialogue in a live game without shipping a full patch. Write updated YAML files to the device's persistent storage (via a download system, a debug tool, or a live-ops pipeline), and WitWeaver will pick them up automatically on the next conversation start.

:::warning
Persistent storage is writable and device-specific. If you ship a dialogue patch via this mechanism and the player clears their app data or reinstalls, the patched files are gone and WitWeaver falls back to the bundled content. Build your live-ops pipeline to re-download patches on app start if they are still active.
:::

---

### Addressables

Loads YAML using Unity's Addressables system. The addressable key is taken from the **File Path** field on the Conversation Data asset.

Requires two things:
- The **Addressables** package must be installed in your project.
- The `WITWEAVER_ADDRESSABLES` scripting define symbol must be added in **Edit → Project Settings → Player → Other Settings → Scripting Define Symbols**.

If both conditions are met, WitWeaver calls into the Addressables runtime to locate and load the text asset.

:::warning
Using the synchronous `WitWeaverYamlLoader.Load()` method with an Addressables source calls `WaitForCompletion()` internally. This blocks the main thread until the asset is ready and can cause a visible frame hitch, especially on slower devices or when loading from a remote bundle. Use `LoadAsync()` or `LoadCoroutine()` when the Addressables source is in your priority order.
:::

:::note
If you add the `WITWEAVER_ADDRESSABLES` scripting define but the Addressables package is not installed, the Addressables code path is compiled out and WitWeaver treats this source as always unavailable. No error is logged. If you expect Addressables loading and it silently falls back to Resources, verify that the package is present in the Package Manager.
:::

---

### Resources

Loads via `Resources.Load<TextAsset>(FilePath)`, using the **File Path** field on the Conversation Data asset as the path relative to a `Resources/` folder.

:::note
Any file placed inside a folder named `Resources/` anywhere in `Assets/` can be loaded by name at runtime using `Resources.Load`. The `FilePath` field is the path from the Resources folder to the file, without the file extension. For a file at `Assets/Dialogue/Resources/Conversations/TownSquare.yml`, the `FilePath` value would be `Conversations/TownSquare`.
:::

The Resources source is always available and requires no extra packages or scripting defines. It is a reliable fallback for projects that have not set up Addressables.

:::tip
If you are just getting started and want dialogue to work without any extra configuration, use Resources as your primary source. Add the YAML files to a `Resources/Dialogue/` folder, set the `FilePath` on each Conversation Data asset, and WitWeaver will find them at runtime without any further setup.
:::

---

## Source priority order

The order in which WitWeaver tries each source is configured in **WitWeaverSettings** under the **Source Order** list. WitWeaver works through the list from top to bottom and uses the first source that successfully returns content.

The default order is:

1. AssignedTextAsset
2. Persistent
3. Addressables
4. Resources

You can reorder, enable, or disable sources in the settings asset to match your project's delivery strategy. For example:

- A single-platform game with no live-ops: remove Persistent and Addressables, keep AssignedTextAsset and Resources.
- A live-ops-heavy game: put Persistent first so hotfix files take priority over all bundled content.
- A game with DLC dialogue packs: put Addressables first and use Addressables groups per DLC.

---

## The loading API

`WitWeaverYamlLoader` exposes three entry points. All three populate the `DialogueLines` on the provided `WitWeaverConversationData` asset after loading.

### Synchronous load

```csharp
WitWeaverYamlLoader.Load(conversationData);
```

Attempts all configured sources in priority order and returns when done. Appropriate for AssignedTextAsset and Resources, which are non-blocking. Avoid for Addressables (see warning above).

### Async load (Task-based)

```csharp
await WitWeaverYamlLoader.LoadAsync(conversationData);
```

An awaitable version for use with `async`/`await` patterns. Does not block the main thread while waiting for Addressables or other asynchronous sources. Use this in `async MonoBehaviour` methods or async initialization routines.

### Coroutine-based load

```csharp
yield return StartCoroutine(WitWeaverYamlLoader.LoadCoroutine(conversationData, OnDone));
```

A coroutine overload that accepts an optional callback delegate (`Action OnDone`) called when loading completes. Use this when you need coroutine-compatible async loading - for example, in a loading screen that must keep the UI responsive while dialogue data is fetched.

```csharp
private IEnumerator LoadAndPlay(WitWeaverConversationData conversationData)
{
    bool loaded = false;
    yield return StartCoroutine(
        WitWeaverYamlLoader.LoadCoroutine(conversationData, () => loaded = true));

    if (loaded)
        _conversationRunner.PlayConversation();
    else
        Debug.LogError("Failed to load dialogue for: " + conversationData.name);
}
```

---

## YAML Watcher (editor only)

`WitWeaverYamlWatcher` runs exclusively in the Unity editor. It is not compiled into builds.

The watcher registers with Unity's `AssetModifiedProcessor` to receive callbacks whenever assets are imported or modified. When a `.yml` file change is detected and that file is linked to a Conversation Data asset:

1. The watcher reads the new file content.
2. It assigns the content as the `ConversationYaml` TextAsset on the linked asset.
3. It calls `InitializeDialogueData()` to reparse the YAML and update `DialogueLines`.
4. It calls `EditorUtility.SetDirty()` and `AssetDatabase.SaveAssets()` to persist the changes.

At build time, the `ConversationYaml` TextAsset embedded in each Conversation Data asset is bundled with the game. The watcher never runs and is not referenced in the build - the AssignedTextAsset source reads the already-embedded text directly from the ScriptableObject.

:::info[For Advanced Users]
`WitWeaverYamlLoader.Settings` is a static property that holds the active `WitWeaverSettings` instance. By default WitWeaver discovers the settings asset via `Resources.Load<WitWeaverSettings>("WitWeaverSettings")`. You can override this at application startup by assigning a custom instance:

```csharp
WitWeaverYamlLoader.Settings = myCustomSettings;
```

This is useful when the settings asset itself is delivered as downloadable content - for example, a DLC language pack that ships its own source order configuration. Assign the downloaded settings early in your boot sequence, before any conversations are loaded, and all subsequent loads will use it.
:::
