---
sidebar_position: 2
title: Conversation Data
---

# Conversation Data

`WitWeaverConversationData` is the ScriptableObject that represents a single conversation. It holds the compiled dialogue lines, the participant character profiles, localization data, and a stable identity GUID. It is the core data asset that the WitWeaver runner reads from at runtime.

---

## What Is a ScriptableObject?

:::note
ScriptableObjects are Unity's way of storing data as project assets, like a config file you can edit in the inspector and reference from multiple scenes. A `WitWeaverConversationData` asset lives in your project folders (not embedded in a scene), so it persists across scene loads and can be shared between multiple WitWeaver components.
:::

---

## Creating a Conversation Data Asset

Right-click in the **Project** panel and choose:

**Create → WitWeaver → Conversation Dialogue Object**

Name it something that matches the conversation (e.g., `TownSquareGreeting`, `BossIntroduction`). Then assign your YAML file to the **Conversation Yaml** field and fill in the remaining fields below.

:::tip
If you are using the YAML Watcher, it will detect your `.yml` file and automatically assign the **Conversation Yaml** reference when you save the file in your editor. You still need to create the asset manually first.
:::

---

## Inspector Fields

| Field | Description |
|---|---|
| **Conversation Title** | A human-readable display name used in editor tools and debug output. Does not need to match any YAML key. |
| **Conversation Key** | Must exactly match the root `ConversationName` key in your YAML file. This is how the parser identifies the right conversation in the YAML. |
| **Conversation Yaml** | Drag your `.yml` TextAsset here. The YAML Watcher auto-populates this when it detects a matching file. |
| **File Path** | The path used when loading from Resources or Addressables (relative to the Resources root, no file extension). |
| **Conversation Participant Profiles** | All `WitWeaverCharacterProfileBaseData` assets for the characters who appear in this conversation. Every `CharacterID` referenced in the YAML must have a corresponding profile in this list. |
| **Dialogue Lines** | The compiled list of `DialogueLineInfo` objects. This is generated from your YAML file - do not edit it directly. |

:::warning
Never manually edit the **Dialogue Lines** list in the inspector. It is auto-generated from your YAML file. Any manual changes will be overwritten the next time the YAML Watcher runs or you invoke **Force Validate Dialogue Lines** from the context menu.
:::

---

## Conversation GUID

Every `WitWeaverConversationData` asset has a **Conversation GUID**, a stable, unique identifier that is automatically generated the first time the asset is validated (in `OnValidate`). The GUID is stored in the `_conversationGuid` serialized field and exposed via the `ConversationGuid` property.

The GUID is used as the key for all save data. The save system stores conversation progress and visited-line records under this GUID, not under the asset name or file path. This means you can rename the asset or move it in your project without breaking existing saves.

### Viewing the GUID

The Conversation GUID is displayed in the inspector on the asset. You do not need to copy or manage it manually.

### When the GUID is generated

The GUID is generated once, the first time Unity calls `OnValidate` on the asset (which happens on import, on first selection in the Project panel, or when you force a reimport). After that it never changes unless you explicitly regenerate it.

:::warning
Calling **Regenerate Guid** assigns a brand new GUID to the asset. This **breaks all existing save data** for this conversation: any saved progress, visited lines, or variable snapshots stored under the old GUID will no longer be found.

Only regenerate the GUID if you intentionally want to invalidate old saves, for example after making dialogue changes so significant that restoring old save states would be incorrect or confusing. A context menu item on the asset exposes this action.
:::

---

## Context Menu Actions

Right-click the asset in the Project panel to access these actions:

| Action | What it does |
|---|---|
| **Force Validate Dialogue Lines** | Reparses the YAML file and rebuilds the entire `DialogueLines` list. Run this after editing your YAML if the Watcher did not pick up the change automatically. |
| **Sync All Representation Object References** | Rebuilds references to character representation assets if they have become disconnected (e.g., after moving files). |
| **Regenerate Guid** | Assigns a new GUID. See the warning above before using this. |
| **Debug Character Profiles** | Logs character and representation info to the Unity console. Useful for diagnosing missing profile or representation issues. |

---

## What Happens at Runtime

When `WitWeaver.PlayConversation(data)` is called, it invokes `data.InitializeDialogueData()`, which performs the following steps:

1. **Load YAML**: The asset's YAML TextAsset (or file path) is passed to `WitWeaverYamlLoader`, which provides the raw YAML string.
2. **Parse**: `WitWeaverYamlParser` parses the YAML string into an intermediate representation of dialogue lines.
3. **Match and update**: The parsed lines are matched to the existing `DialogueLines` list by `LineID`. If a line's `LineID` matches, its localized text and expression data are updated in-place. If no `LineID` is present, lines are matched by index.
4. **Ready**: The `DialogueLines` list is now up to date for the current language and YAML state, and the runner begins iteration.

This two-step design (pre-compiled ScriptableObject + runtime YAML refresh) means the inspector-visible `DialogueLines` list serves as a stable reference that the editor and save system can inspect at any time, while the YAML remains the authoritative source of text content.

---

## Relationship to Other Assets

- **ConversationContainer**: A container can hold multiple `WitWeaverConversationData` assets and choose between them at runtime. See [Conversation Container](conversation-container).
- **Character Profiles** - Each participant listed in the YAML must have a corresponding `WitWeaverCharacterProfileBaseData` entry in the **Conversation Participant Profiles** list.
- **Dialogue Actions** - Actions are referenced per-line inside the `DialogueLineInfo` objects within `DialogueLines`. They are ScriptableObject assets assigned via the inspector or YAML directives.
