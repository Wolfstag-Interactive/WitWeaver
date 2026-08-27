using System.Collections;
using UnityEngine;


namespace WolfstagInteractive.ConvoCore
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreDialogueLineActionFadeInOutSpriteRenderer.html")]
[CreateAssetMenu(fileName = "FadeInOutSpriteRendererAction", menuName = "ConvoCore/Actions/Fade In Or Out SpriteRenderer")]
    [System.Serializable]
    public class ConvoCoreDialogueLineActionFadeInOutSpriteRenderer : BaseDialogueLineAction
    {
        [Header("Target Settings")]
        [Tooltip("Reference to the GameObject containing the SpriteRenderer to fade")]
        public GameObjectReference TargetGameObject = new GameObjectReference();
        
        [Header("Fade Settings")]
        [Tooltip("Type of fade operation to perform")]
        public FadeType fadeType = FadeType.FadeIn;
        
        [Tooltip("Duration of the fade in seconds")]
        [Range(0.1f, 10f)]
        public float fadeDuration = 1f;
        
        [Tooltip("Animation curve for the fade (x = time, y = alpha)")]
        public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Alpha Values")]
        [Tooltip("Starting alpha value (0 = transparent, 1 = opaque)")]
        [Range(0f, 1f)]
        public float startAlpha = 0f;
        
        [Tooltip("Ending alpha value (0 = transparent, 1 = opaque)")]
        [Range(0f, 1f)]
        public float endAlpha = 1f;
        
        [Header("Advanced Options")]
        [Tooltip("If true, automatically sets start/end alpha based on fade type")]
        public bool useAutoAlphaValues = true;
        
        [Tooltip("If true, the action will continue even if SpriteRenderer is not found")]
        public bool continueOnError = false;
        
        [Tooltip("If true, enables the GameObject before fading in")]
        public bool enableGameObjectOnFadeIn = true;
        
        [Tooltip("If true, disables the GameObject after fading out")]
        public bool disableGameObjectOnFadeOut = true;

        public enum FadeType
        {
            FadeIn,
            FadeOut,
            Custom
        }

        public override IEnumerator ExecuteLineAction()
        {
            GameObject targetObj = TargetGameObject.GameObject;
            
            if (targetObj == null)
            {
                string errorMessage = "FadeInOutSpriteRenderer Action: Target GameObject could not be found or resolved!";
                
                if (continueOnError)
                {
                    Debug.LogWarning(errorMessage + " Continuing execution due to continueOnError setting.");
                    yield return null;
                    yield break;
                }
                else
                {
                    Debug.LogError(errorMessage);
                    yield return null;
                    yield break;
                }
            }

            SpriteRenderer spriteRenderer = targetObj.GetComponent<SpriteRenderer>();
            
            if (spriteRenderer == null)
            {
                string errorMessage = $"FadeInOutSpriteRenderer Action: No SpriteRenderer component found on GameObject '{targetObj.name}'!";
                
                if (continueOnError)
                {
                    Debug.LogWarning(errorMessage + " Continuing execution due to continueOnError setting.");
                    yield return null;
                    yield break;
                }
                else
                {
                    Debug.LogError(errorMessage);
                    yield return null;
                    yield break;
                }
            }

            // Set up alpha values based on fade type
            float fadeStartAlpha = startAlpha;
            float fadeEndAlpha = endAlpha;
            
            if (useAutoAlphaValues)
            {
                switch (fadeType)
                {
                    case FadeType.FadeIn:
                        fadeStartAlpha = 0f;
                        fadeEndAlpha = 1f;
                        break;
                    case FadeType.FadeOut:
                        fadeStartAlpha = 1f;
                        fadeEndAlpha = 0f;
                        break;
                    case FadeType.Custom:
                        // Use the manually set values
                        break;
                }
            }

            // Enable GameObject before fading in if specified
            if (fadeType == FadeType.FadeIn && enableGameObjectOnFadeIn && !targetObj.activeSelf)
            {
                targetObj.SetActive(true);
                Debug.Log($"Enabled GameObject '{targetObj.name}' before fade in.");
            }

            // Set initial alpha
            Color initialColor = spriteRenderer.color;
            initialColor.a = fadeStartAlpha;
            spriteRenderer.color = initialColor;

            Debug.Log($"Starting {fadeType} on '{targetObj.name}' over {fadeDuration} seconds (Alpha: {fadeStartAlpha:F2} → {fadeEndAlpha:F2})");

            // Perform the fade
            yield return PerformFade(spriteRenderer, fadeStartAlpha, fadeEndAlpha);

            // Disable GameObject after fading out if specified
            if (fadeType == FadeType.FadeOut && disableGameObjectOnFadeOut && fadeEndAlpha <= 0.01f)
            {
                targetObj.SetActive(false);
                Debug.Log($"Disabled GameObject '{targetObj.name}' after fade out.");
            }

            Debug.Log($"Fade {fadeType} completed on '{targetObj.name}'.");
        }

        /// <summary>
        /// Performs the actual fade operation
        /// </summary>
        private IEnumerator PerformFade(SpriteRenderer spriteRenderer, float startAlpha, float endAlpha)
        {
            float elapsed = 0f;
            Color currentColor = spriteRenderer.color;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = elapsed / fadeDuration;
                
                // Apply the animation curve
                float curveValue = fadeCurve.Evaluate(normalizedTime);
                
                // Interpolate alpha based on curve
                float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, curveValue);
                
                // Apply the new alpha
                currentColor.a = currentAlpha;
                spriteRenderer.color = currentColor;
                
                yield return null;
            }

            // Ensure we end exactly at the target alpha
            currentColor.a = endAlpha;
            spriteRenderer.color = currentColor;
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

            if (!TargetGameObject.IsValid())
            {
                validationMessage = "TargetGameObject reference cannot be resolved. Check your reference settings.";
                return false;
            }

            GameObject targetObj = TargetGameObject.GameObject;
            if (targetObj != null && targetObj.GetComponent<SpriteRenderer>() == null)
            {
                validationMessage = $"GameObject '{targetObj.name}' does not have a SpriteRenderer component.";
                return false;
            }

            if (fadeDuration <= 0)
            {
                validationMessage = "Fade duration must be greater than 0.";
                return false;
            }

            validationMessage = "Configuration is valid.";
            return true;
        }

        /// <summary>
        /// Gets a preview of what this action will do (useful for editor display)
        /// </summary>
        public string GetActionPreview()
        {
            string fadeTypeText = useAutoAlphaValues ? fadeType.ToString() : "Custom";
            GameObject targetObj = TargetGameObject?.GameObject;
            string targetName = targetObj != null ? targetObj.name : "Unknown";
            
            return $"{fadeTypeText} '{targetName}' over {fadeDuration}s";
        }
    }
}