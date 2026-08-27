namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Controls whether a dialogue line displays text, plays audio, or both.
    /// Set per-line via <see cref="ConvoCoreConversationData.DialogueLineInfo.PresentationMode"/>
    /// or as a conversation-level default via <see cref="ConvoCoreConversationData.DefaultPresentationMode"/>.
    /// </summary>
    public enum ConversationPresentationMode
    {
        /// <summary>
        /// Text is displayed in the UI. Audio plays if a manifest and provider are assigned.
        /// </summary>
        AudioAndText,

        /// <summary>
        /// No text is sent to the UI. Progression is driven by audio length or explicit input.
        /// </summary>
        AudioOnly,

        /// <summary>
        /// Text is displayed. Audio is not played even if a manifest and provider are present.
        /// </summary>
        TextOnly
    }
}
