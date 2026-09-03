---
sidebar_position: 2
title: Unity Audio Setup
---

# Unity Audio Setup

The `UnityAudioSource` backend is the quickest way to add voice audio to a WitWeaver™ conversation. Drag `AudioClip` assets directly into the manifest, and WitWeaver handles everything else — including automatically adding an `AudioSource` component to the runner's GameObject if one is not already present.

---

## Prerequisites

- A working `WitWeaverConversationData` asset with at least one dialogue line.
- `AudioClip` assets for the lines you want to voice.

No additional packages or components are required.

---

## Step-by-Step Setup

### 1. Create an Audio Manifest

Right-click in the Project panel and choose:

**Create → WitWeaver → Audio Manifest**

Name it something that matches the conversation (e.g. `TownGreeting_AudioManifest`).

---

### 2. Configure the Manifest

Select the manifest asset. In the inspector:

1. Set **Audio Backend** to `UnityAudioSource`.
2. Set **Mode** to `Conversation Driven`.
3. Drag your `WitWeaverConversationData` asset into the **Source Conversation** field.
4. Click **Sync Rows From Conversation**.

WitWeaver generates one entry row per dialogue line per locale. Each row shows a language tag on the left and an `AudioClip` drag-drop slot on the right.

:::tip
If a line has multiple locales in the YAML (e.g. `EN` and `FR`), it gets one row per locale so you can assign separate voice clips for each language. If a line has no locales defined, it gets a single language-agnostic row.
:::

---

### 3. Assign Audio Clips

Drag your `AudioClip` assets into the per-row slots. You can:

- Assign a **different clip per locale** (e.g. English VO on the `EN` row, French VO on the `FR` row).
- Assign a **clip to the language-agnostic row only** (`(any)` label) if you have one voice track for all languages.

:::note
A row does not require both a Clip and a Reference. If you drag a clip into the `Clip` slot, that is sufficient. The `Reference` slot (for `WitWeaverUnityAudioReference` ScriptableObjects) is an optional secondary mechanism for sharing one clip asset across many lines and is not needed for typical setup.
:::

---

### 4. Assign the Manifest to the Conversation

Open your `WitWeaverConversationData` asset. Drag the manifest into the **Audio Manifest** field.

---

### 5. Set Presentation Mode

On the same `WitWeaverConversationData` asset, set **Default Presentation Mode** to the mode you want:

- `AudioAndText` — plays audio while showing the dialogue text.
- `AudioOnly` — plays audio and hides the dialogue text.
- `TextOnly` — shows text only (audio will not be triggered).

You can override this per-line using the **Presentation Mode** dropdown inside each dialogue line entry.

---

### 6. Set Progression Method (Optional)

For voice-acted lines, set the **Progression Method** on individual lines (or in bulk) to `AudioComplete`. WitWeaver will wait until the clip finishes playing before advancing, so the conversation pacing follows the audio.

:::warning
If you set a line's mode to `AudioOnly` and leave the progression as `UserInput`, WitWeaver will automatically promote the progression to `AudioComplete` at runtime (or to `Timed` if no provider is present). This prevents the conversation from stalling on a line with no visible UI element to click.
:::

---

### 7. Press Play

That's it. When the conversation starts:

1. WitWeaver detects the manifest's `UnityAudioSource` backend.
2. If no `WitWeaverUnityAudioProvider` component is already present on the runner's GameObject, it automatically adds one along with the required `AudioSource`.
3. For each line, WitWeaver resolves the best-matching clip from the manifest and calls `PlayVoiceLine` on the provider.

---

## Auto-Provisioning

You never need to manually add an `AudioSource` or `WitWeaverUnityAudioProvider` component when using the `UnityAudioSource` backend. WitWeaver adds them at the start of `PlayConversation` if they are missing.

If you **do** want to pre-configure the `AudioSource` (e.g. set the spatial blend, mixer group, or output channel), add the components manually and WitWeaver will use the one that is already there rather than creating a new one.

---

## Inspector Reference

### Audio Manifest (UnityAudioSource mode)

| Field | Description |
|---|---|
| **Audio Backend** | Must be set to `UnityAudioSource` for this workflow. |
| **Mode** | `Conversation Driven` or `Standalone`. |
| **Source Conversation** | The `WitWeaverConversationData` to sync rows from. Required in `Conversation Driven` mode. |
| **Sync Rows From Conversation** | Button. Rebuilds the entry list from the source conversation. Preserves any clips already assigned. |

### Entry Row (UnityAudioSource mode)

| Column | Description |
|---|---|
| **Language** | The locale this clip plays for (e.g. `EN`, `FR`). `(any)` means it matches all locales. |
| **Clip** | The `AudioClip` to play. Drag-drop from the Project panel. |

---

## Troubleshooting

**No audio plays, no errors in console.**

- Check that the manifest is assigned to the **Audio Manifest** field on the conversation data.
- Check that the conversation's **Default Presentation Mode** is not `TextOnly`.
- Check that the entry for the relevant line and language has a clip assigned (look for the warning count at the top of the manifest inspector).

**Audio plays but cuts off early / does not advance.**

- The progression method is likely `Timed` with `TimeBeforeNextLine = 0`. Switch to `AudioComplete` so the conversation waits for the clip to finish.

**`AudioSource` settings (volume, spatial blend) are not applied.**

- Manually add an `AudioSource` component to the runner's GameObject before pressing Play. Set your desired values. WitWeaver will find and use it rather than creating a new default one.

**Clips are lost after clicking Sync.**

- Clips are preserved by `(LineID, Language)` key. If a line's `LineID` changes (e.g. after a YAML re-import that renumbers lines), the key no longer matches and clips appear unassigned. Keep `LineID` values stable in your YAML to avoid this.
