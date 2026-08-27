using UnityEngine;
using System.Collections;



// [CreateAssetMenu(fileName = "CustomActionTransform", menuName = "ConvoCore/CustomAction")] //creates button in unity to make copy of object make sure to change menu and file name to match new action or else it wont show up in editor
//[System.Serializable] // required to make sure values in the inspector are saved

namespace WolfstagInteractive.ConvoCore
{
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1BaseDialogueLineAction.html")]
    [System.Serializable]
    /// <summary>
    /// Base ScriptableObject class for all ConvoCore dialogue line actions.
    /// Extend this class to run custom game logic before or after any dialogue line.
    /// Override <see cref="ExecuteLineAction"/> for forward playback and
    /// <see cref="ExecuteOnReversedLineAction"/> to undo side effects when the player steps back.
    /// Do not also inherit from MonoBehaviour or ScriptableObject directly — the base class handles that.
    /// </summary>
    public class BaseDialogueLineAction : ScriptableObject
    {
        [Tooltip("If true, this action will only execute once per conversation for a given line, even if the player reverses and replays that line.")]
        public bool RunOnlyOncePerConversation = false;
        /// <summary>
        /// Override this function in your custom line actions to perform custom logic that should occur when a dialogue line is presented to the user
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerator ExecuteLineAction()
        {
            //logic called before base.DoAction will execute before the timer
            yield return new WaitForSecondsRealtime(0); //wait for the time listed in wait timer
            //logic called after base.DoAction will execute after the timer
        }
        /// <summary>
        /// Override this function in your custom line action to perform custom logic that should occur when the user goes back to a previous line.
        /// Use this function to reverse custom logic from execute line action
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerator ExecuteOnReversedLineAction()
        {
            yield return new WaitForSecondsRealtime(0);
        }
    }
}