using UnityEngine;
using System;
using Unity.GraphToolkit.Editor;

namespace WolfstagInteractive.WitWeaver.GraphEditor
{
    /// <summary>
    /// Leaves the current conversation and hands control to a <see cref="ConversationContainer"/>.
    /// Control flow does not return through this node — returns happen at runtime via the
    /// return-point stack when "Push Return Point" is set on the way in.
    /// </summary>
    [Serializable]
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1GraphEditor_1_1ContainerBranchNode.html")]
[UseWithGraph(typeof(WitWeaverConversationGraph))]
    public class ContainerBranchNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort(WitWeaverGraphSchema.InPort)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddInputPort<ConversationContainer>(WitWeaverGraphSchema.ContainerPort)
                .WithTooltip("Container that selects and plays the next conversation.")
                .Build();

            context.AddInputPort<string>(WitWeaverGraphSchema.AliasOrNamePort)
                .WithTooltip("Optional alias or entry name inside the container. Empty lets the container decide.")
                .Delayed()
                .Build();

            context.AddInputPort<bool>(WitWeaverGraphSchema.PushReturnPort)
                .WithTooltip("Push a return point so the branched conversation can come back here when it ends.")
                .Build();
        }
    }
}
