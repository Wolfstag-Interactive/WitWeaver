// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WolfstagInteractive.WitWeaver;

namespace WolfstagInteractive.WitWeaver
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverDialogueLineActionDialogueLineActionGroup.html")]
[CreateAssetMenu(menuName = "WitWeaver/Actions/Action Group")][ System.Serializable]
    
    public class WitWeaverDialogueLineActionDialogueLineActionGroup : BaseDialogueLineAction
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