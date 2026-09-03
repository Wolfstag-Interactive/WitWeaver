// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace WolfstagInteractive.WitWeaver.GraphEditor
{
    /// <summary>
    /// Shared port-name constants and helpers for the conversation graph. Sync and bake code
    /// address ports by these names, so they must stay stable across versions — renaming one
    /// orphans the serialized values on existing graph assets.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1GraphEditor_1_1WitWeaverGraphSchema.html")]
    internal static class WitWeaverGraphSchema
    {
        // Execution-flow ports (untyped, connection-only).
        public const string InPort = "In";
        public const string NextPort = "Next";
        public const string TargetPort = "Target";

        // Data ports (typed; their embedded values hold the authored data).
        // Note: dialogue line text/character are intentionally NOT ports — the YAML owns them
        // and the line node only displays them (read-only).
        public const string ContainerPort = "Container";
        public const string AliasOrNamePort = "Alias Or Name";
        public const string PushReturnPort = "Push Return Point";
        public const string AllowGoBackPort = "Allow Go Back";

        private const string LabelPortPrefix = "Label ";

        public static string LabelPortName(string language) => $"{LabelPortPrefix}({language})";

        /// <summary>
        /// The languages the graph exposes one text/label port for. Sourced from
        /// <see cref="WitWeaverSettings.SupportedLanguages"/>, cleaned and de-duplicated,
        /// falling back to "en" when settings are missing or empty.
        /// </summary>
        public static List<string> GetLanguages()
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var supported = WitWeaverSettings.Instance?.SupportedLanguages;
            if (supported != null)
            {
                foreach (var lang in supported)
                {
                    if (string.IsNullOrWhiteSpace(lang)) continue;
                    var trimmed = lang.Trim();
                    if (seen.Add(trimmed))
                        result.Add(trimmed);
                }
            }

            if (result.Count == 0)
                result.Add("en");

            return result;
        }
    }
}
