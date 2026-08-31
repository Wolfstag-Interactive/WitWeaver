using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Plays a one-shot audio clip whenever the expression it is attached to is applied —
    /// a vocal bark, a sting, or a UI cue tied to an emotion.
    ///
    /// Remember the expression-action contract: this fires every time the expression is applied,
    /// including when the player navigates back to the line. A one-shot sound is naturally
    /// idempotent, which is what makes it a good fit for an expression action.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1PlayOneShotAudioExpressionAction.html")]
[CreateAssetMenu(fileName = "PlayOneShotAudioExpressionAction",
        menuName = "WitWeaver/Expression Actions/Play One Shot Audio")]
    public class PlayOneShotAudioExpressionAction : BaseExpressionAction
    {
        [Tooltip("Clip played each time the expression is applied.")]
        public AudioClip Clip;

        [Range(0f, 1f)]
        [Tooltip("Playback volume.")]
        public float Volume = 1f;

        public override void ExecuteAction(ExpressionActionContext context)
        {
            if (Clip == null)
            {
                Debug.LogWarning($"[WitWeaver] {name}: no AudioClip assigned.", this);
                return;
            }

            // 2D-style playback at the listener: position at the active camera when one exists.
            var camera = Camera.main;
            var position = camera != null ? camera.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(Clip, position, Volume);
        }
    }
}
