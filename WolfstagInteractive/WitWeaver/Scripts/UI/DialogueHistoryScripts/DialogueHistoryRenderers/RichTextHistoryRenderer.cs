using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// History renderer that outputs formatted text (e.g., color and bold speaker names)
    /// using a generic IDialogueHistoryOutput. No direct Unity UI references are held.
    /// </summary>
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1RichTextHistoryRenderer.html")]
    public class RichTextHistoryRenderer : IConvoCoreHistoryRenderer
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