using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Built-in Unity AudioClip-based audio reference.
    /// Used with <see cref="ConvoCoreUnityAudioProvider"/>.
    /// For FMOD or Wwise, use the corresponding package's reference type instead.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreUnityAudioReference.html")]
[CreateAssetMenu(menuName = "ConvoCore/Audio/Unity Audio Reference")]
    public class ConvoCoreUnityAudioReference : ConvoAudioReference
    {
        [Tooltip("The AudioClip to play when this reference is resolved.")]
        public AudioClip Clip;
    }
}
