#if CONVOCORE_WWISE
// ─────────────────────────────────────────────────────────────────────────────
// ConvoCoreAudioProviderWwise — Wwise integration for ConvoCore
//
// REQUIREMENTS: Audiokinetic Wwise Unity Integration package must be installed.
//   https://www.audiokinetic.com/en/library/edge/?source=Unity
//
// SETUP:
//   1. Add this component to the same GameObject as your ConvoCore runner.
//   2. Set the AudioManifest's Backend field to AudioBackend.Wwise.
//   3. In the manifest inspector, enter the Wwise event name per line
//      (e.g. "VO_CharA_Intro_01").
//   4. Ensure the SoundBank containing your events is loaded before playback.
//      Add an AkBank component to your scene for each required bank.
//   5. Add CONVOCORE_WWISE to Project Settings > Player > Scripting Define Symbols.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using WolfstagInteractive.ConvoCore;

/// <summary>
/// Wwise audio provider for ConvoCore. Uses <c>AkSoundEngine.PostEvent</c> with an
/// <c>AK_EndOfEvent</c> callback so that <see cref="IConvoAudioProvider.IsPlaying"/>
/// works correctly with the <c>AudioComplete</c> dialogue progression mode.
/// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreAudioProviderWwise.html")]
[AddComponentMenu("ConvoCore/Audio/ConvoCoreAudioProviderWwise")]
public class ConvoCoreAudioProviderWwise : MonoBehaviour, IConvoAudioProvider
{
    private uint _playingID = AkSoundEngine.AK_INVALID_PLAYING_ID;
    private bool _isPlaying;

    /// <summary>
    /// True from the moment an event is posted until the <c>AK_EndOfEvent</c>
    /// callback fires (or <see cref="StopVoiceLine"/> is called).
    /// </summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>
    /// Posts the Wwise event specified by the <see cref="ConvoCoreAudioEventKeyReference.EventKey"/>
    /// (event name, e.g. <c>"VO_CharA_Intro_01"</c>) and registers an end-of-event callback
    /// so that IsPlaying transitions to false when playback completes naturally.
    /// </summary>
    public void PlayVoiceLine(ConvoCoreConversationData.DialogueLineInfo line, ConvoAudioReference reference)
    {
        StopVoiceLine();

        if (reference is not ConvoCoreAudioEventKeyReference keyRef || string.IsNullOrEmpty(keyRef.EventKey))
        {
            Debug.LogWarning("[ConvoCoreAudioProviderWwise] No event key found on reference. Check that EventKey is filled in the Audio Manifest.");
            return;
        }

        _isPlaying = true;
        _playingID = AkSoundEngine.PostEvent(
            keyRef.EventKey,
            gameObject,
            (uint)AkCallbackType.AK_EndOfEvent,
            OnEventEnd,
            null);

        if (_playingID == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogWarning($"[ConvoCoreAudioProviderWwise] PostEvent failed for '{keyRef.EventKey}'. Ensure the SoundBank is loaded and the event name is correct.");
            _isPlaying = false;
        }
    }

    private void OnEventEnd(object cookie, AkCallbackType type, AkCallbackInfo info)
    {
        _isPlaying = false;
        _playingID = AkSoundEngine.AK_INVALID_PLAYING_ID;
    }

    /// <summary>Stops the current event immediately.</summary>
    public void StopVoiceLine()
    {
        if (_playingID != AkSoundEngine.AK_INVALID_PLAYING_ID)
            AkSoundEngine.StopPlayingID(_playingID, 0);
        _isPlaying = false;
        _playingID = AkSoundEngine.AK_INVALID_PLAYING_ID;
    }

    /// <summary>Pauses the current event.</summary>
    public void PauseVoiceLine()
    {
        if (_playingID != AkSoundEngine.AK_INVALID_PLAYING_ID)
            AkSoundEngine.ExecuteActionOnPlayingID(
                AkActionOnEventType.AkActionOnEventType_Pause, _playingID, 0);
    }

    /// <summary>Resumes a paused event.</summary>
    public void ResumeVoiceLine()
    {
        if (_playingID != AkSoundEngine.AK_INVALID_PLAYING_ID)
            AkSoundEngine.ExecuteActionOnPlayingID(
                AkActionOnEventType.AkActionOnEventType_Resume, _playingID, 0);
    }
}
#endif
