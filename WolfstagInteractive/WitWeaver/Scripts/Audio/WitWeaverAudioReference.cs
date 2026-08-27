namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Abstract base for audio references used in WitWeaver's audio manifest.
    /// Extend this to create middleware-specific reference types.
    /// WitWeaver ships one concrete implementation: <see cref="WitWeaverUnityAudioReference"/>.
    /// Third-party packages (FMOD, Wwise) should ship their own subclasses.
    /// </summary>
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/")]
    public abstract class WitWeaverAudioReference : UnityEngine.ScriptableObject { }
}
