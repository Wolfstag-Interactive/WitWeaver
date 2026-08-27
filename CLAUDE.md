# ConvoCore - Claude Code Context

## Project Overview

**ConvoCore** is a Unity dialogue and conversation framework (Unity Package Manager package) built for scalable multi-character conversations. It is a tooling product, not a game-specific system. Author: Dylan Alter (Wolfstag Interactive), version 1.0.0, supports Unity 2021.3+.

Dialogue is authored in **YAML as the single source of truth**, compiled into **ScriptableObjects** for runtime and editor use. All systems are modular, replaceable, and genre-agnostic (no assumptions about UI, rendering, cameras, etc.).

---

## Repository Structure

```
ConvoCore/
├── WolfstagInteractive/ConvoCore/      # Core UPM package source
│   ├── Scripts/                        # Runtime C# scripts
│   │   ├── ConvoCoreYaml/              # YAML parsing & loading
│   │   ├── ConvoCoreContainers/        # Runtime conversation context
│   │   ├── UI/                         # UI foundation & history
│   │   └── SampleActions/             # Example dialogue actions
│   ├── Editor/                         # Custom inspectors & editor tools
│   ├── Samples~/                       # Sample assets
│   ├── ThirdParty/                     # External dependencies
│   └── package.json                    # UPM manifest
├── ConvoCoreTest/                      # Test Unity project
│   └── Assets/
│       ├── ConvoCore/                  # Package symlink/copy
│       ├── ConvoCoreCustomActions/     # Custom action extensions
│       └── Samples/                   # 2D & 3D sample scenes
├── Docs/                               # Generated Doxygen docs
├── website_docusaurus/                 # Documentation website (Docusaurus)
└── README.md
```

---

## Key Scripts

| Script | Purpose |
|---|---|
| `ConvoCore.cs` | Main conversation runner; manages state, line progression, actions, and events |
| `ConvoCoreConversationData.cs` | ScriptableObject holding dialogue data, participants, YAML refs, localization |
| `ConversationContainer.cs` | Wraps conversation data with UI configuration |
| `ConvoCoreYamlParser.cs` | Parses YAML files into dialogue structures |
| `ConvoCoreYamlLoader.cs` | Loads and manages YAML file references |
| `ConvoCoreYamlWatcher.cs` | Watches for YAML file changes in editor |
| `CharacterRepresentationBase.cs` | Base class for character visual representations |
| `SpriteCharacterRepresentationData.cs` / `PrefabCharacterRepresentationData.cs` / `AnimatedCharacterRepresentationData.cs` | Built-in representations; expressions live as GUID-keyed mapping lists on each asset |
| `BaseDialogueLineAction.cs` | Base for pre/post-line custom ScriptableObject actions |
| `ConvoCoreUIFoundation.cs` | Base UI setup |
| `ConvoCoreDialogueHistoryUI.cs` | Dialogue history display |
| `ConvoCoreLanguageManager.cs` | Language/locale management |

---

## Architecture Principles

- **YAML is the single source of truth** for dialogue content — never manually edit the compiled ScriptableObjects
- **Modular subsystems** — all systems (UI, character display, actions, localization) can be replaced without touching the core runner
- **ScriptableObject-based extensibility** — custom actions extend `BaseDialogueLineAction` or `BaseExpressionAction`
- **No game-genre assumptions** — presentation, input, cameras are fully in user's hands
- **Editor-first workflow** — custom inspectors, YAML watchers, and asset creators live in the `Editor/` folder

## YAML Dialogue Format

```yaml
ConversationName: "ExampleConversation"
Participants:
  - CharacterID: "CharacterA"
  - CharacterID: "CharacterB"
Dialogue:
  - CharacterID: CharacterA
    LocalizedDialogue:
      EN: "Hello!"
      FR: "Bonjour!"
  - CharacterID: CharacterB
    LocalizedDialogue:
      EN: "Hi there."
```

---

## Events (ConvoCore.cs)

- `StartedConversation`
- `PausedConversation`
- `EndedConversation`
- `CompletedConversation`

---

## Common Tasks

- **Add a new dialogue action**: Extend `BaseDialogueLineAction` as a ScriptableObject, implement pre/post-line hooks
- **Add a new character representation**: Extend `CharacterRepresentationBase`
- **Create a new conversation**: Use the Unity Asset Menu → Create → ConvoCore → New Conversation (generates a YAML template)
- **Add a new UI**: Extend `ConvoCoreUIFoundation` and `ConvoCoreCharacterDisplayBase`
- **Localization**: Add language keys to YAML dialogue entries; `ConvoCoreLanguageManager` handles runtime switching
