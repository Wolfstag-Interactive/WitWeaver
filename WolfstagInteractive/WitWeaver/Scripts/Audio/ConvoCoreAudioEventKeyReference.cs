using UnityEngine;
namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Passes a middleware event key (FMOD event path, Wwise event name, etc.)
    /// through the <see cref="IConvoAudioProvider"/> interface without introducing
    /// SDK compile dependencies in the ConvoCore core assembly.
    /// Created at runtime by <see cref="ConvoCore"/> from
    /// <see cref="ConvoCoreAudioManifest.AudioEntry.EventKey"/> — do not create as a persistent asset.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreAudioEventKeyReference.html")]
    public class ConvoCoreAudioEventKeyReference : ConvoAudioReference
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
