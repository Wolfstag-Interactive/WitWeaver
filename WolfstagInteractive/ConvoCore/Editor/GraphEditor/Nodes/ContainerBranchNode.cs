using UnityEngine;
using System;
using Unity.GraphToolkit.Editor;

namespace WolfstagInteractive.ConvoCore.GraphEditor
{
    /// <summary>
    /// Leaves the current conversation and hands control to a <see cref="ConversationContainer"/>.
    /// Control flow does not return through this node — returns happen at runtime via the
    /// return-point stack when "Push Return Point" is set on the way in.
    /// </summary>
    [Serializable]
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1GraphEditor_1_1ContainerBranchNode.html")]
[UseWithGraph(typeof(ConvoCoreConversationGraph))]
    public class ContainerBranchNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort(ConvoCoreGraphSchema.InPort)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddInputPort<ConversationContainer>(ConvoCoreGraphSchema.ContainerPort)
                .WithTooltip("Container that selects and plays the next conversation.")
                .Build();

            context.AddInputPort<string>(ConvoCoreGraphSchema.AliasOrNamePort)
                .WithTooltip("Optional alias or entry name inside the container. Empty lets the container decide.")
                .Delayed()
                .Build();

            context.AddInputPort<bool>(ConvoCoreGraphSchema.PushReturnPort)
                .WithTooltip("Push a return point so the branched conversation can come back here when it ends.")
                .Build();
        }
    }
}
