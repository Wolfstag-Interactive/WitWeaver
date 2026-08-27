using UnityEngine;
namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Passes a middleware event key (FMOD event path, Wwise event name, etc.)
    /// through the <see cref="IWitWeaverAudioProvider"/> interface without introducing
    /// SDK compile dependencies in the WitWeaver core assembly.
    /// Created at runtime by <see cref="WitWeaver"/> from
    /// <see cref="WitWeaverAudioManifest.AudioEntry.EventKey"/> — do not create as a persistent asset.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverAudioEventKeyReference.html")]
    public class WitWeaverAudioEventKeyReference : WitWeaverAudioReference
    {
        /// <summary>
        /// The middleware event identifier.
        /// For FMOD: full event path, e.g. <c>"event:/VO/CharacterA/Line001"</c>.
        /// For Wwise: event name, e.g. <c>"VO_CharA_Intro_01"</c>.
        /// For custom backends: any string your provider interprets.
        /// </summary>
        public string EventKey;
    }
}
