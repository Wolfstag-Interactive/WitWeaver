using System.Collections.Generic;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Base contract for any dialogue history renderer.
    /// Renderers may target UGUI, UI Toolkit, worldspace text, etc.
    /// </summary>
    public interface IConvoCoreHistoryRenderer
    {
        string RendererName { get; }
        void Initialize(object context = null);
        void Clear();
        void RenderAll(IReadOnlyList<DialogueHistoryEntry> entries);
        void RenderEntry(DialogueHistoryEntry entry);
        void Tick(float deltaTime);
    }
}