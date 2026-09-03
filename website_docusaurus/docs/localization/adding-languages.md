---
sidebar_position: 2
title: Adding Languages
---

# Adding Languages

This page walks through the full process of adding a new language to your WitWeaver™ project, from registering the language in settings through testing it at runtime.

---

## Step 1: Add the Language to WitWeaverSettings

WitWeaver will only accept `SetLanguage` calls for language codes that appear in the **Supported Languages** list. This is the authoritative list of languages your project exposes.

1. Open **Tools → WitWeaver → Open Settings**. This opens the `WitWeaverSettings` asset in the inspector.
2. In the **Supported Languages** list, click the **+** button and type your new language code (e.g., `FR` for French, `DE` for German, `pt-BR` for Brazilian Portuguese).
3. Save the asset (Ctrl+S or File → Save).

:::warning
If you skip this step and add translations to your YAML files but do not add the code to Supported Languages, `SetLanguage("FR")` will log a warning and silently do nothing. The UI will remain in the current language. Always register the language in settings first.
:::

---

## Step 2: Add Translations to Your YAML Files

Open each YAML file that needs a translation and add a new key to every `LocalizedDialogue` block. The key must match the code you added in Step 1 (matching is case-insensitive).

Before (English only):

```yaml
ConversationName: "VillageGreeting"
Participants:
  - CharacterID: "Innkeeper"
Dialogue:
  - CharacterID: Innkeeper
    LocalizedDialogue:
      EN: "Good morning, traveller. What can I do for you?"
  - CharacterID: Innkeeper
    LocalizedDialogue:
      EN: "We have rooms available. Will you be staying the night?"
```

After (English and French):

```yaml
ConversationName: "VillageGreeting"
Participants:
  - CharacterID: "Innkeeper"
Dialogue:
  - CharacterID: Innkeeper
    LocalizedDialogue:
      EN: "Good morning, traveller. What can I do for you?"
      FR: "Bonjour, voyageur. Que puis-je faire pour vous ?"
  - CharacterID: Innkeeper
    LocalizedDialogue:
      EN: "We have rooms available. Will you be staying the night?"
      FR: "Nous avons des chambres disponibles. Passerez-vous la nuit ?"
```

:::tip
Partial translations are fully supported. If a line has no `FR` key, WitWeaver automatically falls back to `EN` (or the first available key). Ship what you have and fill in the rest as translation work progresses. You do not need 100% coverage before enabling a language.
:::

---

## Step 3: Reimport and Validate

After saving your YAML file, the **YAML Watcher** detects the change and triggers a reimport automatically in the editor. The `WitWeaverConversationData` asset is updated with the new localization entries.

To verify the import succeeded:

1. Select the `WitWeaverConversationData` asset for the conversation you edited.
2. In the inspector, expand the **Dialogue Lines** list and select a line.
3. Confirm that `LocalizedDialogues` contains entries for both `EN` and `FR` (or whichever codes you added).

If the reimport does not trigger automatically, right-click the YAML file in the Project panel and select **Reimport**.

---

## Step 4: Test the Language at Runtime

Enter Play Mode and switch languages via code:

```csharp
// Switch to French.
WitWeaverLanguageManager.Instance.SetLanguage("FR");
```

Then start a conversation. The French text should appear for any line that has an `FR` entry. Lines without `FR` will display the English fallback.

If you want to switch language while a conversation is already playing and see the change immediately:

```csharp
WitWeaverLanguageManager.Instance.SetLanguage("FR");
_runner.UpdateUIForLanguage("FR");
```

`UpdateUIForLanguage` re-resolves the current line's localized text and re-renders it without restarting the conversation.

---

## Step 5: Build a Language Selector (Optional)

A language selector UI lets players pick their language from a list. Populate it dynamically from `WitWeaverLanguageManager` so it always reflects the project's current Supported Languages:

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using WolfstagInteractive.WitWeaver;

public class LanguageSelectorUI : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown _dropdown;

    // Reference to the active WitWeaver runner, if any.
    [SerializeField] private WitWeaver _runner;

    private List<string> _languages;

    private void Start()
    {
        _languages = WitWeaverLanguageManager.Instance.GetSupportedLanguages();

        _dropdown.ClearOptions();
        foreach (string lang in _languages)
            _dropdown.options.Add(new TMP_Dropdown.OptionData(lang));

        // Set the dropdown to the currently active language.
        string current = WitWeaverLanguageManager.Instance.CurrentLanguage;
        int currentIndex = _languages.FindIndex(
            l => string.Equals(l, current, System.StringComparison.OrdinalIgnoreCase));
        _dropdown.SetValueWithoutNotify(currentIndex >= 0 ? currentIndex : 0);
        _dropdown.RefreshShownValue();

        _dropdown.onValueChanged.AddListener(OnLanguageSelected);
    }

    private void OnDestroy()
    {
        _dropdown.onValueChanged.RemoveListener(OnLanguageSelected);
    }

    private void OnLanguageSelected(int index)
    {
        string selected = _languages[index];
        WitWeaverLanguageManager.Instance.SetLanguage(selected);

        if (_runner != null)
            _runner.UpdateUIForLanguage(selected);
    }
}
```

Assign this component to your settings menu and wire up the `TMP_Dropdown` and (optionally) the `WitWeaver` runner in the inspector.

---

## Checklist

Use this checklist when adding any new language:

- [ ] Language code added to **Supported Languages** in `WitWeaverSettings`.
- [ ] All YAML files updated with translations for the new code (or at least the most important conversations).
- [ ] `WitWeaverConversationData` assets reimported and validated in the inspector.
- [ ] Language tested in Play Mode with `SetLanguage`.
- [ ] Fallback behavior confirmed for any lines that are not yet translated.
- [ ] Language selector UI updated if your project has one.
- [ ] `WitWeaverSettings.asset` is in a `Resources/` folder so it is included in the build.

---

## Regional Variants

WitWeaver supports regional language variants out of the box. Use IETF BCP 47 format for regional codes:

```yaml
LocalizedDialogue:
  EN: "Hello."
  fr: "Bonjour."
  fr-CA: "Bonjour, eh."
```

If the active language is `"fr-CA"` and a line has an `fr-CA` key, it is used. If the line only has `fr`, WitWeaver's fallback chain strips the region suffix and matches `fr` automatically. This means you do not need to duplicate translations for every regional variant; add the base locale and only add regional variants where the text genuinely differs.

Add each regional code you want players to be able to select to the Supported Languages list separately (e.g., both `"fr"` and `"fr-CA"` if you want to support both).

:::info[For Advanced Users]
The fallback chain in `WitWeaverDialogueLocalizationHandler` is: exact match → base locale of requested → `"en"` → base locale of `"en"` → first available key. This means even with a completely untranslated line, the worst case is displaying the English text rather than a blank or error, as long as at least one `EN` key exists. The `IsFallback` flag and `ErrorMessage` field on the returned `LocalizedDialogueResult` let you audit exactly which fallback path was taken for each line during testing.
:::
