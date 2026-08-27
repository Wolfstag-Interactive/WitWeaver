using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Built-in Unity AudioClip-based audio reference.
    /// Used with <see cref="WitWeaverUnityAudioProvider"/>.
    /// For FMOD or Wwise, use the corresponding package's reference type instead.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverUnityAudioReference.html")]
[CreateAssetMenu(menuName = "WitWeaver/Audio/Unity Audio Reference")]
    public class WitWeaverUnityAudioReference : WitWeaverAudioReference
    {
        [Tooltip("The AudioClip to play when this reference is resolved.")]
        public AudioClip Clip;
    }
}
