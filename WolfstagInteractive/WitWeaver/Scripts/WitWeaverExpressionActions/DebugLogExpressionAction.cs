using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Logs every field of the <see cref="ExpressionActionContext"/> when the expression it is
    /// attached to is applied. Useful as a wiring check and as a minimal reference implementation
    /// showing what an expression action receives.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1DebugLogExpressionAction.html")]
[CreateAssetMenu(fileName = "DebugLogExpressionAction",
        menuName = "WitWeaver/Expression Actions/Debug Log")]
    public class DebugLogExpressionAction : BaseExpressionAction
    {
        public override void ExecuteAction(ExpressionActionContext context)
        {
            Debug.Log(
                $"[WitWeaver] Expression action '{name}' executed. " +
                $"Expression '{context.ExpressionId}' on representation " +
                $"'{(context.Representation != null ? context.Representation.name : "<null>")}', " +
                $"line {context.LineIndex} of conversation " +
                $"'{(context.Conversation != null ? context.Conversation.ConversationKey : "<null>")}', " +
                $"runtime '{(context.Runtime != null ? context.Runtime.name : "<null>")}', " +
                $"display {(context.Display != null ? context.Display.GetType().Name : "null (sprite/animated path or automatic fallback pass)")}.",
                this);
        }
    }
}
