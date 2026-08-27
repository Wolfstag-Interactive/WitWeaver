using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.GraphEditor
{
    /// <summary>
    /// One dialogue line. Identity is the hidden serialized LineID (stable across bakes and
    /// YAML reimports — the same key the SaveSystem and YAML pipeline use).
    ///
    /// Text and character are <b>read-only</b> here: the YAML is the source of truth for what is
    /// said, and the node only displays it (title = speaker, subtitle = text preview, tooltip =
    /// full text), refreshed by sync. The graph edits flow — wiring, choices, jumps, endings.
    /// Line nodes cannot be deleted from the canvas either; removing a line happens in the YAML,
    /// and Refresh From YAML removes its node.
    /// </summary>
    [Serializable]
    [UseWithGraph(typeof(WitWeaverConversationGraph))]
    public class DialogueLineNode : Node
    {
        [SerializeField, HideInInspector] private string m_LineId;
        [SerializeField, HideInInspector] private string m_CharacterId;
        [SerializeField, HideInInspector] private List<string> m_TextLanguages = new();
        [SerializeField, HideInInspector] private List<string> m_TextValues = new();

        internal string LineId => m_LineId;
        internal string CharacterId => m_CharacterId;

        internal void SetLineId(string lineId) => m_LineId = lineId;

        /// <summary>Assigns a fresh LineID when none exists yet (canvas-created nodes).</summary>
        internal void EnsureLineId()
        {
            if (string.IsNullOrEmpty(m_LineId))
                m_LineId = WitWeaverLineID.NewLineID();
        }

        internal void SetCharacterId(string characterId) => m_CharacterId = characterId ?? "";

        internal void ClearTexts()
        {
            m_TextLanguages.Clear();
            m_TextValues.Clear();
        }

        internal void SetText(string language, string text)
        {
            if (string.IsNullOrEmpty(language)) return;
            for (int i = 0; i < m_TextLanguages.Count; i++)
            {
                if (string.Equals(m_TextLanguages[i], language, StringComparison.OrdinalIgnoreCase))
                {
                    m_TextValues[i] = text ?? "";
                    return;
                }
            }
            m_TextLanguages.Add(language);
            m_TextValues.Add(text ?? "");
        }

        internal string GetText(string language)
        {
            for (int i = 0; i < m_TextLanguages.Count; i++)
            {
                if (string.Equals(m_TextLanguages[i], language, StringComparison.OrdinalIgnoreCase))
                    return m_TextValues[i] ?? "";
            }
            return "";
        }

        internal string GetFirstNonEmptyText()
        {
            for (int i = 0; i < m_TextValues.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(m_TextValues[i]))
                    return m_TextValues[i];
            }
            return "";
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort(WitWeaverGraphSchema.InPort)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddOutputPort(WitWeaverGraphSchema.NextPort)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .WithTooltip("Next step: a dialogue line, a Player Choice, a Container Branch, or an End node.")
                .Build();
        }
    }
}
