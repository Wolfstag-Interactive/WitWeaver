using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// UI-agnostic render target for animated portrait playback. Implemented by the
    /// UI layer (e.g. a uGUI Image host); core playback code only ever talks to this
    /// contract so any UI solution (uGUI, UI Toolkit, world-space) can supply its own.
    /// </summary>
    public interface IAnimatedPortraitSurface
    {
        /// <summary>Display a single sprite frame on the surface's native visual.</summary>
        void SetFrame(Sprite frame);

        /// <summary>
        /// Transform that spawned animated prefabs are parented under.
        /// Return null if this surface cannot host GameObjects (e.g. UI Toolkit) —
        /// prefab-based backends will degrade gracefully.
        /// </summary>
        Transform GetPrefabHost();

        /// <summary>
        /// Show or hide the surface's own sprite visual (e.g. while a hosted prefab
        /// provides the visuals instead).
        /// </summary>
        void SetNativeVisualVisible(bool visible);
    }

    /// <summary>
    /// Everything a payload needs to create playback. The driver (UI layer) supplies
    /// the surface, the time source choice, and a cached-instantiation callback so
    /// prefab instance caching stays out of core.
    /// </summary>
    public struct AnimatedPlaybackContext
    {
        public IAnimatedPortraitSurface Surface;
        public bool UseUnscaledTime;
        /// <summary>Returns a (possibly cached/reused) instance of the given prefab.</summary>
        public Func<GameObject, GameObject> InstantiateCached;
    }

    /// <summary>
    /// A live animation instance driving one surface. Created per Play by a payload,
    /// ticked by the driver, disposed when the surface is reused or hidden.
    /// </summary>
    public interface IAnimatedExpressionPlayback : IDisposable
    {
        /// <summary>Apply the first visual state.</summary>
        void Start();

        /// <summary>Advance the animation. Delta time is already scaled/unscaled by the driver.</summary>
        void Tick(float deltaTime);
    }

    /// <summary>
    /// Base class for animation payloads authored on an
    /// <see cref="AnimatedCharacterRepresentationData"/> expression. Subclass to add
    /// new animation backends; each payload creates its own playback so the UI never
    /// needs to know about concrete backend types.
    /// </summary>
    [Serializable]
    public abstract class AnimatedExpressionPayload
    {
        /// <summary>True when the payload has enough data to play.</summary>
        public abstract bool IsConfigured { get; }

        public abstract IAnimatedExpressionPlayback CreatePlayback(AnimatedPlaybackContext context);

#if UNITY_EDITOR
        /// <summary>
        /// Representative sprite for inspector and hover previews.
        /// <paramref name="normalizedTime"/> (0..1) lets animated previews step frames.
        /// May return null (e.g. prefab backends with no sprite).
        /// </summary>
        public abstract Sprite GetPreviewSprite(float normalizedTime);
#endif
    }

    public enum FlipbookLoopMode
    {
        /// <summary>Repeat from the first frame after the last.</summary>
        Loop,
        /// <summary>Play once and hold the last frame.</summary>
        Once,
        /// <summary>Play forward then backward, repeating.</summary>
        PingPong
    }

    /// <summary>
    /// Frame-by-frame sprite animation: a list of sprites cycled at a fixed rate.
    /// </summary>
    [Serializable]
    public sealed class FlipbookAnimationPayload : AnimatedExpressionPayload
    {
        [Tooltip("Sprite frames played in order.")]
        public List<Sprite> Frames = new();

        [Min(0.01f), Tooltip("Playback rate in frames per second.")]
        public float FramesPerSecond = 8f;

        public FlipbookLoopMode LoopMode = FlipbookLoopMode.Loop;

        public override bool IsConfigured => Frames != null && Frames.Count > 0;

        public override IAnimatedExpressionPlayback CreatePlayback(AnimatedPlaybackContext context) =>
            new FlipbookPlayback(this, context);

#if UNITY_EDITOR
        public override Sprite GetPreviewSprite(float normalizedTime)
        {
            if (Frames == null || Frames.Count == 0) return null;
            int index = Mathf.Clamp(Mathf.FloorToInt(normalizedTime * Frames.Count), 0, Frames.Count - 1);
            return Frames[index];
        }
#endif
    }

    public enum AnimatorControlMode
    {
        /// <summary>Play a named state directly (default; robust on freshly activated Animators).</summary>
        PlayState,
        /// <summary>Fire a trigger parameter.</summary>
        SetTrigger
    }

    /// <summary>
    /// Animator-driven animation: a prefab containing an Animator is hosted on the
    /// surface and a state or trigger is fired per expression. For uGUI surfaces the
    /// prefab should be a UI prefab (RectTransform root).
    /// </summary>
    [Serializable]
    public sealed class AnimatorPrefabAnimationPayload : AnimatedExpressionPayload
    {
        [Tooltip("Prefab with an Animator that provides the animated visuals. Hosted under the portrait/slot surface.")]
        public GameObject AnimatedPrefab;

        public AnimatorControlMode ControlMode = AnimatorControlMode.PlayState;

        [Tooltip("State name (Play State) or trigger parameter name (Set Trigger).")]
        public string StateOrTriggerName = "Idle";

        [Tooltip("Animator layer used with Play State.")]
        public int Layer = 0;

        public override bool IsConfigured => AnimatedPrefab != null;

        public override IAnimatedExpressionPlayback CreatePlayback(AnimatedPlaybackContext context) =>
            new AnimatorPrefabPlayback(this, context);

#if UNITY_EDITOR
        public override Sprite GetPreviewSprite(float normalizedTime)
        {
            if (AnimatedPrefab == null) return null;
            var image = AnimatedPrefab.GetComponentInChildren<UnityEngine.UI.Image>(true);
            if (image != null && image.sprite != null) return image.sprite;
            var spriteRenderer = AnimatedPrefab.GetComponentInChildren<SpriteRenderer>(true);
            return spriteRenderer != null ? spriteRenderer.sprite : null;
        }
#endif
    }

    internal sealed class FlipbookPlayback : IAnimatedExpressionPlayback
    {
        private readonly FlipbookAnimationPayload _payload;
        private readonly IAnimatedPortraitSurface _surface;
        private float _elapsed;
        private int _currentIndex = -1;

        public FlipbookPlayback(FlipbookAnimationPayload payload, AnimatedPlaybackContext context)
        {
            _payload = payload;
            _surface = context.Surface;
        }

        public void Start()
        {
            _elapsed = 0f;
            ApplyFrame(0);
        }

        public void Tick(float deltaTime)
        {
            int count = _payload.Frames?.Count ?? 0;
            if (count <= 1) return;

            _elapsed += deltaTime;
            int rawFrame = Mathf.FloorToInt(_elapsed * _payload.FramesPerSecond);

            int index;
            switch (_payload.LoopMode)
            {
                case FlipbookLoopMode.Once:
                    index = Mathf.Min(rawFrame, count - 1);
                    break;
                case FlipbookLoopMode.PingPong:
                    int cycle = count * 2 - 2;
                    int phase = rawFrame % cycle;
                    index = phase < count ? phase : cycle - phase;
                    break;
                default: // Loop
                    index = rawFrame % count;
                    break;
            }

            ApplyFrame(index);
        }

        public void Dispose()
        {
            // Leaves the last frame on the surface; the driver decides visibility.
        }

        private void ApplyFrame(int index)
        {
            if (index == _currentIndex) return;
            var frames = _payload.Frames;
            if (frames == null || index < 0 || index >= frames.Count) return;
            _currentIndex = index;
            _surface.SetFrame(frames[index]);
        }
    }

    internal sealed class AnimatorPrefabPlayback : IAnimatedExpressionPlayback
    {
        private readonly AnimatorPrefabAnimationPayload _payload;
        private readonly AnimatedPlaybackContext _context;
        private GameObject _instance;

        public AnimatorPrefabPlayback(AnimatorPrefabAnimationPayload payload, AnimatedPlaybackContext context)
        {
            _payload = payload;
            _context = context;
        }

        public void Start()
        {
            var host = _context.Surface.GetPrefabHost();
            if (host == null)
            {
                Debug.LogWarning("[AnimatorPrefabPlayback] The active portrait surface does not support " +
                                 "prefab-hosted animation; skipping animator payload.");
                return;
            }

            if (_context.InstantiateCached == null)
            {
                Debug.LogWarning("[AnimatorPrefabPlayback] No InstantiateCached callback supplied; skipping animator payload.");
                return;
            }

            _instance = _context.InstantiateCached(_payload.AnimatedPrefab);
            if (_instance == null) return;

            _instance.SetActive(true);
            _context.Surface.SetNativeVisualVisible(false);

            var animator = _instance.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogWarning($"[AnimatorPrefabPlayback] Prefab '{_payload.AnimatedPrefab.name}' has no Animator.");
                return;
            }

            animator.updateMode = _context.UseUnscaledTime
                ? AnimatorUpdateMode.UnscaledTime
                : AnimatorUpdateMode.Normal;

            if (string.IsNullOrEmpty(_payload.StateOrTriggerName)) return;

            if (_payload.ControlMode == AnimatorControlMode.PlayState)
            {
                animator.Play(_payload.StateOrTriggerName, _payload.Layer, 0f);
            }
            else
            {
                // Freshly activated Animators can swallow triggers before the controller
                // initializes; force an update first.
                animator.Update(0f);
                animator.ResetTrigger(_payload.StateOrTriggerName);
                animator.SetTrigger(_payload.StateOrTriggerName);
            }
        }

        public void Tick(float deltaTime)
        {
            // The Animator updates itself.
        }

        public void Dispose()
        {
            if (_instance != null)
                _instance.SetActive(false);
            _context.Surface.SetNativeVisualVisible(true);
        }
    }
}
