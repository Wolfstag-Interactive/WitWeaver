// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// History renderer that outputs formatted text (e.g., color and bold speaker names)
    /// using a generic IDialogueHistoryOutput. No direct Unity UI references are held.
    /// </summary>
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1RichTextHistoryRenderer.html")]
    public class RichTextHistoryRenderer : IWitWeaverHistoryRenderer
    {
        public string RendererName => "Rich";

        private IDialogueHistoryOutput _output;
        private readonly StringBuilder _buffer = new();

        public void Initialize(object context = null)
        {
            if (context is DialogueHistoryRendererContext ctx)
                _output = ctx.OutputHandler;

            _buffer.Clear();
            _output?.Clear();
        }

        public void Clear()
        {
            _buffer.Clear();
            _output?.Clear();
        }

        public void RenderAll(IReadOnlyList<DialogueHistoryEntry> entries)
        {
            _buffer.Clear();
            foreach (var e in entries)
                AppendFormatted(e);

            _output?.Clear();
            _output?.Append(_buffer.ToString());
            _output?.RefreshView();
        }

        public void RenderEntry(DialogueHistoryEntry entry)
        {
            AppendFormatted(entry);
            _output?.Append(_buffer.ToString());
            _output?.RefreshView();
        }

        private void AppendFormatted(DialogueHistoryEntry entry)
        {
            var color = entry.SpeakerTextColor == Color.clear
                ? "#FFFFFF"
                : ColorUtility.ToHtmlStringRGBA(entry.SpeakerTextColor);

            if (!color.StartsWith("#"))
                color = "#" + color;

            // Build formatted line using TMP rich text markup
            _buffer.Clear();
            _buffer.AppendLine($"<b><color={color}>{entry.Speaker}</color>:</b> {entry.Text}");
        }

        public void Tick(float deltaTime) { }
    }
}