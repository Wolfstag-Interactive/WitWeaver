#if UNITY_EDITOR
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Interface for character representations that want to provide a custom editor UI for per-line display options
    /// </summary>
    public interface IDialogueLineEditorCustomizable
    {
        /// <summary>
        /// Draws custom editor fields for per-line display options
        /// </summary>
        /// <param name="rect">The rect to draw in</param>
        /// <param name="expressionID">The selected expression ID</param>
        /// <param name="displayOptionsProperty">SerializedProperty for the LineSpecificDisplayOptions</param>
        /// <param name="spacing">Spacing between elements</param>
        /// <returns>The updated rect after drawing</returns>
        Rect DrawDialogueLineOptions(Rect rect, string expressionID, UnityEditor.SerializedProperty displayOptionsProperty, float spacing);
        
        /// <summary>
        /// Gets the height needed for the custom dialogue line options
        /// </summary>
        /// <param name="expressionID">The selected expression ID</param>
        /// <param name="displayOptionsProperty">SerializedProperty for the LineSpecificDisplayOptions</param>
        /// <returns>Height in pixels</returns>
        float GetDialogueLineOptionsHeight(string expressionID, UnityEditor.SerializedProperty displayOptionsProperty);
    }
}
#endif