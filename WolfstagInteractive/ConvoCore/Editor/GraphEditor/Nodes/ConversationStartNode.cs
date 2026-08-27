using UnityEngine;
using System;
using Unity.GraphToolkit.Editor;

namespace WolfstagInteractive.ConvoCore.GraphEditor
{
    /// <summary>
    /// Entry point of a conversation graph. Exactly one must exist per graph; its "Next"
    /// connection identifies the first dialogue line of the baked conversation.
    /// </summary>
    [Serializable]
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1GraphEditor_1_1ConversationStartNode.html")]
[UseWithGraph(typeof(ConvoCoreConversationGraph))]
    public class ConversationStartNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort(ConvoCoreGraphSchema.NextPort)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .WithTooltip("First dialogue line of the conversation.")
                .Build();
        }
    }
}
