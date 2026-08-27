using UnityEngine;
using System;
using Unity.GraphToolkit.Editor;

namespace WolfstagInteractive.ConvoCore.GraphEditor
{
    /// <summary>
    /// Explicit conversation end. An unconnected "Next" also bakes as an end (with a validation
    /// warning); wiring into this node states the intent visibly.
    /// </summary>
    [Serializable]
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1GraphEditor_1_1EndConversationNode.html")]
[UseWithGraph(typeof(ConvoCoreConversationGraph))]
    public class EndConversationNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort(ConvoCoreGraphSchema.InPort)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }
    }
}
