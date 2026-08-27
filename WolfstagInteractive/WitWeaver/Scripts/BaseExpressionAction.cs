using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Abstract base ScriptableObject for custom expression actions. Extend this class to run
    /// game logic whenever a character expression is applied to a line — for example, triggering
    /// an animation, particle effect, or audio cue when a specific emotion is displayed.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1BaseExpressionAction.html")]
    public abstract class BaseExpressionAction : ScriptableObject
    {
        /// <summary>
        /// Executes this action 
        /// </summary>
        /// <param name="context"></param>
        public abstract void ExecuteAction(ExpressionActionContext context);
    }
    /// <summary>
    /// Context passed to BaseExpressionAction when a character expression is applied.
    /// </summary>
    public struct ExpressionActionContext
    {
        // Dialogue side
        public ConvoCore Runtime;
        public ConvoCoreConversationData Conversation;
        public int LineIndex;

        // Representation side
        public CharacterRepresentationBase Representation;
        public IConvoCoreCharacterDisplay Display;

        // Expression info
        public string ExpressionId;
    }
}