// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System.Collections.Generic;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Base contract for any dialogue history renderer.
    /// Renderers may target UGUI, UI Toolkit, worldspace text, etc.
    /// </summary>
    public interface IWitWeaverHistoryRenderer
    {
        string RendererName { get; }
        void Initialize(object context = null);
        void Clear();
        void RenderAll(IReadOnlyList<DialogueHistoryEntry> entries);
        void RenderEntry(DialogueHistoryEntry entry);
        void Tick(float deltaTime);
    }
}