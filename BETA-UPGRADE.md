# Upgrading a project from ConvoCore to WitWeaver

The plugin has been renamed from ConvoCore to WitWeaver. This guide walks an existing project through the update with the least disruption. Follow the steps in order. The order matters.

**The one rule that protects your data: do not open Unity until step 8.** If the project opens halfway through, Unity can quietly reset some component data (details in step 5) without showing any error.

Good news up front: your conversation assets, character profiles, custom actions, and YAML/Excel files all survive this untouched. Conversation ScriptableObjects link to the plugin's scripts by internal file IDs that did not change in the rename, and the YAML format is exactly the same.

## 1. Back up first

Commit your project to version control, or copy the whole project folder. Every step below is safe, but a backup makes it free to retry.

## 2. Close Unity

Close the Unity editor for this project and keep it closed until step 8.

## 3. Check text serialization

You need your assets stored as text for step 5. In most projects this is already the case. To confirm: the setting lives in **Edit > Project Settings > Editor > Asset Serialization**, and it should say **Force Text**. If you cannot check without opening Unity, open any `.unity` scene file in a text editor: if it starts with readable YAML (`%YAML 1.1`), you are set.

## 4. Update the package entry

Open `Packages/manifest.json` in a text editor.

Delete this line:

```json
"com.wolfstaginteractive.convocore": "https://github.com/Wolfstag-Interactive/ConvoCore.git?path=WolfstagInteractive/ConvoCore",
```

Add this line in its place:

```json
"com.wolfstaginteractive.witweaver": "https://github.com/Wolfstag-Interactive/WitWeaver.git?path=WolfstagInteractive/WitWeaver",
```

The package id and the folder path inside the repository both changed, so the old entry cannot simply be updated. It has to be replaced.

## 5. Fix the type references stored inside your scenes and assets

This is the step that protects your configured data. A few fields (the **Input** field on the ConvoCore runner component in your scenes, and animation payloads on animated character representation assets) store the plugin's old type names as plain text inside the file. Unity cannot fix these on its own. If they are left stale, those fields come back empty, and the inspector will then quietly replace them with defaults.

With Unity still closed, search your `Assets` folder (all `.unity`, `.asset`, and `.prefab` files) for:

```
ns: WolfstagInteractive.ConvoCore
```

On every line found, make these two changes:

- `ns: WolfstagInteractive.ConvoCore` becomes `ns: WolfstagInteractive.WitWeaver`
- `asm: ConvoCore` becomes `asm: WitWeaver`

A typical line looks like this before:

```
type: {class: SingleConversationInput, ns: WolfstagInteractive.ConvoCore, asm: ConvoCore}
```

and like this after:

```
type: {class: SingleConversationInput, ns: WolfstagInteractive.WitWeaver, asm: WitWeaver}
```

Change only the `ns:` and `asm:` values. Leave the `class:` names and everything else on the line alone.

Your conversation ScriptableObjects do not contain any of these lines, which is why they need nothing here.

## 6. Rename the settings asset and remove old samples

**Settings asset.** The plugin now looks for its settings by the name `WitWeaverSettings`. In your file explorer (not in Unity), go to `Assets/Resources/` and rename both files as a pair:

- `ConvoCoreSettings.asset` becomes `WitWeaverSettings.asset`
- `ConvoCoreSettings.asset.meta` becomes `WitWeaverSettings.asset.meta`

Rename the existing files. Do not delete and recreate them, and do not touch what is inside them. Renaming both together keeps the asset's identity, so anything referencing it stays connected.

**Old samples.** Delete the folder `Assets/Samples/ConvoCore/` completely (with its `.meta` file). These are stale copies with old names. You can re-import fresh samples from the package later if you want them.

## 7. Update your own scripts and defines

**Your scripts.** In any code you wrote that uses the plugin, find and replace:

- `ConvoCore` with `WitWeaver` (covers namespaces like `WolfstagInteractive.ConvoCore` and type names like `ConvoCoreConversationData`)
- Types starting with a bare `Convo` also changed, for example `IConvoInput` is now `IWitWeaverInput`, and `ConvoVariableStore` is now `WitWeaverVariableStore`

**Your asmdefs.** If any of your assembly definition files list `ConvoCore` or `ConvoCoreEditor` by name in their references, change them to `WitWeaver` and `WitWeaverEditor`. References shown as GUIDs need no change.

**Scripting defines.** If you use the FMOD, Wwise, or Addressables integrations, your Player Settings contain defines like `CONVOCORE_FMOD`. Replace each with the `WITWEAVER_` version (`WITWEAVER_FMOD`, `WITWEAVER_WWISE`, `WITWEAVER_ADDRESSABLES`). You can edit these as text in `ProjectSettings/ProjectSettings.asset` while Unity is closed, or in **Player Settings** right after opening. If you skip this, those integrations silently turn off rather than erroring, so it is easy to miss.

## 8. Open Unity and verify

Open the project and let it resolve the new package and recompile. Then check:

1. The Console shows no missing script warnings.
2. Open one of your conversation ScriptableObjects in the inspector: all dialogue lines, participants, and settings are intact.
3. Select a runner component in one of your scenes: its **Input** field still shows your configuration (this confirms step 5 worked).
4. Play a conversation end to end.
5. If you use the integrations, confirm audio still fires (this confirms the defines from step 7).

As a final check, a case-insensitive search for `convocore` across your `Assets` folder should find nothing.

## What you do not need to do

- **YAML and Excel files**: no changes. The dialogue format is identical.
- **Saves and editor preferences**: the save keys and folders changed name, so old ones are simply ignored. If you never shipped saves, nothing to do.
- **Anything inside your conversation assets**: they carry over as they are.

If anything looks wrong after step 8, close Unity, restore the backup from step 1, and go through the steps again in order.
