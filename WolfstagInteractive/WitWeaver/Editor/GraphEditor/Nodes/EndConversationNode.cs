using UnityEngine;
using System;
using Unity.GraphToolkit.Editor;

namespace WolfstagInteractive.WitWeaver.GraphEditor
{
    /// <summary>
    /// Explicit conversation end. An unconnected "Next" also bakes as an end (with a validation
    /// warning); wiring into this node states the intent visibly.
    /// </summary>
    [Serializable]
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1GraphEditor_1_1EndConversationNode.html")]
[UseWithGraph(typeof(WitWeaverConversationGraph))]
    public class EndConversationNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort(WitWeaverGraphSchema.InPort)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }
    }
}
