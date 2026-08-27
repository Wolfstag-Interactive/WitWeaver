namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Abstract base for audio references used in ConvoCore's audio manifest.
    /// Extend this to create middleware-specific reference types.
    /// ConvoCore ships one concrete implementation: <see cref="ConvoCoreUnityAudioReference"/>.
    /// Third-party packages (FMOD, Wwise) should ship their own subclasses.
    /// </summary>
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/")]
    public abstract class ConvoAudioReference : UnityEngine.ScriptableObject { }
}
