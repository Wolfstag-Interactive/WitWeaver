---
sidebar_position: 5
title: Save Providers
---

# Save Providers

A save provider handles the actual reading and writing of save data to a storage backend. The provider is decoupled from the rest of the save system behind the `IWitWeaverSaveProvider` interface, so you can swap storage backends without changing any gameplay code.

---

## Built-in providers

WitWeaver™ ships with two built-in providers. Select between them via the **Use Yaml** checkbox on your `WitWeaverSaveManager` asset.

### JsonFileWitWeaverSaveProvider (default)

Serializes the game snapshot as JSON and writes it to the device's persistent data directory.

**File locations**:
- Game saves: `Application.persistentDataPath/WitWeaverSaves/<slot>.witweaver.json`
- Settings: `Application.persistentDataPath/WitWeaverSaves/settings.witweaver.json`

:::tip
On Windows, `Application.persistentDataPath` is typically `C:\Users\<YourName>\AppData\LocalLow\<CompanyName>\<ProductName>`. Log `Application.persistentDataPath` in the Unity Console to see the exact path for your project. On Android and iOS, Unity maps this to the platform-appropriate sandboxed location.
:::

### YamlFileWitWeaverSaveProvider

Same behaviour as the JSON provider, but writes YAML files instead.

**File locations**:
- Game saves: `Application.persistentDataPath/WitWeaverSaves/<slot>.witweaver.yml`
- Settings: `Application.persistentDataPath/WitWeaverSaves/settings.witweaver.yml`

:::tip
YAML save files are human-readable. During development, open them in VS Code or any text editor to inspect or manually edit save state. This makes it straightforward to reproduce specific game states for testing without having to replay through the game. Switch to JSON before shipping if you want smaller files or slightly faster serialization.
:::

---

## IWitWeaverSaveProvider interface

```csharp
namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    public interface IWitWeaverSaveProvider
    {
        /// <summary>Write a game snapshot to the given key (slot name).</summary>
        void Save(string key, WitWeaverGameSnapshot snapshot);

        /// <summary>Read a game snapshot from the given key. Returns null if not found.</summary>
        WitWeaverGameSnapshot Load(string key);

        /// <summary>Returns true if a save exists for the given key.</summary>
        bool HasSave(string key);

        /// <summary>Delete the save for the given key. Does nothing if the key does not exist.</summary>
        void Delete(string key);

        /// <summary>Write a settings snapshot.</summary>
        void SaveSettings(string key, WitWeaverSettingsSnapshot settings);

        /// <summary>Read a settings snapshot. Returns null if not found.</summary>
        WitWeaverSettingsSnapshot LoadSettings(string key);
    }
}
```

All six methods are required. There is no partial implementation base class; implement all methods, even if some are empty stubs for your backend (e.g. a read-only cloud provider might leave `Delete` as a no-operation that logs a warning).

---

## Implementing a custom provider

The steps are:

1. Create a class that implements `IWitWeaverSaveProvider`.
2. Inject it via `_saveManager.SetProvider(new YourProvider())` before or after `Initialize()`. The new provider takes effect on the next `Save()` or `Load()` call.

### Example: PlayerPrefs provider

A minimal implementation for platforms where `PlayerPrefs` is the only viable storage (WebGL, some embedded targets):

```csharp
using WolfstagInteractive.WitWeaver.SaveSystem;
using UnityEngine;

public class PlayerPrefsWitWeaverSaveProvider : IWitWeaverSaveProvider
{
    // Prefix all keys to avoid collisions with other PlayerPrefs entries
    private const string KeyPrefix = "WitWeaver_";
    private const string SettingsKey = "WitWeaver_Settings";

    private readonly IWitWeaverSerializer _serializer;

    public PlayerPrefsWitWeaverSaveProvider(IWitWeaverSerializer serializer)
    {
        _serializer = serializer;
    }

    public void Save(string key, WitWeaverGameSnapshot snapshot)
    {
        string json = _serializer.Serialize(snapshot);
        PlayerPrefs.SetString(KeyPrefix + key, json);
        PlayerPrefs.Save();
    }

    public WitWeaverGameSnapshot Load(string key)
    {
        if (!HasSave(key)) return null;
        string json = PlayerPrefs.GetString(KeyPrefix + key);
        return _serializer.Deserialize<WitWeaverGameSnapshot>(json);
    }

    public bool HasSave(string key) =>
        PlayerPrefs.HasKey(KeyPrefix + key);

    public void Delete(string key)
    {
        PlayerPrefs.DeleteKey(KeyPrefix + key);
        PlayerPrefs.Save();
    }

    public void SaveSettings(string key, WitWeaverSettingsSnapshot settings)
    {
        string json = _serializer.Serialize(settings);
        PlayerPrefs.SetString(SettingsKey, json);
        PlayerPrefs.Save();
    }

    public WitWeaverSettingsSnapshot LoadSettings(string key)
    {
        if (!PlayerPrefs.HasKey(SettingsKey)) return null;
        string json = PlayerPrefs.GetString(SettingsKey);
        return _serializer.Deserialize<WitWeaverSettingsSnapshot>(json);
    }
}
```

