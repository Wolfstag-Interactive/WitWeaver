using System.Collections;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverDialogueLineActionInstantiatePrefab.html")]
[CreateAssetMenu(menuName = "WitWeaver/Actions/InstantiatePrefab")][ System.Serializable]
    public class WitWeaverDialogueLineActionInstantiatePrefab : BaseDialogueLineAction
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