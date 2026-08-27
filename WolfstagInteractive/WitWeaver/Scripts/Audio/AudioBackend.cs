namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Selects the audio playback backend for a <see cref="ConvoCoreAudioManifest"/>.
    /// Controls which fields are shown in the manifest inspector and how the runner
    /// resolves and plays audio at runtime.
    /// </summary>
    public enum AudioBackend
    {
        /// <summary>
        /// Plays audio via Unity's built-in AudioSource.
        /// Assign <see cref="UnityEngine.AudioClip"/> directly in each manifest entry slot.
        /// No extra components are required — the runner auto-provisions an AudioSource
        /// on its GameObject when none is manually assigned.
        /// </summary>
        UnityAudioSource,

        /// <summary>
        /// Audio is driven by FMOD Studio.
        /// The runner triggers events using <see cref="ConvoCoreConversationData.DialogueLineInfo.LineID"/>
        /// as the event key. Assign an FMOD adapter that implements
        /// <see cref="IConvoAudioProvider"/> to the ConvoCore runner.
        /// AudioClip slots are not shown in the manifest inspector.
        /// </summary>
        FMOD,

        /// <summary>
        /// Audio is driven by Wwise.
        /// The runner posts events using <see cref="ConvoCoreConversationData.DialogueLineInfo.LineID"/>
        /// as the event key. Assign a Wwise adapter that implements
        /// <see cref="IConvoAudioProvider"/> to the ConvoCore runner.
        /// AudioClip slots are not shown in the manifest inspector.
        /// </summary>
        Wwise,

        /// <summary>
        /// A custom <see cref="IConvoAudioProvider"/> drives playback.
        /// Assign the provider component to the ConvoCore runner.
        /// Both AudioClip and ConvoAudioReference slots are shown in the manifest inspector.
        /// </summary>
        Custom
    }
}
