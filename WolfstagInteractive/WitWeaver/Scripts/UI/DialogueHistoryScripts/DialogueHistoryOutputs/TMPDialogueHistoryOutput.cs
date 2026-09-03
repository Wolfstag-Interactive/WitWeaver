// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WolfstagInteractive.WitWeaver
{
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1TMPDialogueHistoryOutput.html")]
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