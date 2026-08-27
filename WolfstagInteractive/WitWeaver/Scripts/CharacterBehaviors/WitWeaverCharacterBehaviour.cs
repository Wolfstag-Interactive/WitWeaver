using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Context passed to <see cref="ConvoCoreCharacterBehaviour.ResolvePresence"/> each time
    /// a character needs to be resolved for a dialogue line. Contains no slot concepts;
    /// character placement is entirely the behaviour's responsibility.
    /// </summary>
    public struct CharacterBehaviourContext
    {
        /// <summary>Zero-based index of this character in the current line's representation list.</summary>
        public int CharacterIndex;

        /// <summary>Total number of characters on the current line.</summary>
        public int TotalCharacters;

        /// <summary>Per-line display overrides (scale, flip, SlotId). May be null.</summary>
        public DialogueLineDisplayOptions DisplayOptions;

        /// <summary>
        /// The CharacterID from the conversation participant profile. Used for scene-registry lookup
        /// so behaviours can resolve scene-resident characters without depending on a removed field.
        /// Empty when not populated by the UI (e.g. legacy 2D UI).
        /// </summary>
        public string CharacterId;

        /// <summary>
        /// The configuration entry name to use when spawning from a prefab.
        /// Null or empty selects the default entry on the representation asset.
        /// </summary>
        public string ConfigurationEntryName;
    }

    /// <summary>
    /// Abstract ScriptableObject base for all character behaviour types.
    ///
    /// A character behaviour determines how characters are placed and managed in 3D world-space
    /// conversations. It is responsible for resolving a live <see cref="IConvoCoreCharacterDisplay"/>
    /// for each character that appears on a line, and for cleaning up at conversation end.
    ///
    /// The spawner is passed into <see cref="ResolvePresence"/> at call time. The behaviour
    /// is a ScriptableObject and must not hold serialized references to scene objects.
    /// Runtime-only scene references may be cached in <c>[System.NonSerialized]</c> fields.
    /// </summary>
    public abstract class ConvoCoreCharacterBehaviour : ScriptableObject
    {
        /// <summary>
        /// Resolve the live <see cref="IConvoCoreCharacterDisplay"/> for the given representation.
        /// Called once per character per dialogue line by <see cref="ConvoCoreSampleUI3D"/>.
        ///
        /// Implementations that cache displays across lines (e.g. <see cref="WorldPointBehaviour"/>)
        /// should return the cached instance on subsequent calls for the same character rather than
        /// requesting a new spawn from the spawner.
        ///
        /// Return null if the character cannot or should not be resolved (e.g. ExternalBehaviour for
        /// a spawn-from-prefab character). The 3D UI will skip expression application when null is returned.
        /// </summary>
        public abstract IConvoCoreCharacterDisplay ResolvePresence(
            PrefabCharacterRepresentationData representation,
            CharacterBehaviourContext context,
            ConvoCorePrefabRepresentationSpawner spawner);

        /// <summary>Called by <see cref="ConvoCoreSampleUI3D"/> when a conversation begins.</summary>
        public virtual void OnConversationBegin() { }

        /// <summary>
        /// Called by <see cref="ConvoCoreSampleUI3D"/> when a conversation ends.
        /// Implementations must release any spawned instances and clean up scene state here.
        /// The spawner is NOT called by the 3D UI after this point.
        /// </summary>
        public virtual void OnConversationEnd() { }
    }
}
