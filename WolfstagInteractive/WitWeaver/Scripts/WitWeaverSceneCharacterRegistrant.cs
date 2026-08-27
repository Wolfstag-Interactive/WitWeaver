using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Add this component to any scene GameObject that should be driven by WitWeaver
    /// as a scene-resident character (i.e. not spawned from a pool).
    ///
    /// The component resolves an <see cref="IWitWeaverCharacterDisplay"/> from this
    /// GameObject or its children and registers it with a <see cref="WitWeaverSceneCharacterRegistry"/>
    /// on enable, unregistering on disable.
    ///
    /// If no registry is explicitly assigned the component falls back to
    /// <see cref="WitWeaverSceneCharacterRegistry.Instance"/> (the first registry that awoke in the scene).
    ///
    /// The <see cref="characterId"/> must match the CharacterID on the
    /// <see cref="WitWeaverCharacterProfileBaseData"/> used in the conversation.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api")]
    public class WitWeaverSceneCharacterRegistrant : MonoBehaviour
    {
        [Tooltip("Must match the CharacterID on the character's WitWeaverCharacterProfileBaseData asset.")]
        [SerializeField] private string characterId;

        [Tooltip("The registry to register with. When left empty the static WitWeaverSceneCharacterRegistry.Instance is used automatically.")]
        [SerializeField] private WitWeaverSceneCharacterRegistry registry;

        private IWitWeaverCharacterDisplay _display;

        private void Awake()
        {
            _display = GetComponentInChildren<IWitWeaverCharacterDisplay>(includeInactive: true);

            if (_display == null)
                Debug.LogWarning($"[WitWeaverSceneCharacterRegistrant] No IWitWeaverCharacterDisplay found on '{gameObject.name}' or its children. This character will not be available to WitWeaver.");
        }

        private void OnEnable()
        {
            var target = registry ?? WitWeaverSceneCharacterRegistry.Instance;
            if (target == null)
            {
                Debug.LogWarning($"[WitWeaverSceneCharacterRegistrant] No registry found for '{gameObject.name}'. " +
                                 $"Assign a WitWeaverSceneCharacterRegistry or ensure one exists in the scene.");
                return;
            }

            if (_display == null) return;

            target.Register(characterId, _display);
        }

        private void OnDisable()
        {
            var target = registry ?? WitWeaverSceneCharacterRegistry.Instance;
            target?.Unregister(characterId);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(characterId))
                Debug.LogWarning($"[WitWeaverSceneCharacterRegistrant] Character ID is empty on '{gameObject.name}'. Assign a CharacterID that matches the character profile.");
        }
#endif
    }
}