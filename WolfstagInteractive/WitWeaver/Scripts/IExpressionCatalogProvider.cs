// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System.Collections.Generic;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Implemented by character representations that expose a list of expressions
    /// (stable GUID + human-readable name) for editor dropdowns and tooling.
    /// </summary>
    public interface IExpressionCatalogProvider
    {
        IReadOnlyList<(string id, string name)> GetExpressionCatalog();
    }
}
