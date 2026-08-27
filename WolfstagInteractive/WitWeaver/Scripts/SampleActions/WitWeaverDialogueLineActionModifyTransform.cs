using UnityEngine;
using System.Collections;

namespace WolfstagInteractive.WitWeaver
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverDialogueLineActionModifyTransform.html")]
[CreateAssetMenu(menuName = "WitWeaver/Actions/ModifyTransform")] [System.Serializable]
    public class WitWeaverDialogueLineActionModifyTransform : BaseDialogueLineAction
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