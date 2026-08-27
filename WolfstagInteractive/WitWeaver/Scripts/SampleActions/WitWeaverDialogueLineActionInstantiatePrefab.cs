using System.Collections;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreDialogueLineActionInstantiatePrefab.html")]
[CreateAssetMenu(menuName = "ConvoCore/Actions/InstantiatePrefab")][ System.Serializable]
    public class ConvoCoreDialogueLineActionInstantiatePrefab : BaseDialogueLineAction
    {
        public GameObject Prefab;
        public Vector3 Position;
        public Vector3 Rotation;

        public override IEnumerator ExecuteLineAction()
        {
            Instantiate(Prefab, Position, Quaternion.Euler(Rotation));
            return base.ExecuteLineAction();
        }
    }
}