using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Character behaviour type for characters that are fully managed by the developer.
    /// WitWeaver never spawns, parents, or destroys anything.
    ///
    /// The character is resolved via <see cref="WitWeaverSceneCharacterRegistry"/> using
    /// <see cref="CharacterBehaviourContext.CharacterId"/>. If no registrant is found for that ID
    /// (or the ID is empty) this behaviour returns null.
    ///
    /// <see cref="OnConversationEnd"/> is a no-op. The developer is fully responsible for
    /// character lifecycle.
    ///
    /// Use case: characters already placed in the world by the developer, with no WitWeaver lifecycle involvement.
    /// </summary>
    [CreateAssetMenu(fileName = "ExternalBehaviour", menuName = "WitWeaver/Character Behaviour/External Behaviour")]
    public class ExternalBehaviour : WitWeaverCharacterBehaviour
    {
        public override IWitWeaverCharacterDisplay ResolvePresence(
            PrefabCharacterRepresentationData representation,
            CharacterBehaviourContext context,
            WitWeaverPrefabRepresentationSpawner spawner)
        {
            if (!spawner.TryGetSceneResident(context.CharacterId, out var display))
            {
                Debug.LogWarning($"[ExternalBehaviour] Scene-resident character '{context.CharacterId}' " +
                                 $"not found in registry. Is a WitWeaverSceneCharacterRegistrant present in the scene?");
                return null;
            }
            return display;
        }
    }
}
