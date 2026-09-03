---
sidebar_position: 1
title: Audio Overview
---

# Audio Overview

WitWeaver™ includes a full voice-line audio system that is designed to be backend-agnostic. You can play dialogue audio through Unity's built-in AudioSource, FMOD Studio, Wwise, or any custom audio engine — without changing your conversation data or YAML files.

---

## Key Concepts

### Presentation Mode

Every dialogue line has a `PresentationMode` property that controls what WitWeaver does when it reaches that line:

| Mode | Behaviour |
|---|---|
| **AudioAndText** | Displays the dialogue text in the UI **and** plays the audio clip. |
| **TextOnly** | Displays text only. No audio is played even if a clip is assigned. |
| **AudioOnly** | Plays audio only. The UI text is hidden for this line. |

The conversation asset has a **Default Presentation Mode** field. New lines created by syncing from YAML inherit this value. You can override it per-line in the inspector.

:::tip
Use `AudioOnly` for cutscene-style conversations where you want voice acting without a dialogue box. Use `TextOnly` for menus, tutorials, or accessibility-first builds where audio may not be available.
:::

---

### Audio Manifest

The `WitWeaverAudioManifest` ScriptableObject maps dialogue line IDs to audio assets or event references. It sits between your conversation data and your audio engine.

**Create one** via *Right-click in Project → Create → WitWeaver → Audio Manifest*.

Assign it to the **Audio Manifest** field on your `WitWeaverConversationData` asset.

The manifest has two authoring modes:

| Mode | When to use |
|---|---|
| **Conversation Driven** | You have a YAML-based conversation. Click **Sync Rows From Conversation** to generate one entry per line per locale. Fill in the audio per row. |
| **Standalone** | You are building a voice-only sequence with no text. Add lines manually in the manifest, then click **Generate Conversation Asset** to produce a matching `WitWeaverConversationData`. |

---

### Audio Backend

The manifest has a **Backend** field that selects the audio engine:

| Backend | Description |
|---|---|
| **UnityAudioSource** | Drag-and-drop `AudioClip` assets directly into each entry row. No middleware required. |
| **FMOD** | Type the FMOD event path (e.g. `event:/VO/CharA/Line001`) into each entry row. |
| **Wwise** | Type the Wwise event name (e.g. `VO_CharA_Intro_01`) into each entry row. |
| **Custom** | All three slots are shown (Clip, Event Key, Reference). Your own `IWitWeaverAudioProvider` implementation drives playback. |

Changing the Backend field updates the inspector slot display for every entry row so you only see the fields that are relevant.

---

### Audio Progression

When WitWeaver finishes displaying a line, it checks the line's **Progression Method** to decide when to advance:

| Method | Behaviour |
|---|---|
| **UserInput** | Waits for the player to press the advance button. |
| **Timed** | Waits for `TimeBeforeNextLine` seconds, then advances automatically. |
| **AudioComplete** | Waits until `IWitWeaverAudioProvider.IsPlaying` returns `false`, then advances. Use this for voice-acted lines where the audio clip drives the pacing. |

:::note
If a line's mode is `AudioOnly` and its progression is `UserInput`, WitWeaver automatically coerces the progression to `AudioComplete` (or `Timed` if no provider is available). This prevents the conversation from stalling on a line with no visible UI element for the player to interact with.
:::

---

### IWitWeaverAudioProvider

All audio backends communicate with WitWeaver through the `IWitWeaverAudioProvider` interface. The built-in Unity provider (`WitWeaverUnityAudioProvider`) implements it out of the box. For FMOD and Wwise, sample provider scripts are included in `Samples~/Audio/` (see the backend-specific pages).

| Member | Description |
|---|---|
| `PlayVoiceLine(line, reference)` | Called when WitWeaver wants to play audio for a line. |
| `StopVoiceLine()` | Called on conversation stop, skip, and line reverse. |
| `PauseVoiceLine()` | Called when `WitWeaver.PauseConversation()` is invoked. |
| `ResumeVoiceLine()` | Called when `WitWeaver.ResumeConversation()` is invoked. |
| `IsPlaying` | Polled by the `AudioComplete` progression loop. |

---

### Locale Resolution

Every entry in the manifest has a **Language** field. When resolving audio for a line, WitWeaver performs a two-pass lookup:

1. **Exact locale match** — finds the entry whose `Language` matches the active language code (case-insensitive).
2. **Language-agnostic fallback** — if no exact match exists, finds the entry whose `Language` is empty. This acts as a "play this clip for any language".

This means you can have separate audio clips per locale for fully localized voice acting, or a single blank-language entry that plays for all locales.

---

## Setting Up Audio (Quick Start)

1. Create a `WitWeaverAudioManifest` asset.
2. Set **Backend** to the engine you are using.
3. Set **Mode** to `ConversationDriven`, assign your **Source Conversation**, and click **Sync Rows From Conversation**.
4. Fill in the audio for each row.
5. Assign the manifest to the **Audio Manifest** field on your `WitWeaverConversationData`.
6. Set **Default Presentation Mode** on the conversation to `AudioAndText` (or `AudioOnly`).
7. Press Play.

For a full backend-specific walkthrough, see the pages in this section:

- [Unity Audio Setup](unity-audio-setup)
- [FMOD Integration](fmod-integration)
- [Wwise Integration](wwise-integration)
- [Custom Audio Provider](custom-audio-provider)
