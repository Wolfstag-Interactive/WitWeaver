using UnityEngine;
using System;
using System.Collections.Generic;

namespace WolfstagInteractive.ConvoCore.GraphEditor
{
    /// <summary>
    /// Shared port-name constants and helpers for the conversation graph. Sync and bake code
    /// address ports by these names, so they must stay stable across versions — renaming one
    /// orphans the serialized values on existing graph assets.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1GraphEditor_1_1ConvoCoreGraphSchema.html")]
    internal static class ConvoCoreGraphSchema
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
        /// <see cref="ConvoCoreSettings.SupportedLanguages"/>, cleaned and de-duplicated,
        /// falling back to "en" when settings are missing or empty.
        /// </summary>
        public static List<string> GetLanguages()
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var supported = ConvoCoreSettings.Instance?.SupportedLanguages;
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
