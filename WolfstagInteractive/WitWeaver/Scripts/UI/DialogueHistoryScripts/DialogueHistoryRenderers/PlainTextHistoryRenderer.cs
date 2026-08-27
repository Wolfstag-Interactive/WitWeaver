using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Simple dialogue history renderer that appends plain text lines
    /// to a TMP_Text component with no markup or color formatting.
    /// </summary>
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1PlainTextHistoryRenderer.html")]
    public class PlainTextHistoryRenderer : IConvoCoreHistoryRenderer
    {
        public string RendererName => "Plain";

        private IDialogueHistoryOutput  _output;
        private ScrollRect _scroll;
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

        public void RenderEntry(DialogueHistoryEntry entry)
        {
            _buffer.AppendLine($"{entry.Speaker}: {entry.Text}");
            _output?.Append($"{entry.Speaker}: {entry.Text}\n");
            _output?.RefreshView();
        }

        public void RenderAll(IReadOnlyList<DialogueHistoryEntry> entries)
        {
            _buffer.Clear();
            foreach (var e in entries)
                _buffer.AppendLine($"{e.Speaker}: {e.Text}");

            _output?.Clear();
            _output?.Append(_buffer.ToString());
            _output?.RefreshView();
        }

        private void AppendPlain(DialogueHistoryEntry entry)
        {
            _buffer.AppendLine($"{entry.Speaker}: {entry.Text}");
        }

        private void ForceScrollToBottom()
        {
            if (_scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                _scroll.verticalNormalizedPosition = 0f;
            }
        }

        public void Tick(float deltaTime) { }
    }
}