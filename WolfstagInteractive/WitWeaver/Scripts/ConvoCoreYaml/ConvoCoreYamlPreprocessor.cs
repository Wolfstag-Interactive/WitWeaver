using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Result returned by <see cref="ConvoCoreYamlPreprocessor.Run"/>.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1PreprocessorResult.html")]
    public sealed class PreprocessorResult
    {
        /// <summary>
        /// True when no Error-level diagnostics were found. Only when <c>true</c> is
        /// <see cref="ProcessedYaml"/> safe to pass to YamlDotNet.
        /// </summary>
        public bool Success { get; internal set; }

        /// <summary>
        /// The transformed YAML text ready for deserialization.
        /// <c>null</c> when <see cref="Success"/> is <c>false</c>.
        /// </summary>
        public string ProcessedYaml { get; internal set; }

        /// <summary>All diagnostics collected during pre-flight and the state machine pass.</summary>
        public IReadOnlyList<ConvoCoreYamlDiagnostic> Diagnostics => _diagnostics;
        internal readonly List<ConvoCoreYamlDiagnostic> _diagnostics = new List<ConvoCoreYamlDiagnostic>();

        /// <summary>
        /// Maps 0-indexed line numbers to the number of characters added to that line
        /// by the escape/wrapping transform. Used by the parser to correct column
        /// numbers reported by YamlDotNet back to positions in the original file.
        /// </summary>
        public IReadOnlyDictionary<int, int> ColumnOffsets => _columnOffsets;
        internal readonly Dictionary<int, int> _columnOffsets = new Dictionary<int, int>();

        internal bool HasErrors => _diagnostics.Exists(d => d.Severity == DiagnosticSeverity.Error);
        internal void Add(ConvoCoreYamlDiagnostic d) => _diagnostics.Add(d);
    }

    /// <summary>
    /// Three-phase YAML preprocessor that replaces the old regex-based
    /// <c>EnsureQuotesOnLocalizedValues</c>:
    /// <list type="number">
    ///   <item><description>
    ///     <b>Pre-flight</b> — scans the raw text for file-level problems (BOM, git
    ///     conflict markers, multiple documents, tab indentation, smart quotes,
    ///     zero-width characters) before any transforms are applied.
    ///   </description></item>
    ///   <item><description>
    ///     <b>State machine transform</b> — walks the file line-by-line, enters
    ///     <c>LocalizedDialogue</c> blocks by key name, and applies a safe escape
    ///     pipeline only to locale value lines. All other lines pass through
    ///     unchanged.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Column offset recording</b> — for every transformed line, records how
    ///     many characters were added so that YamlDotNet's error columns can be
    ///     corrected back to the original file coordinates.
    ///   </description></item>
    /// </list>
    /// </summary>
    public static class ConvoCoreYamlPreprocessor
    {
        // BCP 47 locale code: 2-3 alpha, optional subtag(s) (e.g. zh-CN, pt-BR, en).
        private static readonly Regex LocaleCodeRegex = new Regex(
            @"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Block scalar indicators that must not be wrapped in quotes.
        private static readonly HashSet<string> BlockScalars =
            new HashSet<string> { "|", ">", "|-", ">-", "|+", ">+" };

        // Smart/curly quote characters that are visually similar to ASCII quotes.
        private const string SmartQuoteChars = "“”‘’";

        // Zero-width/invisible characters that can corrupt key matching.
        private const string ZeroWidthChars = "​‌‍";

        // ───────────────────────────────────────────────────────────────────────────
        //  Public entry point
        // ───────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Runs the full three-phase pipeline against <paramref name="originalYaml"/>.
        /// Always returns a populated <see cref="PreprocessorResult"/>; check
        /// <see cref="PreprocessorResult.Success"/> before using
        /// <see cref="PreprocessorResult.ProcessedYaml"/>.
        /// </summary>
        public static PreprocessorResult Run(string originalYaml)
        {
            var result = new PreprocessorResult();

            if (string.IsNullOrEmpty(originalYaml))
            {
                result.Success       = true;
                result.ProcessedYaml = originalYaml ?? string.Empty;
                return result;
            }

            // Normalise line endings; keep original so we can show authors their exact text.
            string[] lines = originalYaml.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            RunPreFlight(lines, result);

            // If hard errors already found, skip the transform — the YAML is not safe
            // to process and the author needs to fix these first.
            if (result.HasErrors)
            {
                result.Success       = false;
                result.ProcessedYaml = null;
                return result;
            }

            string processed = RunStateMachine(lines, result);

            result.Success       = !result.HasErrors;
            result.ProcessedYaml = result.Success ? processed : null;
            return result;
        }

        // ───────────────────────────────────────────────────────────────────────────
        //  Phase A: Pre-flight checks  (read-only, no transforms)
        // ───────────────────────────────────────────────────────────────────────────

        private static void RunPreFlight(string[] lines, PreprocessorResult result)
        {
            // BOM at the very start of the file.
            if (lines.Length > 0 && lines[0].Length > 0 && lines[0][0] == '﻿')
            {
                result.Add(new ConvoCoreYamlDiagnostic(
                    DiagnosticSeverity.Error, 1, 1, lines[0],
                    "BOM",
                    "File starts with a UTF-8 BOM (byte-order mark), which is not valid in YAML.",
                    "Re-save the file as UTF-8 without BOM in your text editor or IDE.",
                    "In VS Code: click the 'UTF-8 with BOM' status-bar item → Save with Encoding → UTF-8."));
            }

            bool seenDocSeparator = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line    = lines[i];
                int    lineNum = i + 1; // 1-indexed for display
                string trimmed = line.TrimStart();

                // ── Tab indentation ────────────────────────────────────────────────
                if (line.Length > 0 && line[0] == '\t')
                {
                    result.Add(new ConvoCoreYamlDiagnostic(
                        DiagnosticSeverity.Error, lineNum, 1, line,
                        "TAB_INDENT",
                        "YAML requires spaces for indentation; tab characters are not allowed.",
                        "Replace the leading tab(s) with spaces.",
                        "Most editors have an option to convert tabs to spaces on save."));
                }

                // ── Git merge conflict markers ─────────────────────────────────────
                if (trimmed.StartsWith("<<<<<<<") || trimmed.StartsWith(">>>>>>>") ||
                    trimmed == "=======")
                {
                    result.Add(new ConvoCoreYamlDiagnostic(
                        DiagnosticSeverity.Error, lineNum, 0, line,
                        "CONFLICT",
                        "Unresolved git merge conflict marker found. The file cannot be parsed until the conflict is resolved.",
                        "Open the file in your merge tool and accept/reject the conflicting changes.",
                        "Delete all conflict marker lines (<<<<<<<, =======, >>>>>>>) and keep only the desired content."));
                }

                // ── Multiple YAML documents ────────────────────────────────────────
                if (trimmed == "---")
                {
                    if (!seenDocSeparator)
                    {
                        seenDocSeparator = true; // first '---' is the document start marker — fine
                    }
                    else
                    {
                        result.Add(new ConvoCoreYamlDiagnostic(
                            DiagnosticSeverity.Error, lineNum, 0, line,
                            "MULTI_DOC",
                            "A second '---' document separator was found. The parser only reads the first YAML document; all conversations after this line will be silently ignored.",
                            "Split this file into separate YAML files, one per document.",
                            "Remove the '---' separator and merge the conversations into a single document."));
                    }
                }

                // ── Smart / curly quotes (warning — import can still proceed) ──────
                foreach (char c in SmartQuoteChars)
                {
                    int idx = line.IndexOf(c);
                    if (idx >= 0)
                    {
                        char replacement = (c == '‘' || c == '’') ? '\'' : '"';
                        result.Add(new ConvoCoreYamlDiagnostic(
                            DiagnosticSeverity.Warning, lineNum, idx + 1, line,
                            "SMART_QUOTE",
                            $"Curly/smart quote character (U+{(int)c:X4} '{c}') found. " +
                            "This is not a YAML quote character and will appear as a literal character in the dialogue string.",
                            $"Replace the curly quote with a straight {(replacement == '\'' ? "apostrophe (')" : "double-quote (\")") }."));
                        break; // one warning per line is enough
                    }
                }

                // ── Zero-width / invisible characters (warning) ────────────────────
                foreach (char c in ZeroWidthChars)
                {
                    int idx = line.IndexOf(c);
                    if (idx >= 0)
                    {
                        result.Add(new ConvoCoreYamlDiagnostic(
                            DiagnosticSeverity.Warning, lineNum, idx + 1, line,
                            "ZERO_WIDTH",
                            $"Invisible zero-width character (U+{(int)c:X4}) found at column {idx + 1}. " +
                            "This can corrupt key matching and cause unpredictable parse behaviour.",
                            "Delete the invisible character. It may have been introduced by copy-pasting from a web page or word processor.",
                            "Enable 'Show Whitespace' or 'Show Special Characters' in your editor to locate it."));
                        break;
                    }
                }
            }
        }

        // ───────────────────────────────────────────────────────────────────────────
        //  Phase B: State machine transform
        // ───────────────────────────────────────────────────────────────────────────

        private static string RunStateMachine(string[] lines, PreprocessorResult result)
        {
            var output = new StringBuilder(capacity: lines.Length * 40);

            bool inLocalizedDialogue    = false;
            int  localizedDialogueIndent = -1;  // indent of the "LocalizedDialogue:" key
            bool flowStyleBlock         = false; // true when the LD value used flow {..}
            var  seenLocales            = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                string line    = lines[i];
                int    lineNum = i + 1;

                // ── Determine indent of this line ──────────────────────────────────
                int currentIndent = CountLeadingSpaces(line);
                string trimmed    = line.TrimStart();

                // ── Exit LocalizedDialogue state when indent drops ─────────────────
                if (inLocalizedDialogue && !string.IsNullOrWhiteSpace(trimmed))
                {
                    bool isComment = trimmed.StartsWith("#");
                    if (!isComment && currentIndent <= localizedDialogueIndent)
                    {
                        inLocalizedDialogue     = false;
                        localizedDialogueIndent = -1;
                        flowStyleBlock          = false;
                        seenLocales.Clear();
                    }
                }

                // ── Detect LocalizedDialogue key ───────────────────────────────────
                if (!inLocalizedDialogue && IsLocalizedDialogueKey(trimmed, out bool isFlowStyle))
                {
                    localizedDialogueIndent = currentIndent;
                    flowStyleBlock          = isFlowStyle;
                    inLocalizedDialogue     = !isFlowStyle; // don't enter state for flow-style
                    seenLocales.Clear();
                    // Pass the key line through unchanged regardless.
                    AppendLine(output, line, i, lines);
                    continue;
                }

                // ── Process lines inside a block-style LocalizedDialogue ───────────
                if (inLocalizedDialogue && !flowStyleBlock)
                {
                    // Blank lines — pass through.
                    if (string.IsNullOrWhiteSpace(trimmed))
                    {
                        AppendLine(output, line, i, lines);
                        continue;
                    }

                    // Comment lines — never quote; pass through.
                    if (trimmed.StartsWith("#"))
                    {
                        AppendLine(output, line, i, lines);
                        continue;
                    }

                    // Parse as  <localeCode>: <value>
                    if (!TryParseKeyValue(trimmed, out string localeKey, out string rawValue))
                    {
                        // Doesn't look like a key-value pair; pass through and let YamlDotNet handle it.
                        AppendLine(output, line, i, lines);
                        continue;
                    }

                    // Key must match a BCP-47 locale code pattern.
                    if (!LocaleCodeRegex.IsMatch(localeKey))
                    {
                        AppendLine(output, line, i, lines);
                        continue;
                    }

                    // ── Duplicate locale detection ─────────────────────────────────
                    string lowerKey = localeKey.ToLowerInvariant();
                    if (!seenLocales.Add(lowerKey))
                    {
                        result.Add(new ConvoCoreYamlDiagnostic(
                            DiagnosticSeverity.Warning, lineNum, 0, line,
                            "DUPLICATE_LOCALE",
                            $"Locale key '{localeKey}' appears more than once in this LocalizedDialogue block. " +
                            "Only the last value will be used; earlier values are silently discarded.",
                            $"Remove the duplicate '{localeKey}' entry and keep only the intended line."));
                    }

                    // ── Skip lines that need no transform ──────────────────────────

                    // Null / empty values — preserve YAML semantics.
                    if (string.IsNullOrEmpty(rawValue) ||
                        rawValue == "~"    ||
                        rawValue == "null" ||
                        rawValue == "Null" ||
                        rawValue == "NULL")
                    {
                        AppendLine(output, line, i, lines);
                        continue;
                    }

                    // Block scalar indicators — warn and pass through.
                    if (BlockScalars.Contains(rawValue.Trim()))
                    {
                        result.Add(new ConvoCoreYamlDiagnostic(
                            DiagnosticSeverity.Warning, lineNum, 0, line,
                            "BLOCK_SCALAR",
                            $"The locale value for '{localeKey}' uses a YAML block scalar indicator ('{rawValue.Trim()}'). " +
                            "Block scalars inside LocalizedDialogue may produce unexpected whitespace or newlines at runtime.",
                            $"Replace the block scalar with a single-quoted string:  {localeKey}: 'your text here'"));
                        AppendLine(output, line, i, lines);
                        continue;
                    }

                    // Already validly double-quoted — leave untouched.
                    if (IsValidlyDoubleQuoted(rawValue))
                    {
                        AppendLine(output, line, i, lines);
                        continue;
                    }

                    // Already validly single-quoted — leave untouched.
                    if (IsValidlySingleQuoted(rawValue))
                    {
                        AppendLine(output, line, i, lines);
                        continue;
                    }

                    // ── Apply escape pipeline and wrap in double quotes ────────────
                    //   Order is critical:
                    //     1. Escape existing backslashes first  (\  →  \\)
                    //     2. Then escape any double-quotes      ("  →  \")
                    //     3. Wrap result in outer double quotes
                    string escaped = rawValue
                        .Replace("\\", "\\\\")
                        .Replace("\"", "\\\"");

                    // Reconstruct the line preserving original leading whitespace.
                    string prefix      = line.Substring(0, currentIndent);
                    string transformed = $"{prefix}{localeKey}: \"{escaped}\"";

                    // Record column offset: characters added = length difference.
                    int offset = transformed.Length - line.Length;
                    if (offset != 0)
                        result._columnOffsets[i] = offset;

                    AppendLine(output, transformed, i, lines);
                    continue;
                }

                // ── Default: pass line through unchanged ───────────────────────────
                AppendLine(output, line, i, lines);
            }

            return output.ToString();
        }

        // ───────────────────────────────────────────────────────────────────────────
        //  Helpers
        // ───────────────────────────────────────────────────────────────────────────

        private static void AppendLine(StringBuilder sb, string line, int index, string[] allLines)
        {
            sb.Append(line);
            if (index < allLines.Length - 1)
                sb.Append('\n');
        }

        private static int CountLeadingSpaces(string line)
        {
            int count = 0;
            while (count < line.Length && line[count] == ' ')
                count++;
            return count;
        }

        /// <summary>
        /// Returns true if <paramref name="trimmed"/> is a <c>LocalizedDialogue:</c> key line.
        /// Sets <paramref name="isFlowStyle"/> when the value opens with <c>{</c>.
        /// </summary>
        private static bool IsLocalizedDialogueKey(string trimmed, out bool isFlowStyle)
        {
            isFlowStyle = false;
            if (trimmed == "LocalizedDialogue:")
                return true;
            if (trimmed.StartsWith("LocalizedDialogue:"))
            {
                string rest = trimmed.Substring("LocalizedDialogue:".Length).TrimStart();
                if (rest.StartsWith("{"))
                    isFlowStyle = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Splits a trimmed line into its key and raw value.
        /// Returns false if no colon is found.
        /// </summary>
        private static bool TryParseKeyValue(string trimmed, out string key, out string rawValue)
        {
            int colonIdx = trimmed.IndexOf(':');
            if (colonIdx < 0)
            {
                key = rawValue = null;
                return false;
            }
            key = trimmed.Substring(0, colonIdx);
            string afterColon = trimmed.Substring(colonIdx + 1);
            // Strip exactly one leading space after the colon (YAML convention).
            rawValue = afterColon.Length > 0 && afterColon[0] == ' '
                ? afterColon.Substring(1)
                : afterColon;
            return true;
        }

        /// <summary>
        /// Returns true if <paramref name="value"/> is already wrapped in correctly
        /// escaped double quotes, meaning no transform is needed.
        /// </summary>
        private static bool IsValidlyDoubleQuoted(string value)
        {
            if (value.Length < 2 || value[0] != '"' || value[value.Length - 1] != '"')
                return false;

            // Walk the interior; check for any unescaped '"' that would close the string early.
            bool backslashed = false;
            for (int i = 1; i < value.Length - 1; i++)
            {
                if (backslashed) { backslashed = false; continue; }
                if (value[i] == '\\') { backslashed = true;  continue; }
                if (value[i] == '"')  return false; // unescaped internal quote
            }
            return true;
        }

        /// <summary>
        /// Returns true if <paramref name="value"/> is a correctly formed single-quoted
        /// YAML string (no lone apostrophes — only <c>''</c> escape pairs).
        /// </summary>
        private static bool IsValidlySingleQuoted(string value)
        {
            if (value.Length < 2 || value[0] != '\'' || value[value.Length - 1] != '\'')
                return false;
            string inner = value.Substring(1, value.Length - 2);
            // Replace all valid '' escape pairs; any remaining ' is a bare apostrophe.
            return !inner.Replace("''", string.Empty).Contains('\'');
        }
    }
}
