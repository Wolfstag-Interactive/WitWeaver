using System;
using System.Collections.Generic;
using System.Text;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>Severity level of a YAML diagnostic.</summary>
    public enum DiagnosticSeverity
    {
        /// <summary>Import cannot proceed.</summary>
        Error,
        /// <summary>Import can proceed but the author should review this.</summary>
        Warning
    }

    /// <summary>
    /// A single diagnostic record produced during YAML pre-flight, preprocessing, or
    /// post-process validation. Carries the source line, a machine-readable code,
    /// a plain-English problem description, and numbered fix steps.
    /// </summary>
    public readonly struct ConvoCoreYamlDiagnostic
    {
        /// <summary>Whether this diagnostic blocks import.</summary>
        public readonly DiagnosticSeverity Severity;

        /// <summary>1-indexed source line number. 0 means file-level (no specific line).</summary>
        public readonly int Line;

        /// <summary>1-indexed column within the source line. 0 means unknown.</summary>
        public readonly int Column;

        /// <summary>The original (pre-transform) text of the problematic source line.</summary>
        public readonly string SourceLine;

        /// <summary>Short machine-readable tag identifying the issue, e.g. "BACKSLASH", "CONFLICT".</summary>
        public readonly string Code;

        /// <summary>Plain-English description of the problem.</summary>
        public readonly string Problem;

        /// <summary>Ordered fix instructions presented to the author.</summary>
        public readonly string[] FixSteps;

        public ConvoCoreYamlDiagnostic(
            DiagnosticSeverity severity,
            int    line,
            int    column,
            string sourceLine,
            string code,
            string problem,
            params string[] fixSteps)
        {
            Severity   = severity;
            Line       = line;
            Column     = column;
            SourceLine = sourceLine ?? string.Empty;
            Code       = code       ?? string.Empty;
            Problem    = problem    ?? string.Empty;
            FixSteps   = fixSteps   ?? Array.Empty<string>();
        }

        // ─── Formatting ────────────────────────────────────────────────────────────

        /// <summary>
        /// Formats a collection of diagnostics into a single human-readable block
        /// suitable for Unity's <c>Debug.LogError</c> / <c>Debug.LogWarning</c>.
        /// </summary>
        /// <param name="filename">
        ///   Optional filename shown in the header (e.g. "MyConversation.yaml").
        ///   Pass <c>null</c> to omit.
        /// </param>
        public static string Format(string filename, IEnumerable<ConvoCoreYamlDiagnostic> diagnostics)
        {
            var list     = new List<ConvoCoreYamlDiagnostic>(diagnostics);
            bool hasError = list.Exists(d => d.Severity == DiagnosticSeverity.Error);
            string outcome = hasError ? "import failed" : "imported with warnings";

            var sb = new StringBuilder();
            sb.AppendLine(!string.IsNullOrEmpty(filename)
                ? $"[ConvoCore] YAML {outcome} — {filename}"
                : $"[ConvoCore] YAML {outcome}");

            foreach (var d in list)
            {
                sb.AppendLine();

                // ── Header line ──────────────────────────────────────
                string icon  = d.Severity == DiagnosticSeverity.Error ? "✗" : "⚠";
                string where = d.Line > 0 ? $"Line {d.Line}" : "File";
                if (d.Column > 0) where += $", Col {d.Column}";
                sb.AppendLine($"  {icon} {where}  [{d.Code}]");

                // ── Source line + caret ──────────────────────────────
                if (!string.IsNullOrEmpty(d.SourceLine))
                {
                    sb.Append("    ").AppendLine(d.SourceLine);
                    if (d.Column > 0)
                    {
                        // Use char count (not byte count) so multi-byte/emoji stay aligned.
                        int caretPos = Math.Max(0, d.Column - 1);
                        sb.Append("    ").Append(new string(' ', caretPos)).AppendLine("^");
                    }
                }

                // ── Problem description ──────────────────────────────
                sb.AppendLine($"  Problem: {d.Problem}");

                // ── Fix steps ───────────────────────────────────────
                if (d.FixSteps.Length > 0)
                {
                    sb.AppendLine("  Fix:");
                    for (int i = 0; i < d.FixSteps.Length; i++)
                        sb.AppendLine($"    {i + 1}. {d.FixSteps[i]}");
                }
            }

            return sb.ToString();
        }
    }
}
