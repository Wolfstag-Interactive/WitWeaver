using System.Collections;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreDialogueLineActionEnableDisableGameObject.html")]
[CreateAssetMenu(fileName = "EnableDisableGameObjectAction", menuName = "ConvoCore/Actions/Enable Or Disable GameObject")]
    [System.Serializable]
    public class ConvoCoreDialogueLineActionEnableDisableGameObject : BaseDialogueLineAction
    {
        [Header("GameObject Settings")]
        [Tooltip("Reference to the GameObject to enable or disable")]
        public GameObjectReference TargetGameObject = new GameObjectReference();
        
        [Header("Action Settings")]
        [Tooltip("Set to true to enable the GameObject, false to disable it")]
        public bool Enable = true;
        
        [Header("Error Handling")]
        [Tooltip("If true, the action will continue even if the GameObject is not found")]
        public bool ContinueOnError = false;

        public override IEnumerator ExecuteLineAction()
        {
            GameObject targetObj = TargetGameObject.GameObject;
            
            if (targetObj == null)
            {
                string errorMessage = "EnableDisableGameObject Action: Target GameObject could not be found or resolved!";
                
                if (ContinueOnError)
                {
                    Debug.LogWarning(errorMessage + " Continuing execution due to ContinueOnError setting.");
                }
                else
                {
                    Debug.LogError(errorMessage);
                }
                
                yield return null;
                yield break;
            }

            // Check if the GameObject is already in the desired state
            if (targetObj.activeSelf == Enable)
            {
                Debug.Log($"GameObject '{targetObj.name}' is already {(Enable ? "enabled" : "disabled")}. No action needed.");
            }
            else
            {
                // Set the active state
                targetObj.SetActive(Enable);
                Debug.Log($"GameObject '{targetObj.name}' has been {(Enable ? "enabled" : "disabled")}.");
            }

            yield return null;
        }

        /// <summary>
        /// Validates the action configuration in the editor
        /// </summary>
        public bool ValidateConfiguration(out string validationMessage)
        {
            if (TargetGameObject == null)
            {
                validationMessage = "TargetGameObject reference is not configured.";
                return false;
            }

            // Try to resolve the reference to check if it's valid
            if (!TargetGameObject.IsValid())
            {
                validationMessage = "TargetGameObject reference cannot be resolved. Check your reference settings.";
                return false;
            }

            validationMessage = "Configuration is valid.";
            return true;
        }
    }
}