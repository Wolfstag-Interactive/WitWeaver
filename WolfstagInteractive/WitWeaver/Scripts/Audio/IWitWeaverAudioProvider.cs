namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Interface for audio playback backends. Implement to support FMOD, Wwise,
    /// or any custom audio middleware. The built-in implementation is
    /// <see cref="ConvoCoreUnityAudioProvider"/>. Assign via the ConvoCore inspector or
    /// call <see cref="ConvoCore.SetAudioProvider"/> at runtime.
    /// </summary>
    public interface IConvoAudioProvider
    {
        /// <summary>
        /// Play a voice line for the given dialogue line.
        /// The full <see cref="ConvoCoreConversationData.DialogueLineInfo"/> is passed so middleware
        /// providers can use <c>LineID</c>, <c>characterID</c>, or expression data to route to the
        /// correct event or bank. Providers that use Unity AudioClips should cast
        /// <paramref name="reference"/> to <see cref="ConvoCoreUnityAudioReference"/>.
        /// Providers using middleware should ignore <paramref name="reference"/> and use
        /// <c>line.LineID</c> as the event key.
        /// </summary>
        void PlayVoiceLine(ConvoCoreConversationData.DialogueLineInfo line, ConvoAudioReference reference);

        /// <summary>
        /// Stop any currently playing voice clip immediately.
        /// Called on conversation stop, skip, and reverse.
        /// </summary>
        void StopVoiceLine();

        /// <summary>
        /// Pause the current voice clip. Paired with <see cref="ResumeVoiceLine"/>.
        /// Called when <see cref="ConvoCore.PauseConversation"/> is invoked.
        /// </summary>
        void PauseVoiceLine();

        /// <summary>
        /// Resume a paused voice clip.
        /// Called when <see cref="ConvoCore.ResumeConversation"/> is invoked.
        /// </summary>
        void ResumeVoiceLine();

        /// <summary>
        /// True while a voice clip is actively playing.
        /// Used by <see cref="ConvoCoreConversationData.DialogueLineProgressionMethod.AudioComplete"/>
        /// progression to poll for clip completion.
        /// </summary>
        bool IsPlaying { get; }
    }
}
