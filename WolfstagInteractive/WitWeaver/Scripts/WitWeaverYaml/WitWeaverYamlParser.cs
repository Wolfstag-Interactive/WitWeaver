using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Provides methods to parse YAML text into a conversation data structure and manage
    /// associated dialogue configurations.
    ///
    /// <para>
    /// Parsing runs as a three-phase pipeline:
    /// <list type="number">
    ///   <item><description>
    ///     <b>Pre-flight</b> — <see cref="ConvoCoreYamlPreprocessor"/> scans the raw text
    ///     for file-level problems (BOM, merge conflicts, multiple documents, tab
    ///     indentation, smart quotes, zero-width characters).
    ///   </description></item>
    ///   <item><description>
    ///     <b>State machine transform</b> — <see cref="ConvoCoreYamlPreprocessor"/> walks
    ///     the file and applies the escape pipeline only to locale value lines inside
    ///     <c>LocalizedDialogue</c> blocks; everything else passes through unchanged.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Post-process validation</b> — after YamlDotNet deserialises the processed
    ///     YAML, <see cref="ValidateRoundTrip"/> checks for silent data corruption
    ///     (embedded control characters that indicate a misinterpreted escape sequence).
    ///   </description></item>
    /// </list>
    /// </para>
    /// </summary>
    [UnityEngine.HelpURL(
        "https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreYamlParser.html")]
    public static class ConvoCoreYamlParser
    {
        // Build the YamlDotNet deserializer once and reuse across calls.
        private static readonly IDeserializer Deserializer =
            new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

        // ───────────────────────────────────────────────────────────────────────────
        //  Public API
        // ───────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses <paramref name="yamlText"/> into a
        /// <c>Dictionary&lt;conversationKey, List&lt;DialogueYamlConfig&gt;&gt;</c>.
        /// Locale keys are normalised to lower-invariant and stored in a
        /// case-insensitive dictionary.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///   Thrown when pre-flight or post-process validation finds blocking errors.
        ///   The exception message contains the full human-readable diagnostic block.
        /// </exception>
        /// <exception cref="YamlException">
        ///   Re-thrown from YamlDotNet when the preprocessed YAML is syntactically invalid.
        /// </exception>
        public static Dictionary<string, List<DialogueYamlConfig>> Parse(string yamlText)
        {
            if (string.IsNullOrWhiteSpace(yamlText))
                return new Dictionary<string, List<DialogueYamlConfig>>();

            // Phase 1 + 2: pre-flight checks and state-machine transform.
            var pre = ConvoCoreYamlPreprocessor.Run(yamlText);
            if (!pre.Success)
                throw new InvalidOperationException(
                    ConvoCoreYamlDiagnostic.Format(null, pre.Diagnostics));

            // Phase 3a: YamlDotNet deserialisation (may throw YamlException naturally).
            var dict = Deserializer
                           .Deserialize<Dictionary<string, List<DialogueYamlConfig>>>(pre.ProcessedYaml)
                       ?? new Dictionary<string, List<DialogueYamlConfig>>();

            // Phase 3b: round-trip corruption check.
            var corruption = ValidateRoundTrip(dict);
            if (corruption.Count > 0)
                throw new InvalidOperationException(
                    ConvoCoreYamlDiagnostic.Format(null, corruption));

            foreach (var kv in dict)
                NormalizeLocales(kv.Value);

            return dict;
        }

        /// <summary>
        /// Safe parse that will not throw. Returns <c>false</c> and populates
        /// <paramref name="error"/> with a human-readable diagnostic block on failure.
        /// On success <paramref name="error"/> is <c>null</c>; use the structured
        /// overload to also receive warnings.
        /// </summary>
        public static bool TryParse(
            string yamlText,
            out Dictionary<string, List<DialogueYamlConfig>> result,
            out string error)
        {
            bool success = TryParse(yamlText, out result,
                                    out IReadOnlyList<ConvoCoreYamlDiagnostic> diagnostics);
            error = success ? null : ConvoCoreYamlDiagnostic.Format(null, diagnostics);
            return success;
        }

        /// <summary>
        /// Structured overload: returns all diagnostics (errors <em>and</em> warnings)
        /// regardless of whether parsing succeeds. Callers should surface warnings even
        /// when this method returns <c>true</c>.
        /// </summary>
        public static bool TryParse(
            string yamlText,
            out Dictionary<string, List<DialogueYamlConfig>> result,
            out IReadOnlyList<ConvoCoreYamlDiagnostic> diagnostics)
        {
            result = null;

            if (string.IsNullOrWhiteSpace(yamlText))
            {
                result      = new Dictionary<string, List<DialogueYamlConfig>>();
                diagnostics = Array.Empty<ConvoCoreYamlDiagnostic>();
                return true;
            }

            // ── Phase 1 + 2: preprocessor ─────────────────────────────────────────
            PreprocessorResult pre;
            try
            {
                pre = ConvoCoreYamlPreprocessor.Run(yamlText);
            }
            catch (Exception ex)
            {
                diagnostics = new[]
                {
                    new ConvoCoreYamlDiagnostic(DiagnosticSeverity.Error, 0, 0, null,
                        "INTERNAL", $"Preprocessor threw an unexpected exception: {ex.Message}")
                };
                return false;
            }

            if (!pre.Success)
            {
                diagnostics = pre.Diagnostics;
                return false;
            }

            // ── Phase 3a: YamlDotNet deserialisation ──────────────────────────────
            Dictionary<string, List<DialogueYamlConfig>> dict;
            try
            {
                dict = Deserializer
                           .Deserialize<Dictionary<string, List<DialogueYamlConfig>>>(pre.ProcessedYaml)
                       ?? new Dictionary<string, List<DialogueYamlConfig>>();
            }
            catch (YamlException ex)
            {
                var d       = BuildYamlExceptionDiagnostic(yamlText, ex, pre.ColumnOffsets);
                var combined = new List<ConvoCoreYamlDiagnostic>(pre.Diagnostics) { d };
                diagnostics  = combined;
                return false;
            }
            catch (Exception ex)
            {
                var d       = new ConvoCoreYamlDiagnostic(DiagnosticSeverity.Error, 0, 0, null,
                                  "PARSE_ERROR", ex.Message);
                var combined = new List<ConvoCoreYamlDiagnostic>(pre.Diagnostics) { d };
                diagnostics  = combined;
                return false;
            }

            // ── Phase 3b: round-trip corruption check ─────────────────────────────
            var corruption = ValidateRoundTrip(dict);
            if (corruption.Count > 0)
            {
                var combined = new List<ConvoCoreYamlDiagnostic>(pre.Diagnostics);
                combined.AddRange(corruption);
                diagnostics = combined;
                return false;
            }

            foreach (var kv in dict)
                NormalizeLocales(kv.Value);

            result      = dict;
            diagnostics = pre.Diagnostics; // may contain warnings
            return true;
        }

        /// <summary>Convenience: enumerate available conversation keys from a parsed dict.</summary>
        public static IEnumerable<string> GetConversationKeys(
            Dictionary<string, List<DialogueYamlConfig>> dict)
            => (IEnumerable<string>)dict?.Keys ?? Array.Empty<string>();

        // ───────────────────────────────────────────────────────────────────────────
        //  Private helpers
        // ───────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a <see cref="ConvoCoreYamlDiagnostic"/> from a YamlDotNet exception,
        /// correcting the reported column number via the preprocessor's offset map so
        /// the caret points at the right place in the <em>original</em> source line.
        /// </summary>
        private static ConvoCoreYamlDiagnostic BuildYamlExceptionDiagnostic(
            string originalYaml,
            YamlException ex,
            IReadOnlyDictionary<int, int> columnOffsets)
        {
            int line = ex.Start.Line;   // 1-indexed
            int col  = ex.Start.Column; // 1-indexed

            // Subtract characters the transform inserted before this column.
            if (columnOffsets != null && columnOffsets.TryGetValue(line - 1, out int added))
                col = Math.Max(1, col - added);

            string[] lines = originalYaml
                .Replace("\r\n", "\n").Replace("\r", "\n")
                .Split('\n');

            string sourceLine = (line >= 1 && line <= lines.Length)
                ? lines[line - 1]
                : string.Empty;

            // Diagnose the most common authoring mistakes from the source line text.
            string   problem;
            string[] fixSteps;

            if (!string.IsNullOrEmpty(sourceLine))
            {
                string trimmedEnd = sourceLine.TrimEnd();
                if (trimmedEnd.EndsWith("\\"))
                {
                    problem  = "Trailing backslash creates an unterminated double-quoted string.";
                    fixSteps = new[]
                    {
                        "Remove the trailing backslash.",
                        "Or escape it as '\\\\' if a literal backslash is intended."
                    };
                }
                else if (sourceLine.Contains("\\"))
                {
                    problem  = "Unescaped backslash — in YAML double-quoted strings '\\' is an " +
                               "escape character and must be written as '\\\\'.";
                    fixSteps = new[]
                    {
                        "Replace each '\\' with '\\\\' for a literal backslash.",
                        "Or use a single-quoted value (single quotes do not interpret backslashes):  " +
                            "en: 'C:\\Users\\name'"
                    };
                }
                else
                {
                    problem  = ex.Message;
                    fixSteps = new[] { "Check the YAML syntax at this location." };
                }
            }
            else
            {
                problem  = ex.Message;
                fixSteps = new[] { "Check the YAML syntax at this location." };
            }

            return new ConvoCoreYamlDiagnostic(
                DiagnosticSeverity.Error, line, col, sourceLine,
                "YAML_ERROR", problem, fixSteps);
        }

        /// <summary>
        /// Post-process check: scans every parsed locale value for embedded control
        /// characters (actual newline, carriage return, null byte) that indicate a
        /// backslash escape sequence was incorrectly interpreted during preprocessing.
        /// </summary>
        private static List<ConvoCoreYamlDiagnostic> ValidateRoundTrip(
            Dictionary<string, List<DialogueYamlConfig>> dict)
        {
            var errors = new List<ConvoCoreYamlDiagnostic>();

            foreach (var conv in dict)
            {
                if (conv.Value == null) continue;
                foreach (var entry in conv.Value)
                {
                    if (entry?.LocalizedDialogue == null) continue;
                    foreach (var kv in entry.LocalizedDialogue)
                    {
                        string val = kv.Value;
                        if (val == null) continue;

                        if (val.IndexOf('\n') >= 0 ||
                            val.IndexOf('\r') >= 0 ||
                            val.IndexOf('\0') >= 0)
                        {
                            errors.Add(new ConvoCoreYamlDiagnostic(
                                DiagnosticSeverity.Error, 0, 0, null,
                                "CORRUPTION",
                                $"Locale value for '{kv.Key}' in conversation '{conv.Key}' contains " +
                                "embedded control characters (newline, carriage return, or null byte) " +
                                "after preprocessing — a backslash escape sequence was likely misinterpreted.",
                                "Check this dialogue line for backslash characters and escape them as " +
                                "'\\\\' for a literal backslash.",
                                $"Or use a single-quoted value:  {kv.Key}: 'your text with \\backslash'"));
                        }
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Lowercases / normalises <c>LocalizedDialogue</c> keys and replaces the map
        /// with a case-insensitive equivalent.
        /// </summary>
        private static void NormalizeLocales(List<DialogueYamlConfig> list)
        {
            if (list == null) return;
            foreach (var cfg in list)
            {
                if (cfg?.LocalizedDialogue == null) continue;
                var norm = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in cfg.LocalizedDialogue)
                {
                    if (string.IsNullOrEmpty(p.Key)) continue;
                    norm[p.Key.Trim().ToLowerInvariant()] = p.Value;
                }
                cfg.LocalizedDialogue = norm;
            }
        }
    }
}
