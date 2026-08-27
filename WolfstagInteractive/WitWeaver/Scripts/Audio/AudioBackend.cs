namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Selects the audio playback backend for a <see cref="WitWeaverAudioManifest"/>.
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
        /// The runner triggers events using <see cref="WitWeaverConversationData.DialogueLineInfo.LineID"/>
        /// as the event key. Assign an FMOD adapter that implements
        /// <see cref="IWitWeaverAudioProvider"/> to the WitWeaver runner.
        /// AudioClip slots are not shown in the manifest inspector.
        /// </summary>
        FMOD,

        /// <summary>
        /// Audio is driven by Wwise.
        /// The runner posts events using <see cref="WitWeaverConversationData.DialogueLineInfo.LineID"/>
        /// as the event key. Assign a Wwise adapter that implements
        /// <see cref="IWitWeaverAudioProvider"/> to the WitWeaver runner.
        /// AudioClip slots are not shown in the manifest inspector.
        /// </summary>
        Wwise,

        /// <summary>
        /// A custom <see cref="IWitWeaverAudioProvider"/> drives playback.
        /// Assign the provider component to the WitWeaver runner.
        /// Both AudioClip and WitWeaverAudioReference slots are shown in the manifest inspector.
        /// </summary>
        Custom
    }
}
