using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Context container passed to renderers on initialization.
    /// Allows renderer implementations to extract whatever they need.
    /// </summary>
    [System.Serializable]
    public struct DialogueHistoryRendererContext
    {
        public IDialogueHistoryOutput OutputHandler;

        public Color DefaultSpeakerColor;
        public int MaxEntries;
    }
}