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
