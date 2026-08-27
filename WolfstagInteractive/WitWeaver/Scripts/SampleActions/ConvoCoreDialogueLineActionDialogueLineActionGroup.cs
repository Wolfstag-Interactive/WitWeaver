using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WolfstagInteractive.ConvoCore;

namespace WolfstagInteractive.ConvoCore
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreDialogueLineActionDialogueLineActionGroup.html")]
[CreateAssetMenu(menuName = "ConvoCore/Actions/Action Group")][ System.Serializable]
    
    public class ConvoCoreDialogueLineActionDialogueLineActionGroup : BaseDialogueLineAction
    {
        /// <summary>
        /// Add commonly executed actions together for easy reuse, executes each action in the list one after the other.
        /// </summary>
        public List<BaseDialogueLineAction> ActionGroup = new List<BaseDialogueLineAction>();
        
        public override IEnumerator ExecuteLineAction()
        {
            foreach (var t in ActionGroup)
            {
                yield return t.ExecuteLineAction();
            }
        }
    }
}