using UnityEngine;
using System;
using Unity.GraphToolkit.Editor;

namespace WolfstagInteractive.WitWeaver.GraphEditor
{
    /// <summary>
    /// Presents the player with choices after the dialogue line wired into "In" finishes.
    /// Each choice is a <see cref="ChoiceOptionBlock"/> inside this context node; block order
    /// is presentation order (reorder blocks to reorder choices).
    /// </summary>
    [Serializable]
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1GraphEditor_1_1PlayerChoiceNode.html")]
[UseWithGraph(typeof(WitWeaverConversationGraph))]
    public class PlayerChoiceNode : ContextNode
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort(WitWeaverGraphSchema.InPort)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .WithTooltip("The dialogue line these choices follow.")
                .Build();

            context.AddInputPort<bool>(WitWeaverGraphSchema.AllowGoBackPort)
                .WithTooltip("Appends a '← Go Back' option at runtime that revisits the previous line.")
                .Build();
        }
    }

    /// <summary>
    /// A single selectable choice inside a <see cref="PlayerChoiceNode"/>. Localized labels are
    /// typed input ports; "Target" wires to a dialogue line (intra-conversation jump), a
    /// Container Branch node (conversation switch), or an End node.
    /// </summary>
    [Serializable]
    [UseWithContext(typeof(PlayerChoiceNode))]
    public class ChoiceOptionBlock : BlockNode
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            foreach (var language in WitWeaverGraphSchema.GetLanguages())
            {
                context.AddInputPort<string>(WitWeaverGraphSchema.LabelPortName(language))
                    .Delayed()
                    .Build();
            }

            context.AddInputPort<bool>(WitWeaverGraphSchema.PushReturnPort)
                .WithTooltip("Push a return point so a later branch can come back to the line after this one.")
                .Build();

            context.AddOutputPort(WitWeaverGraphSchema.TargetPort)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .WithTooltip("Where this choice leads: a dialogue line, a Container Branch, or an End node.")
                .Build();
        }
    }
}
