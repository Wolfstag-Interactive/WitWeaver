// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Default Unity AudioSource-based audio provider.
    /// Requires an <see cref="AudioSource"/> on the same GameObject — one is added
    /// automatically if not already present (via <see cref="RequireComponent"/>).
    /// For FMOD or Wwise, replace this with the corresponding middleware adapter.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverUnityAudioProvider.html")]
    [AddComponentMenu("WitWeaver/Audio/WitWeaverUnityAudioProvider")]
    [RequireComponent(typeof(AudioSource))]
    public class WitWeaverUnityAudioProvider : MonoBehaviour, IWitWeaverAudioProvider
    {
        private AudioSource _voiceSource;

        private void Awake()
        {
            _voiceSource = GetComponent<AudioSource>();
            _voiceSource.playOnAwake = false;
        }

        public bool IsPlaying => _voiceSource != null && _voiceSource.isPlaying;

        public void PlayVoiceLine(WitWeaverConversationData.DialogueLineInfo line, WitWeaverAudioReference reference)
        {
            if (_voiceSource == null) return;
            if (reference is not WitWeaverUnityAudioReference unityRef) return;
            if (unityRef.Clip == null) return;

            _voiceSource.Stop();
            _voiceSource.clip = unityRef.Clip;
            _voiceSource.Play();
        }

        public void StopVoiceLine()
        {
            if (_voiceSource != null) _voiceSource.Stop();
        }

        public void PauseVoiceLine()
        {
            if (_voiceSource != null) _voiceSource.Pause();
        }

        public void ResumeVoiceLine()
        {
            if (_voiceSource != null) _voiceSource.UnPause();
        }
    }
}
