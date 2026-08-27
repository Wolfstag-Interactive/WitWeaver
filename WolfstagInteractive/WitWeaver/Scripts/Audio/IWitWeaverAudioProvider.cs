namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Interface for audio playback backends. Implement to support FMOD, Wwise,
    /// or any custom audio middleware. The built-in implementation is
    /// <see cref="WitWeaverUnityAudioProvider"/>. Assign via the WitWeaver inspector or
    /// call <see cref="WitWeaver.SetAudioProvider"/> at runtime.
    /// </summary>
    public interface IWitWeaverAudioProvider
    {
        /// <summary>
        /// Play a voice line for the given dialogue line.
        /// The full <see cref="WitWeaverConversationData.DialogueLineInfo"/> is passed so middleware
        /// providers can use <c>LineID</c>, <c>characterID</c>, or expression data to route to the
        /// correct event or bank. Providers that use Unity AudioClips should cast
        /// <paramref name="reference"/> to <see cref="WitWeaverUnityAudioReference"/>.
        /// Providers using middleware should ignore <paramref name="reference"/> and use
        /// <c>line.LineID</c> as the event key.
        /// </summary>
        void PlayVoiceLine(WitWeaverConversationData.DialogueLineInfo line, WitWeaverAudioReference reference);

        /// <summary>
        /// Stop any currently playing voice clip immediately.
        /// Called on conversation stop, skip, and reverse.
        /// </summary>
        void StopVoiceLine();

        /// <summary>
        /// Pause the current voice clip. Paired with <see cref="ResumeVoiceLine"/>.
        /// Called when <see cref="WitWeaver.PauseConversation"/> is invoked.
        /// </summary>
        void PauseVoiceLine();

        /// <summary>
        /// Resume a paused voice clip.
        /// Called when <see cref="WitWeaver.ResumeConversation"/> is invoked.
        /// </summary>
        void ResumeVoiceLine();

        /// <summary>
        /// True while a voice clip is actively playing.
        /// Used by <see cref="WitWeaverConversationData.DialogueLineProgressionMethod.AudioComplete"/>
        /// progression to poll for clip completion.
        /// </summary>
        bool IsPlaying { get; }
    }
}
