---
sidebar_position: 3
title: FMOD Integration
---

# FMOD Integration

WitWeaver can drive voice dialogue through FMOD Studio by mapping each dialogue line to an FMOD event path. WitWeaver passes the event path to a provider component; the provider calls the FMOD API. This keeps the WitWeaver core assembly free of FMOD compile dependencies.

---

## Prerequisites

- **FMOD Studio Unity Integration** package installed in your project.
  Minimum recommended version: FMOD 2.01+.
  Download from [fmod.com/download](https://www.fmod.com/download).
- Your dialogue audio exported from FMOD Studio as events with paths such as `event:/VO/CharacterA/Line001`.
- The **WitWeaverAudioProviderFMOD** sample script imported from the WitWeaver samples folder (see step 1 below).

---

## Step-by-Step Setup

### 1. Import the FMOD Sample Provider

WitWeaver ships a ready-made FMOD provider in the package samples. Import it into your project:

1. In Unity open **Window → Package Manager**.
2. Find **WitWeaver** in the list.
3. Under **Samples**, import **Audio / FMOD Integration**.

This adds `WitWeaverAudioProviderFMOD.cs` to your project. The file will not compile unless the FMOD Unity Integration is also installed.

---

### 2. Add the Provider to Your Runner

Select the GameObject that has your **WitWeaver** runner component:

1. Click **Add Component**.
2. Search for and add **WitWeaverAudioProviderFMOD**.
3. In the **WitWeaver** inspector, drag the `WitWeaverAudioProviderFMOD` component into the **Audio Provider Object** field.

---

### 3. Create and Configure the Audio Manifest

Right-click in the Project panel:

**Create → WitWeaver → Audio Manifest**

In the manifest inspector:

1. Set **Audio Backend** to `FMOD`.
2. Set **Mode** to `Conversation Driven`.
3. Drag your `WitWeaverConversationData` into the **Source Conversation** field.
4. Click **Sync Rows From Conversation**.

Each row now shows a **language tag** and an **Event Path** text field. The `AudioClip` slot is hidden — it is not used by the FMOD backend.

---

### 4. Enter FMOD Event Paths

For each row, type the full FMOD event path that corresponds to that line and locale:

```
event:/VO/CharacterA/TownSquare_Greeting_EN
event:/VO/CharacterA/TownSquare_Greeting_FR
```

These paths must exactly match events that exist in your FMOD Studio project and have been built into a bank that is loaded at runtime.

:::tip
If the same event path is used for all locales (e.g. your FMOD project handles localisation internally via parameters or banks), enter the path on the language-agnostic `(any)` row and delete the locale-specific rows. WitWeaver will use the `(any)` entry as a fallback for any language.
:::

---

### 5. Assign the Manifest to the Conversation

Open your `WitWeaverConversationData` asset and drag the manifest into the **Audio Manifest** field.

---

### 6. Load Your FMOD Banks

FMOD events must be in a loaded bank before WitWeaver tries to play them. You have two common options:

- **FMOD Studio Settings (Master Bank auto-load)**: In *Edit → FMOD Studio → Edit Settings*, enable auto-loading of the banks that contain your VO events.
- **FMODUnity.StudioBankLoader component**: Add this component to a scene object and specify the bank names. It loads them on `Awake`.

:::warning
If an event path is correct but the bank is not loaded, `RuntimeManager.CreateInstance` will silently fail and no audio will play. Always verify that the bank containing your event is loaded before the conversation starts.
:::

---

### 7. Press Play

When the conversation reaches a voice line:

1. WitWeaver calls `ResolveEventKey` on the manifest to find the event path for the current line and language.
2. It wraps the path in a `WitWeaverAudioEventKeyReference` and passes it to `WitWeaverAudioProviderFMOD.PlayVoiceLine`.
3. The provider calls `RuntimeManager.CreateInstance(eventPath)`, then `.start()` and `.release()`.
4. If the line's progression is `AudioComplete`, WitWeaver polls `IsPlaying` (backed by `getPlaybackState`) until the event stops.

---

## How the FMOD Provider Works

The sample provider uses `CreateInstance` rather than `PlayOneShot` so that `IsPlaying` returns accurate state for `AudioComplete` progression:

```csharp
_instance = RuntimeManager.CreateInstance(keyRef.EventKey);
_instance.start();
_instance.release(); // hands ownership to FMOD; instance remains queryable
```

Calling `.release()` immediately after `.start()` is the idiomatic FMOD pattern. It tells FMOD that your code no longer needs to manage the instance's lifetime; FMOD automatically cleans it up when the event stops. The `EventInstance` struct remains valid and `getPlaybackState` continues to work until the event reaches the `STOPPED` state.

`IsPlaying` returns `false` when `getPlaybackState` returns `PLAYBACK_STATE.STOPPED`, which triggers `AudioComplete` advancement.

---

## Inspector Reference

### Audio Manifest (FMOD mode)

| Field | Description |
|---|---|
| **Audio Backend** | Must be `FMOD`. |
| **Mode** | `Conversation Driven` or `Standalone`. |
| **Source Conversation** | The conversation to sync rows from. |

### Entry Row (FMOD mode)

| Column | Description |
|---|---|
| **Language** | The locale this event applies to. `(any)` matches all locales. |
| **Event Path** | The full FMOD event path, e.g. `event:/VO/CharA/Line001`. |

---

## Troubleshooting

**No audio plays and no FMOD errors appear.**

- Verify the bank containing your events is loaded before the conversation starts.
- Verify the event path in the manifest exactly matches the path in FMOD Studio (paths are case-sensitive).
- Check that the `WitWeaverAudioProviderFMOD` component is assigned to **Audio Provider Object** on the WitWeaver runner.

**Conversation stalls on `AudioComplete` lines and never advances.**

- Verify that `WitWeaverAudioProviderFMOD` is attached and the **Audio Provider Object** field is wired up. If the provider is missing, `IsPlaying` is never polled and the `WaitForAudioComplete` loop runs until its 300-second safety cap.

**`AudioComplete` advances too early, cutting off the audio.**

- This can happen if the event is a one-shot and the `getPlaybackState` poll catches it in the brief `STARTING` → `PLAYING` → `STOPPED` transition before the next poll. Ensure the event has audible content and the bank is fully loaded. You may also add a small `STOPPING` state check; see the FMOD `PLAYBACK_STATE` documentation for details.

**Wiring works in editor but not in a build.**

- Make sure your FMOD banks are included in the build. Check *Edit → FMOD Studio → Edit Settings → Build* and verify the bank paths are correct.
