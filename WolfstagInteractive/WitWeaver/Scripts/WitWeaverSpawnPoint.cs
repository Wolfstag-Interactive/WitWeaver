using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Lightweight scene marker used by <see cref="WorldPointBehaviour"/> to define named spawn positions.
    ///
    /// Place this on any GameObject in the scene, give it a unique <see cref="SpawnPointId"/>, and
    /// reference that ID in a <see cref="WorldPointBehaviour"/> entry instead of authoring raw
    /// Vector3/Quaternion values.
    ///
    /// The component self-registers with <see cref="ConvoCoreSpawnPointRegistry"/> on enable.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api")]
    public class ConvoCoreSpawnPoint : MonoBehaviour
    {
        [Tooltip("Unique identifier for this spawn point. Referenced by WorldPointBehaviour entries.")]
        [SerializeField] public string SpawnPointId;

        [Tooltip("Optional label shown in scene-view gizmos.")]
        [SerializeField] private string _displayLabel;

        public string DisplayLabel => string.IsNullOrEmpty(_displayLabel) ? SpawnPointId : _displayLabel;

        private void OnEnable()
        {
            ConvoCoreSpawnPointRegistry.Instance?.Register(SpawnPointId, this);
        }

        private void OnDisable()
        {
            ConvoCoreSpawnPointRegistry.Instance?.Unregister(SpawnPointId);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.7f);
            Gizmos.DrawSphere(transform.position, 0.1f);
            Gizmos.DrawRay(transform.position, transform.forward * 0.4f);

            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.25f,
                $"[{DisplayLabel}]",
                new GUIStyle(UnityEditor.EditorStyles.boldLabel) { normal = { textColor = new Color(0.3f, 0.8f, 1f) } });
        }
#endif
    }
}
