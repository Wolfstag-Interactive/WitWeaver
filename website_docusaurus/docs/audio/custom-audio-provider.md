---
sidebar_position: 5
title: Custom Audio Provider
---

# Custom Audio Provider

If you are using an audio engine other than Unity AudioSource, FMOD, or Wwise — or if you need behaviour that the built-in providers do not cover — you can implement `IWitWeaverAudioProvider` yourself.

---

## The Interface

```csharp
public interface IWitWeaverAudioProvider
{
    void PlayVoiceLine(WitWeaverConversationData.DialogueLineInfo line, WitWeaverAudioReference reference);
    void StopVoiceLine();
    void PauseVoiceLine();
    void ResumeVoiceLine();
    bool IsPlaying { get; }
}
```

Implement this interface on a `MonoBehaviour` and attach it to the same GameObject as your WitWeaver runner.

---

## What Each Member Does

### `PlayVoiceLine(line, reference)`

Called by WitWeaver when it is ready to play the audio for a dialogue line. The `line` argument is the full `DialogueLineInfo`, giving you access to `LineID`, `characterID`, expression data, and anything else from the dialogue data. The `reference` argument is the resolved audio reference from the manifest.

Depending on the backend you select on the manifest, `reference` will be one of:

| Backend | Reference type | What to cast to |
|---|---|---|
| `UnityAudioSource` | `WitWeaverUnityAudioReference` | Cast and read `.Clip` |
| `FMOD` | `WitWeaverAudioEventKeyReference` | Cast and read `.EventKey` (FMOD event path) |
| `Wwise` | `WitWeaverAudioEventKeyReference` | Cast and read `.EventKey` (Wwise event name) |
| `Custom` | Whatever you assigned to the manifest entry's `Clip`, `Key`, or `Reference` slot | Cast appropriately |

For the `Custom` backend, if you assigned a `WitWeaverAudioReference` ScriptableObject subclass to the entry's **Ref** slot, that subclass instance is passed as `reference`. If you only filled in the **Key** text field, a `WitWeaverAudioEventKeyReference` is passed.

### `StopVoiceLine()`

Called when:
- The conversation is stopped via `StopConversation()`.
- The player skips a line while audio is playing.
- The player reverses to a previous line.

Immediately stop any currently playing audio.

### `PauseVoiceLine()` / `ResumeVoiceLine()`

Called when `PauseConversation()` and `ResumeConversation()` are invoked on the runner. Pause and resume playback of the current voice line.

### `IsPlaying`

Return `true` while audio is actively playing, `false` otherwise. WitWeaver polls this property once per frame during `AudioComplete` progression. When it transitions to `false`, WitWeaver advances to the next line.

:::tip
If your audio engine fires a callback when playback ends (like Wwise's `AK_EndOfEvent`), use the callback to set a backing `bool` field and return that field from `IsPlaying`. This is more reliable than polling an engine-side state that may have race conditions.
:::

---

## Minimal Example

This example plays a `AudioClip` via a custom `AudioSource` configuration but also logs every line to a transcript system:

```csharp
using UnityEngine;
using WolfstagInteractive.WitWeaver;

[AddComponentMenu("WitWeaver/Audio/MyCustomAudioProvider")]
public class MyCustomAudioProvider : MonoBehaviour, IWitWeaverAudioProvider
{
    [SerializeField] private AudioSource _voiceSource;

    public bool IsPlaying => _voiceSource != null && _voiceSource.isPlaying;

    public void PlayVoiceLine(WitWeaverConversationData.DialogueLineInfo line, WitWeaverAudioReference reference)
    {
        if (_voiceSource == null) return;

        // Handle a Unity clip reference
        if (reference is WitWeaverUnityAudioReference unityRef && unityRef.Clip != null)
        {
            _voiceSource.Stop();
            _voiceSource.clip = unityRef.Clip;
            _voiceSource.Play();
        }

        // Log to a custom transcript system
        MyTranscriptSystem.Record(line.LineID, line.characterID);
    }

    public void StopVoiceLine()   => _voiceSource?.Stop();
    public void PauseVoiceLine()  => _voiceSource?.Pause();
    public void ResumeVoiceLine() => _voiceSource?.UnPause();
}
```

---

## Using a Custom `WitWeaverAudioReference`

If you need to store additional metadata per line (e.g. a subtitle delay offset, a subtitle speaker colour, or a middleware reference type that WitWeaver does not know about), subclass `WitWeaverAudioReference`:

```csharp
using UnityEngine;
using WolfstagInteractive.WitWeaver;

[CreateAssetMenu(menuName = "WitWeaver/Audio/My Audio Reference")]
public class MyAudioReference : WitWeaverAudioReference
{
    public AudioClip Clip;
    public float SubtitleDelaySeconds;
    public Color SpeakerColour = Color.white;
}
```

Assign instances of this ScriptableObject to the **Ref** slot of manifest entries when using the `Custom` backend. In your provider, cast `reference` to `MyAudioReference` to access the extra fields.

---

## Injecting a Provider at Runtime

If your provider is instantiated through code rather than added in the inspector, use `SetAudioProvider`:

```csharp
var provider = GetComponent<MyCustomAudioProvider>();
_witWeaverRunner.SetAudioProvider(provider);
```

This overrides any provider assigned via the inspector **Audio Provider Object** field.

---

## The `Custom` Backend

Set the manifest's **Audio Backend** to `Custom` to show all three assignment slots in the inspector for each entry row:

| Slot | Purpose |
|---|---|
| **Clip** | An `AudioClip`. Passed as a `WitWeaverUnityAudioReference` if no `Reference` is assigned. |
| **Key** | An arbitrary string. Passed as a `WitWeaverAudioEventKeyReference`. |
| **Ref** | A `WitWeaverAudioReference` subclass ScriptableObject. Passed directly if assigned; takes priority over Clip and Key. |

Your provider decides which of these to use. You can use all three, ignore some, or combine them.

---

## Checklist

Before going live with a custom provider, verify these behaviours:

- [ ] `IsPlaying` returns `false` immediately if `PlayVoiceLine` is called with a null or invalid reference (not stuck at `true`).
- [ ] `StopVoiceLine` does not throw if called when nothing is playing.
- [ ] `PauseVoiceLine` followed by `ResumeVoiceLine` resumes from the same position (not from the beginning).
- [ ] `IsPlaying` transitions to `false` within one frame of playback ending, so `AudioComplete` lines do not stall.
- [ ] The provider cleans up any temporary objects or unmanaged resources it creates during `PlayVoiceLine`.
