using UnityEngine;
using System.Collections;

namespace WolfstagInteractive.ConvoCore
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreDialogueLineActionPlayAudioClip.html")]
[CreateAssetMenu(menuName = "ConvoCore/Actions/PlayAudioClip")] [System.Serializable]
    public class ConvoCoreDialogueLineActionPlayAudioClip : BaseDialogueLineAction
    {
        public AudioClip AudioClip;
        public Vector3 Position;
        [Range(0,1)]
        public float Volume = 1f;

        public override IEnumerator ExecuteLineAction()
        {
            AudioSource.PlayClipAtPoint(AudioClip, Position,Volume);
            yield return new WaitForSeconds(AudioClip.length);
        }

    }
}