Inject in your bootstrapper:

```csharp
private void Awake()
{
    _saveManager.SetProvider(new PlayerPrefsWitWeaverSaveProvider(new JsonWitWeaverSerializer()));
    _saveManager.Initialize();
    _saveManager.InitializeSettings();
}
```

### Example: async cloud provider (coroutine wrapper)

`IWitWeaverSaveProvider` is synchronous by design, matching Unity's standard ScriptableObject lifecycle. For cloud backends that require async I/O, wrap the async call in a coroutine at the call site and block (or buffer) until the result arrives. The save manager fires `OnSaveCompleted` and `OnLoadCompleted` events when operations finish, which is the right place to resume scene logic.

```csharp
public class CloudWitWeaverSaveProvider : IWitWeaverSaveProvider
{
    private readonly ICloudStorage _cloud;

    public CloudWitWeaverSaveProvider(ICloudStorage cloud)
    {
        _cloud = cloud;
    }

    public void Save(string key, WitWeaverGameSnapshot snapshot)
    {
        // Kick off the upload and handle the result asynchronously.
        // The save manager does not await this - hook OnSaveCompleted to wait for
        // confirmation if you need it.
        string json = JsonUtility.ToJson(snapshot);
        _cloud.UploadAsync(key, json)
              .ContinueWith(t => Debug.Log(t.IsFaulted
                  ? $"Cloud save failed: {t.Exception}"
                  : "Cloud save succeeded"));
    }

    public WitWeaverGameSnapshot Load(string key)
    {
        // Synchronous load from a local cache populated by an earlier async fetch.
        string cached = _cloud.GetCached(key);
        if (cached == null) return null;
        return JsonUtility.FromJson<WitWeaverGameSnapshot>(cached);
    }

    public bool HasSave(string key) => _cloud.HasCached(key);
    public void Delete(string key) => _cloud.DeleteAsync(key);

    public void SaveSettings(string key, WitWeaverSettingsSnapshot settings)
    {
        _cloud.UploadAsync("settings", JsonUtility.ToJson(settings));
    }

    public WitWeaverSettingsSnapshot LoadSettings(string key)
    {
        string cached = _cloud.GetCached("settings");
        if (cached == null) return null;
        return JsonUtility.FromJson<WitWeaverSettingsSnapshot>(cached);
    }
}
```

---

## The WitWeaverKeys class

`WitWeaverKeys` provides standardized key constants and helper methods used internally by the save system. Reference these in your own code for consistency:

```csharp
// The reserved key used for settings saves - do NOT use this as a game slot name
string settingsKey = WitWeaverKeys.Settings;

// Generate a namespaced save key for a game slot
string slotKey = WitWeaverKeys.GameSlot("slot_1");
// Result: "WitWeaver_Game_slot_1"
```

:::warning
Never use `WitWeaverKeys.Settings` as a game slot name. Passing it to `Save(WitWeaverKeys.Settings, snapshot)` will write game data to the settings file location, corrupting the settings snapshot that `InitializeSettings()` expects to read. Use `WitWeaverKeys.GameSlot()` for all game slot keys.
:::

---

## Provider selection at runtime

You can swap providers at runtime without reinitializing the save manager. This is useful for letting players choose between local and cloud saves, or for switching to an offline fallback when network connectivity is lost:

```csharp
public void SwitchToOfflineSave()
{
    _saveManager.SetProvider(new JsonFileWitWeaverSaveProvider());
    Debug.Log("Switched to local file saves.");
}

public void SwitchToCloudSave(ICloudStorage cloud)
{
    _saveManager.SetProvider(new CloudWitWeaverSaveProvider(cloud));
    Debug.Log("Switched to cloud saves.");
}
```

The currently registered conversation snapshots are retained in memory when the provider is swapped. Calling `Save()` after swapping writes to the new provider's backend.

---

## Testing providers

During development, use a `MockWitWeaverSaveProvider` that stores data in a static dictionary. This eliminates disk I/O in editor tests and makes it trivial to seed specific save states:

```csharp
public class MockWitWeaverSaveProvider : IWitWeaverSaveProvider
{
    private readonly Dictionary<string, WitWeaverGameSnapshot> _saves = new();
    private WitWeaverSettingsSnapshot _settings;

    public void Save(string key, WitWeaverGameSnapshot snapshot) =>
        _saves[key] = snapshot;

    public WitWeaverGameSnapshot Load(string key) =>
        _saves.TryGetValue(key, out var snap) ? snap : null;

    public bool HasSave(string key) => _saves.ContainsKey(key);
    public void Delete(string key) => _saves.Remove(key);

    public void SaveSettings(string key, WitWeaverSettingsSnapshot settings) =>
        _settings = settings;

    public WitWeaverSettingsSnapshot LoadSettings(string key) => _settings;
}
```
