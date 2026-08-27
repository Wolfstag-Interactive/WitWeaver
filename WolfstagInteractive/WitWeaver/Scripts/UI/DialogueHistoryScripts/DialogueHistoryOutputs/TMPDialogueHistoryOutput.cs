using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WolfstagInteractive.ConvoCore
{
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1TMPDialogueHistoryOutput.html")]
    public class TMPDialogueHistoryOutput : IDialogueHistoryOutput
    {
        private readonly TMP_Text _text;
        private readonly ScrollRect _scroll;

        public TMPDialogueHistoryOutput(TMP_Text text, ScrollRect scroll)
        {
            _text = text;
            _scroll = scroll;
        }

        public void Clear() => _text.text = string.Empty;

        public void Append(string line)
        {
            _text.text += line;
        }

        public void RefreshView()
        {
            if (_scroll == null) return;
            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = 0f;
        }
    }

}