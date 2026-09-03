---
sidebar_position: 1
title: Localization Overview
---

# Localization Overview

WitWeaver™ has a native localization system built directly into the YAML dialogue format. No external localization packages are required. Every dialogue line carries its own translations as a map of language codes to text strings, and the runtime resolves the correct text for the active language at display time.

---

## How Dialogue Localization Works

In your YAML file, each dialogue entry has a `LocalizedDialogue` block. Each key in that block is a language code and each value is the translated text:

```yaml
ConversationName: "VillageGreeting"
Participants:
  - CharacterID: "Innkeeper"
Dialogue:
  - CharacterID: Innkeeper
    LocalizedDialogue:
      EN: "Good morning, traveller. What can I do for you?"
      FR: "Bonjour, voyageur. Que puis-je faire pour vous ?"
      DE: "Guten Morgen, Reisender. Wie kann ich helfen?"
      ES: "Buenos dias, viajero. En que puedo ayudar?"
```

When the conversation runner reaches this line, it calls `WitWeaverDialogueLocalizationHandler.GetLocalizedDialogue(line)` with the current language from `WitWeaverLanguageManager`. The handler returns the appropriate string, which is then passed to the UI for display.

---

## The Fallback Chain

If the active language is not present in a line's `LocalizedDialogue` map, the handler does not fail; it walks a fallback chain and returns the best available translation:

1. **Exact match** - looks for the requested language code (case-insensitive). `"FR"` matches `"fr"`, `"Fr"`, and `"FR"` equally.
2. **Base locale** - if the requested code is a regional variant (e.g., `"fr-CA"`), strips the region suffix and tries the base (`"fr"`).
3. **English fallback**: tries `"en"` (case-insensitive) as a universal fallback.
4. **Base of English**: strips any region suffix from the English code (rarely needed).
5. **First available key**: uses whichever key is first in the map.

The result also carries an `IsFallback` flag and an `ErrorMessage` string. When `IsFallback` is `true`, WitWeaver logs a warning to the console so you can track missing translations during development. The text still displays; the conversation does not break.

:::tip
Include an `EN` entry in every dialogue line. It serves as the catch-all fallback for any language whose translation is incomplete. If a line has only an `EN` key, all non-English players will see the English text rather than a missing-translation error.
:::

---

## WitWeaverLanguageManager

`WitWeaverLanguageManager` is the singleton that tracks the active language and notifies the rest of the system when it changes. It reads its configuration from `WitWeaverSettings`.

| Member | Type | Description |
|---|---|---|
| `Instance` | `WitWeaverLanguageManager` | The singleton. Auto-created on first access. |
| `CurrentLanguage` | `string` | The currently active language code (e.g., `"EN"`). Returns `"EN"` if settings are unavailable. |
| `SetLanguage(string code)` | `void` | Changes the active language. Fires `OnLanguageChanged`. Only accepts codes that exist in the Supported Languages list (case-insensitive). |
| `GetSupportedLanguages()` | `List<string>` | Returns the full list of supported language codes from `WitWeaverSettings`. Returns `["EN"]` as a fallback if settings are missing. |
| `OnLanguageChanged` | `static Action<string>` | Fired when `SetLanguage` successfully changes the active language. The argument is the new canonical language code (as stored in Supported Languages). |

### Subscribing to Language Changes

Subscribe in `OnEnable` and unsubscribe in `OnDisable` to avoid stale event listeners:

```csharp
private void OnEnable()
{
    WitWeaverLanguageManager.OnLanguageChanged += HandleLanguageChanged;
}

private void OnDisable()
{
    WitWeaverLanguageManager.OnLanguageChanged -= HandleLanguageChanged;
}

private void HandleLanguageChanged(string newLanguage)
{
    // Update any UI that shows the current language, refresh cached text, etc.
    Debug.Log($"Language switched to: {newLanguage}");
}
```

### Initialization

`WitWeaverLanguageManager` initializes lazily on the first access to `Instance`. During initialization it:

1. Resolves `WitWeaverSettings` through `WitWeaverYamlLoader.Settings`, which loads the asset via `Resources.Load<WitWeaverSettings>("WitWeaverSettings")` in builds (in the editor it also searches the project with `AssetDatabase.FindAssets`).
2. If no settings are found, logs an error. `CurrentLanguage` returns `"EN"` as a safe fallback.

