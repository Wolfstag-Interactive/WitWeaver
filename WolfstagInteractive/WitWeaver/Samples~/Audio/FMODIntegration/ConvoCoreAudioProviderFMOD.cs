#if CONVOCORE_FMOD
// ─────────────────────────────────────────────────────────────────────────────
// ConvoCoreAudioProviderFMOD — FMOD Studio integration for ConvoCore
//
// REQUIREMENTS: FMOD Studio Unity Integration package must be installed.
//   https://www.fmod.com/docs/2.02/unity/
//
// SETUP:
//   1. Add this component to the same GameObject as your ConvoCore runner.
//   2. Set the AudioManifest's Backend field to AudioBackend.FMOD.
//   3. In the manifest inspector, enter the FMOD event path per line
//      (e.g. "event:/VO/CharacterA/Line001").
//   4. Ensure the FMOD Bank containing your events is loaded before playback
//      (typically via the FMOD Studio Settings window or an FMODUnity.StudioBankLoader).
//   5. Add CONVOCORE_FMOD to Project Settings > Player > Scripting Define Symbols.
// ─────────────────────────────────────────────────────────────────────────────

using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using WolfstagInteractive.ConvoCore;

/// <summary>
/// FMOD Studio audio provider for ConvoCore. Uses <c>RuntimeManager.CreateInstance</c>
/// so that <see cref="IConvoAudioProvider.IsPlaying"/> works correctly with the
/// <c>AudioComplete</c> dialogue progression mode.
/// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreAudioProviderFMOD.html")]
[AddComponentMenu("ConvoCore/Audio/ConvoCoreAudioProviderFMOD")]
public class ConvoCoreAudioProviderFMOD : MonoBehaviour, IConvoAudioProvider
{
    private EventInstance _instance;

    /// <summary>
    /// True while the current FMOD event is in a non-stopped playback state.
    /// Polled by ConvoCore's WaitForAudioComplete coroutine.
    /// </summary>
    public bool IsPlaying
    {
        get
        {
            if (!_instance.isValid()) return false;
            _instance.getPlaybackState(out PLAYBACK_STATE state);
            return state != PLAYBACK_STATE.STOPPED;
        }
    }

    /// <summary>
    /// Plays the FMOD event specified by the <see cref="ConvoCoreAudioEventKeyReference.EventKey"/>
    /// (full event path, e.g. <c>"event:/VO/CharA/Line001"</c>).
    /// </summary>
    public void PlayVoiceLine(ConvoCoreConversationData.DialogueLineInfo line, ConvoAudioReference reference)
    {
        StopVoiceLine();

        if (reference is not ConvoCoreAudioEventKeyReference keyRef || string.IsNullOrEmpty(keyRef.EventKey))
        {
            Debug.LogWarning("[ConvoCoreAudioProviderFMOD] No event key found on reference. Check that EventKey is filled in the Audio Manifest.");
            return;
        }

        _instance = RuntimeManager.CreateInstance(keyRef.EventKey);
        _instance.start();
        // Release ownership immediately — FMOD manages the instance lifetime.
        // IsPlaying will still return the correct state until the event stops.
        _instance.release();
    }

    /// <summary>Stops the current event immediately (no fade-out).</summary>
    public void StopVoiceLine()
    {
        if (_instance.isValid())
            _instance.stop(STOP_MODE.IMMEDIATE);
    }

    /// <summary>Pauses the current event.</summary>
    public void PauseVoiceLine()
    {
        if (_instance.isValid())
            _instance.setPaused(true);
    }

    /// <summary>Resumes a paused event.</summary>
    public void ResumeVoiceLine()
    {
        if (_instance.isValid())
            _instance.setPaused(false);
    }
}
#endif
