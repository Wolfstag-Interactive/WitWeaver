using System.Collections.Generic;
using UnityEngine;
using Image = UnityEngine.UI.Image;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// uGUI reference implementation of <see cref="IAnimatedPortraitSurface"/>: hosts
    /// animated expression playback on an existing portrait/slot Image. Added lazily by
    /// the UI foundation via <see cref="GetOrAdd"/>. Other UI solutions implement
    /// <see cref="IAnimatedPortraitSurface"/> and tick playback their own way.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverAnimatedPortraitPlayer.html")]
[RequireComponent(typeof(Image))]
    public sealed class WitWeaverAnimatedPortraitPlayer : MonoBehaviour, IAnimatedPortraitSurface
    {
        private Image _image;
        private IAnimatedExpressionPlayback _active;
        private bool _useUnscaledTime;
        // Animator prefab instances are cached per prefab and reused; they never leave
        // their parent Image, so the scene-wide WitWeaverPrefabPool is not needed here.
        private readonly Dictionary<GameObject, GameObject> _prefabInstances = new();

        public bool IsPlaying => _active != null;

        private Image Image => _image != null ? _image : _image = GetComponent<Image>();

        /// <summary>Starts playback of the given payload, stopping any previous playback first.</summary>
        public void Play(AnimatedExpressionPayload payload, bool useUnscaledTime)
        {
            Stop();

            if (payload == null || !payload.IsConfigured)
                return;

            _useUnscaledTime = useUnscaledTime;
            _active = payload.CreatePlayback(new AnimatedPlaybackContext
            {
                Surface = this,
                UseUnscaledTime = useUnscaledTime,
                InstantiateCached = GetOrCreateInstance
            });
            _active.Start();
        }

        /// <summary>Stops playback and restores the host Image for static use.</summary>
        public void Stop()
        {
            _active?.Dispose();
            _active = null;
        }

        private void Update()
        {
            _active?.Tick(_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        private void OnDestroy() => Stop();

        // ------------------------------------------------------------------
        // IAnimatedPortraitSurface
        // ------------------------------------------------------------------

        public void SetFrame(Sprite frame)
        {
            Image.sprite = frame;
            Image.enabled = true;
        }

        public Transform GetPrefabHost() => Image.rectTransform;

        public void SetNativeVisualVisible(bool visible) => Image.enabled = visible;

        // ------------------------------------------------------------------
        // Prefab instance cache
        // ------------------------------------------------------------------

        private GameObject GetOrCreateInstance(GameObject prefab)
        {
            if (prefab == null) return null;

            if (_prefabInstances.TryGetValue(prefab, out var cached) && cached != null)
                return cached;

            var instance = Instantiate(prefab, Image.rectTransform, worldPositionStays: false);
            if (instance.transform is RectTransform rt)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            _prefabInstances[prefab] = instance;
            return instance;
        }

        // ------------------------------------------------------------------
        // Static helpers for UI foundations
        // ------------------------------------------------------------------

        public static WitWeaverAnimatedPortraitPlayer GetOrAdd(Image image)
        {
            if (image == null) return null;
            var player = image.GetComponent<WitWeaverAnimatedPortraitPlayer>();
            return player != null ? player : image.gameObject.AddComponent<WitWeaverAnimatedPortraitPlayer>();
        }

        /// <summary>
        /// Stops playback on the given GameObject if a player is present. Called from
        /// hide paths: SetActive(false) halts Update, but an explicit Stop is still
        /// needed to restore Image.enabled and deactivate hosted animator children.
        /// </summary>
        public static void StopOn(GameObject go)
        {
            if (go == null) return;
            var player = go.GetComponent<WitWeaverAnimatedPortraitPlayer>();
            if (player != null) player.Stop();
        }
    }
}
