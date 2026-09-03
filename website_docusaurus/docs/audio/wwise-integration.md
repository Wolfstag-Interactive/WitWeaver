---
sidebar_position: 4
title: Wwise Integration
---

# Wwise Integration

WitWeaver™ can drive voice dialogue through Audiokinetic Wwise by mapping each dialogue line to a Wwise event name. WitWeaver passes the event name to a provider component; the provider calls `AkSoundEngine.PostEvent`. This keeps the WitWeaver core assembly free of Wwise compile dependencies.

---

## Prerequisites

- **Audiokinetic Wwise Unity Integration** package installed in your project.
  Download from the [Audiokinetic Launcher](https://www.audiokinetic.com/en/download).
- Your dialogue audio exposed as Wwise events with names such as `VO_CharA_Intro_01`.
- The SoundBank(s) containing your VO events generated in Wwise and added to your Unity project.
- The **WitWeaverAudioProviderWwise** sample script imported from the WitWeaver samples folder (see step 1 below).

---

## Step-by-Step Setup

### 1. Import the Wwise Sample Provider

WitWeaver ships a ready-made Wwise provider in the package samples. Import it into your project:

1. In Unity open **Window → Package Manager**.
2. Find **WitWeaver** in the list.
3. Under **Samples**, import **Audio / Wwise Integration**.

This adds `WitWeaverAudioProviderWwise.cs` to your project. The file will not compile unless the Wwise Unity Integration is also installed.

---

### 2. Add the Provider to Your Runner

Select the GameObject that has your **WitWeaver** runner component:

1. Click **Add Component**.
2. Search for and add **WitWeaverAudioProviderWwise**.
3. In the **WitWeaver** inspector, drag the `WitWeaverAudioProviderWwise` component into the **Audio Provider Object** field.

---

### 3. Ensure Your SoundBank Is Loaded

Wwise requires that any bank containing your events be loaded before you post events. The most common way to do this in Unity is the **AkBank** component:

1. Add an **AkBank** component to a persistent GameObject in your scene (e.g. your AudioManager or GameManager).
2. Set the **Bank Name** field to the name of the bank that contains your VO events.
3. Leave **Load on Enable** checked.

:::warning
Posting a Wwise event whose bank is not loaded returns `AK_INVALID_PLAYING_ID`. The provider will log a warning and mark `IsPlaying` as `false`, which causes `AudioComplete` lines to advance immediately without playing any audio. Always verify the bank is loaded before starting the conversation.
:::

---

### 4. Create and Configure the Audio Manifest

Right-click in the Project panel:

**Create → WitWeaver → Audio Manifest**

In the manifest inspector:

1. Set **Audio Backend** to `Wwise`.
2. Set **Mode** to `Conversation Driven`.
3. Drag your `WitWeaverConversationData` into the **Source Conversation** field.
4. Click **Sync Rows From Conversation**.

Each row now shows a **language tag** and an **Event Name** text field. The `AudioClip` slot is hidden — it is not used by the Wwise backend.

---

### 5. Enter Wwise Event Names

For each row, type the Wwise event name that corresponds to that line and locale:

```
VO_CharA_TownSquare_Greeting_EN
VO_CharA_TownSquare_Greeting_FR
```

These names must exactly match events that exist in your Wwise project and are present in a loaded SoundBank.

:::tip
If you want one event for all languages, enter its name on the `(any)` row and remove the locale-specific rows. WitWeaver will use the `(any)` row as a fallback when no exact language match is found.
:::

---

### 6. Assign the Manifest to the Conversation

Open your `WitWeaverConversationData` asset and drag the manifest into the **Audio Manifest** field.

---

### 7. Press Play

When the conversation reaches a voice line:

1. WitWeaver calls `ResolveEventKey` on the manifest to find the event name for the current line and language.
2. It wraps the name in a `WitWeaverAudioEventKeyReference` and passes it to `WitWeaverAudioProviderWwise.PlayVoiceLine`.
3. The provider calls `AkSoundEngine.PostEvent(eventName, gameObject, AK_EndOfEvent, callback)`.
4. When the event finishes, Wwise fires the `AK_EndOfEvent` callback, which sets `IsPlaying = false`.
5. If the line's progression is `AudioComplete`, WitWeaver detects `IsPlaying == false` and advances.

---

## How the Wwise Provider Works

The sample provider uses `PostEvent` with an end-of-event callback to accurately track playback state:

```csharp
_playingID = AkSoundEngine.PostEvent(
    keyRef.EventKey,
    gameObject,
    (uint)AkCallbackType.AK_EndOfEvent,
    OnEventEnd,
    null);

private void OnEventEnd(object cookie, AkCallbackType type, AkCallbackInfo info)
{
    _isPlaying = false;
    _playingID = AkSoundEngine.AK_INVALID_PLAYING_ID;
}
```

This approach means `IsPlaying` transitions to `false` precisely when Wwise considers the event finished — regardless of clip length. The playing ID is also stored so that `StopVoiceLine`, `PauseVoiceLine`, and `ResumeVoiceLine` can target the specific playing instance.

:::note
The `AK_EndOfEvent` callback is fired on the Wwise audio thread in some configurations. Setting a `bool` field from that thread is safe in practice for this use case, but if you encounter issues in a specific platform or Wwise version, add a `volatile` modifier to `_isPlaying` or dispatch the state change back to the main thread via a queue.
:::

---

## Inspector Reference

### Audio Manifest (Wwise mode)

| Field | Description |
|---|---|
| **Audio Backend** | Must be `Wwise`. |
| **Mode** | `Conversation Driven` or `Standalone`. |
| **Source Conversation** | The conversation to sync rows from. |

### Entry Row (Wwise mode)

| Column | Description |
|---|---|
| **Language** | The locale this event applies to. `(any)` matches all locales. |
| **Event Name** | The Wwise event name, e.g. `VO_CharA_Intro_01`. |

---

## Troubleshooting

**No audio plays and `AK_INVALID_PLAYING_ID` is returned.**

- The SoundBank containing the event is likely not loaded. Add an `AkBank` component and confirm it has loaded successfully before the conversation starts.
- Verify the event name exactly matches what is in your Wwise project (names are case-sensitive).

**Conversation stalls on `AudioComplete` lines.**

- Verify that `WitWeaverAudioProviderWwise` is assigned to **Audio Provider Object** on the WitWeaver runner.
- Confirm the `AK_EndOfEvent` callback is firing. If the bank loaded but the event name is wrong, `PostEvent` returns a playing ID but `AK_EndOfEvent` may never fire (or fires immediately). Log the event name in `PlayVoiceLine` to verify it matches your expectation.

**Audio plays but pause/resume does not work.**

- Verify the playing ID is still valid when `PauseVoiceLine` is called. The ID becomes `AK_INVALID_PLAYING_ID` after `StopVoiceLine` or after `AK_EndOfEvent` fires. If the conversation resumes after a completion event, the ID is no longer valid — this is expected.

**Works in editor but not in a build.**

- Ensure generated SoundBanks are included in the build output path. Check your Wwise Unity Integration settings under *Edit → Wwise Settings → Sound Bank* and confirm the streaming asset path is correct for your target platform.
