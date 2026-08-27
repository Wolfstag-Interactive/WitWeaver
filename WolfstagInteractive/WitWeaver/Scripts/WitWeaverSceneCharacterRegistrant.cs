using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Add this component to any scene GameObject that should be driven by ConvoCore
    /// as a scene-resident character (i.e. not spawned from a pool).
    ///
    /// The component resolves an <see cref="IConvoCoreCharacterDisplay"/> from this
    /// GameObject or its children and registers it with a <see cref="ConvoCoreSceneCharacterRegistry"/>
    /// on enable, unregistering on disable.
    ///
    /// If no registry is explicitly assigned the component falls back to
    /// <see cref="ConvoCoreSceneCharacterRegistry.Instance"/> (the first registry that awoke in the scene).
    ///
    /// The <see cref="characterId"/> must match the CharacterID on the
    /// <see cref="ConvoCoreCharacterProfileBaseData"/> used in the conversation.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api")]
    public class ConvoCoreSceneCharacterRegistrant : MonoBehaviour
    {
        [Tooltip("Must match the CharacterID on the character's ConvoCoreCharacterProfileBaseData asset.")]
        [SerializeField] private string characterId;

        [Tooltip("The registry to register with. When left empty the static ConvoCoreSceneCharacterRegistry.Instance is used automatically.")]
        [SerializeField] private ConvoCoreSceneCharacterRegistry registry;

        private IConvoCoreCharacterDisplay _display;

        private void Awake()
        {
            _display = GetComponentInChildren<IConvoCoreCharacterDisplay>(includeInactive: true);

            if (_display == null)
                Debug.LogWarning($"[ConvoCoreSceneCharacterRegistrant] No IConvoCoreCharacterDisplay found on '{gameObject.name}' or its children. This character will not be available to ConvoCore.");
        }

        private void OnEnable()
        {
            var target = registry ?? ConvoCoreSceneCharacterRegistry.Instance;
            if (target == null)
            {
                Debug.LogWarning($"[ConvoCoreSceneCharacterRegistrant] No registry found for '{gameObject.name}'. " +
                                 $"Assign a ConvoCoreSceneCharacterRegistry or ensure one exists in the scene.");
                return;
            }

            if (_display == null) return;

            target.Register(characterId, _display);
        }

        private void OnDisable()
        {
            var target = registry ?? ConvoCoreSceneCharacterRegistry.Instance;
            target?.Unregister(characterId);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(characterId))
                Debug.LogWarning($"[ConvoCoreSceneCharacterRegistrant] Character ID is empty on '{gameObject.name}'. Assign a CharacterID that matches the character profile.");
        }
#endif
    }
}