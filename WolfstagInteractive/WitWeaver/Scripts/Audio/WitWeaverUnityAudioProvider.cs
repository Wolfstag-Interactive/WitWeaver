using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Default Unity AudioSource-based audio provider.
    /// Requires an <see cref="AudioSource"/> on the same GameObject — one is added
    /// automatically if not already present (via <see cref="RequireComponent"/>).
    /// For FMOD or Wwise, replace this with the corresponding middleware adapter.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreUnityAudioProvider.html")]
    [AddComponentMenu("ConvoCore/Audio/ConvoCoreUnityAudioProvider")]
    [RequireComponent(typeof(AudioSource))]
    public class ConvoCoreUnityAudioProvider : MonoBehaviour, IConvoAudioProvider
    {
        private AudioSource _voiceSource;

        private void Awake()
        {
            _voiceSource = GetComponent<AudioSource>();
            _voiceSource.playOnAwake = false;
        }

        public bool IsPlaying => _voiceSource != null && _voiceSource.isPlaying;

        public void PlayVoiceLine(ConvoCoreConversationData.DialogueLineInfo line, ConvoAudioReference reference)
        {
            if (_voiceSource == null) return;
            if (reference is not ConvoCoreUnityAudioReference unityRef) return;
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
