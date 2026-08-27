using UnityEngine;
using System.Collections;

namespace WolfstagInteractive.ConvoCore
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreDialogueLineActionModifyTransform.html")]
[CreateAssetMenu(menuName = "ConvoCore/Actions/ModifyTransform")] [System.Serializable]
    public class ConvoCoreDialogueLineActionModifyTransform : BaseDialogueLineAction
    {
        public string TransformName;
        public Vector3 NewPosition;
        public Vector3 NewRotation;
        public Vector3 NewScale;
        public override IEnumerator ExecuteLineAction()
        {
            Transform transform = GameObject.Find(TransformName).transform;
            if (transform == null)
            {
                Debug.LogError("Transform not found");
                yield break;
            }
            transform.SetPositionAndRotation(NewPosition, Quaternion.Euler(NewRotation));
            transform.localScale = NewScale;
            yield return null; 
        }
    }
}