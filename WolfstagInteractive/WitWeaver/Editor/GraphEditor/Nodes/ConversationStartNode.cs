// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;
using System;
using Unity.GraphToolkit.Editor;

namespace WolfstagInteractive.WitWeaver.GraphEditor
{
    /// <summary>
    /// Entry point of a conversation graph. Exactly one must exist per graph; its "Next"
    /// connection identifies the first dialogue line of the baked conversation.
    /// </summary>
    [Serializable]
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1GraphEditor_1_1ConversationStartNode.html")]
[UseWithGraph(typeof(WitWeaverConversationGraph))]
    public class ConversationStartNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort(WitWeaverGraphSchema.NextPort)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .WithTooltip("First dialogue line of the conversation.")
                .Build();
        }
    }
}