If `SupportedLanguages` is empty when initialization runs, `"EN"` is added automatically and the settings asset is marked dirty.

---

## Language Codes

Language codes in WitWeaver are arbitrary strings with no built-in list of valid values. A few rules apply:

- Matching is **case-insensitive**: `"EN"`, `"en"`, and `"En"` all refer to the same language.
- `SetLanguage` performs a **canonical lookup** against the Supported Languages list in `WitWeaverSettings`. If you call `SetLanguage("fr")` and the list contains `"FR"`, the active language is stored as `"FR"` (the canonical form from the list).
- Regional variants (`"fr-CA"`, `"pt-BR"`) are supported - the fallback chain strips the region suffix automatically.

For interoperability and readability, use ISO 639-1 two-letter codes (`EN`, `FR`, `DE`, `ES`, `PT`, `ZH`, etc.) or IETF BCP 47 tags (`fr-CA`, `pt-BR`) for regional variants. WitWeaver accepts any string, but standard codes are easier to maintain as your project scales.

---

## Changing Language at Runtime

Call `SetLanguage` at any point: before a conversation, during a conversation, or from a settings menu.

```csharp
// Switch to French.
WitWeaverLanguageManager.Instance.SetLanguage("FR");
```

If a conversation is currently playing and you want the displayed line to update immediately, also call `UpdateUIForLanguage` on the runner:

```csharp
WitWeaverLanguageManager.Instance.SetLanguage("FR");
_runner.UpdateUIForLanguage("FR");
```

This re-localizes and re-renders the current dialogue line in the new language without restarting the conversation.

:::warning
`SetLanguage` silently does nothing if the code you pass is not in the Supported Languages list. If you have added a language to your YAML files but forgotten to add it to the list in `WitWeaverSettings`, calls to `SetLanguage` for that code will log a warning and leave the active language unchanged. Always keep the Supported Languages list in settings synchronized with the codes in your YAML files.
:::

---

## Localization and Choice Labels

Choice options (`ChoiceOption.Labels`) use the same `List<LocalizedDialogue>` structure as dialogue lines. Each choice label is a list of `Language`/`Text` pairs. The same fallback chain applies. When displaying a choice prompt, WitWeaver resolves each option's label using the active language.

Manage choice labels in the Conversation Data inspector, as they are not part of the raw YAML format; choices are configured as ScriptableObject data on the `WitWeaverConversationData` asset after YAML import.

---

## LocalizedDialogueResult

`WitWeaverDialogueLocalizationHandler.GetLocalizedDialogue` returns a `LocalizedDialogueResult` struct:

| Field | Type | Description |
|---|---|---|
| `Success` | `bool` | `true` if a translation was found (including a fallback). `false` only if the line has no translations at all. |
| `Text` | `string` | The resolved text to display. Contains an error string if `Success` is `false`. |
| `UsedLanguage` | `string` | The language code that was actually used (may differ from the requested code if a fallback was applied). |
| `IsFallback` | `bool` | `true` if a fallback was used rather than the exact requested language. |
| `ErrorMessage` | `string` | A human-readable message describing which fallback was used, or the error if `Success` is `false`. |

The runner logs a `Debug.LogWarning` when `IsFallback` is `true` and a `Debug.LogError` when `Success` is `false`. Use these logs during development to audit incomplete translation coverage.

:::info[For Advanced Users]
`WitWeaverDialogueLocalizationHandler` is instantiated once per `WitWeaver` MonoBehaviour during `Awake`, with the `WitWeaverLanguageManager.Instance` passed as a constructor argument. The handler holds no state of its own; it reads `CurrentLanguage` from the manager on every call. This means language changes take effect on the very next line that is resolved, with no additional wiring required.

If you want to replace the localization strategy entirely (for example, to integrate with Unity Localization or a custom translation backend), instantiate a custom handler that wraps your backend and call it in place of the built-in one. Because `WitWeaverDialogueLocalizationHandler` is not sealed, you can also subclass it.
:::
