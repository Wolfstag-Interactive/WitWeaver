using System.Collections;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Drop-on component that implements both <see cref="IConvoCoreFadeIn"/> and
    /// <see cref="IConvoCoreFadeOut"/> by fading a <see cref="CanvasGroup"/> or a
    /// <see cref="Renderer"/> material's alpha over a configurable duration.
    ///
    /// Resolution order:
    /// <list type="number">
    ///   <item>A <see cref="CanvasGroup"/> on this GameObject.</item>
    ///   <item>A <see cref="Renderer"/> on this GameObject or a child.</item>
    /// </list>
    ///
    /// For Renderer fading the material must expose a <c>_BaseColor</c> (URP) or <c>_Color</c>
    /// (Built-In) property and use a blending mode that supports transparency.
    /// </summary>
    public class ConvoCoreSimpleFade : MonoBehaviour, IConvoCoreFadeIn, IConvoCoreFadeOut
    {
        [Tooltip("Duration of the fade in seconds.")]
        [SerializeField] private float duration = 0.3f;

        private CanvasGroup _canvasGroup;
        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Coroutine _fadeCoroutine;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _renderer = GetComponentInChildren<Renderer>();
                if (_renderer != null)
                    _mpb = new MaterialPropertyBlock();
            }
        }

        public void FadeIn(System.Action onComplete = null)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeRoutine(0f, 1f, onComplete));
        }

        public void FadeOutAndRelease(System.Action onComplete)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeRoutine(1f, 0f, onComplete));
        }

        private IEnumerator FadeRoutine(float from, float to, System.Action onComplete)
        {
            SetAlpha(from);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            SetAlpha(to);
            _fadeCoroutine = null;
            onComplete?.Invoke();
        }

        private void SetAlpha(float alpha)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = alpha;
                return;
            }

            if (_renderer != null && _mpb != null)
            {
                _renderer.GetPropertyBlock(_mpb);

                // Try URP _BaseColor first, fall back to Built-In _Color.
                var color = _mpb.GetColor(BaseColorId);
                if (color == default)
                    color = _mpb.GetColor(ColorId);

                color.a = alpha;
                _mpb.SetColor(BaseColorId, color);
                _mpb.SetColor(ColorId, color);
                _renderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
