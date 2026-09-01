using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    /// <summary>
    /// Entry point for the WitWeaver spreadsheet round-trip pipeline.
    /// Converts an .xlsx file into a fully populated <see cref="WitWeaverConversationData"/> ScriptableObject
    /// by parsing the spreadsheet, generating LineIDs, writing them back to the .xlsx,
    /// serializing to YAML, and calling <see cref="WitWeaverYamlUtilities.ImportFromYamlForKey"/> for each key.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverSpreadsheetUtilities.html")]
    public static class WitWeaverSpreadsheetUtilities
    {
        /// <summary>
        /// Runs the full spreadsheet-to-ScriptableObject pipeline for the given conversation data asset.
        /// </summary>
        /// <param name="target">The WitWeaverConversationData asset to populate.</param>
        /// <param name="spreadsheetAssetPath">Unity asset-relative path to the .xlsx file (e.g. "Assets/Dialogue/Forest.xlsx").</param>
        /// <param name="diagnosticMessage">Human-readable result message (success summary or error).</param>
        /// <returns>True on success, false on any failure.</returns>
        public static bool RunFullPipeline(
            WitWeaverConversationData target,
            string spreadsheetAssetPath,
            out string diagnosticMessage)
        {
            bool ok = RunFullPipelineCore(target, spreadsheetAssetPath, out diagnosticMessage);

            // Record for the inspector so watcher-triggered imports are visible there too
            WitWeaverSpreadsheetImportStatus.Record(target, ok, diagnosticMessage);
            return ok;
        }

        private static bool RunFullPipelineCore(
            WitWeaverConversationData target,
            string spreadsheetAssetPath,
            out string diagnosticMessage)
        {
            // Step 1: Load settings
            var settings = WitWeaverSettings.Instance;
            if (settings == null)
            {
                diagnosticMessage =
                    "WitWeaver Spreadsheet: Could not load WitWeaverSettings. " +
                    "Create one via Assets > Create > WitWeaver > Settings.";
                return false;
            }

            // Step 2: Resolve absolute path
            var absolutePath = Path.GetFullPath(spreadsheetAssetPath);
            var fileName = Path.GetFileName(absolutePath);

            // Step 3: Parse the spreadsheet — returns SpreadsheetRowConfig (row number + DialogueYamlConfig)
            var parser = new WitWeaverSpreadsheetParser();
            if (!parser.TryRead(absolutePath, settings, out var rowConfigDict, out var parseError))
            {
                diagnosticMessage = parseError;
                return false;
            }

            if (rowConfigDict == null || rowConfigDict.Count == 0)
            {
                diagnosticMessage =
                    $"WitWeaver Spreadsheet: No conversation data found in '{fileName}'. " +
                    $"Ensure sheet tab names correspond to ConversationKeys and that each sheet has " +
                    $"a '{settings.SpreadsheetCharacterIDHeader}' column and at least one language code column.";
                return false;
            }

            // Step 4: Build a plain config dict (for EnsureLineIds and YAML generation)
            // Config objects are shared references — mutations by EnsureLineIds are visible in rowConfigDict too
            var configDict = rowConfigDict.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ConvertAll(src => src.Config));

            // Step 5: Ensure LineIDs
            bool idsGenerated = WitWeaverLineIDUtility.EnsureLineIds(configDict, out var idError);
            if (idError != null)
            {
                diagnosticMessage =
                    $"WitWeaver Spreadsheet: LineID validation failed in '{fileName}'. {idError}";
                return false;
            }

            // Step 6: Write back LineIDs if any were generated
            bool writebackWarned = false;
            if (idsGenerated)
            {
                if (!WitWeaverSpreadsheetWriter.TryWriteLineIDs(absolutePath, settings, rowConfigDict, out var writeError))
                {
                    Debug.LogWarning(
                        $"WitWeaver Spreadsheet: LineIDs were generated but could not be written back to '{fileName}'. " +
                        $"{writeError} — The file is now out of sync. " +
                        $"Save the .xlsx again to trigger a fresh import.");
                    writebackWarned = true;
                }
                else
                {
                    // Reimport the xlsx so Unity picks up the LineID changes
                    AssetDatabase.ImportAsset(spreadsheetAssetPath, ImportAssetOptions.Default);
                }
            }

            // WitWeaverYamlSerializer (YamlDotNet) may produce single-quoted or folded scalars for
            // strings that start with '*', '...', or are very long. Those are then corrupted by
            // WitWeaverYamlParser.EnsureQuotesOnLocalizedValues (a \s* backtracking bug causes the
            // space before a single-quote to be captured). By generating YAML with all localized
            // values pre-double-quoted on a single line we sidestep the issue entirely.
            var yamlText = BuildSafeYaml(configDict);

            // Validate that the generated YAML round-trips through the parser before embedding it.
            if (!WitWeaverYamlParser.TryParse(yamlText, out _, out string yamlValidationError))
            {
                diagnosticMessage =
                    $"WitWeaver Spreadsheet: Internal YAML generation error for '{fileName}'. " +
                    $"The generated YAML could not be parsed: {yamlValidationError} — " +
                    $"Please report this as a bug with your spreadsheet content.";
                return false;
            }

            // Steps 8-10: Replace the embedded sub-asset via the shared helper (also records
            // provenance and defaults the persistent-override stem)
            WitWeaverEmbedUtility.ReplaceEmbeddedYaml(target, yamlText, spreadsheetAssetPath);

            // Step 11: ImportFromYamlForKey for each conversation key. Write-back is suppressed:
            // this data came from the spreadsheet, so generated LineIDs must never overwrite a
            // linked YAML source file (they were already written back to the .xlsx in step 6).
            var utils = new WitWeaverYamlUtilities(target);
            foreach (var key in configDict.Keys)
                utils.ImportFromYamlForKey(key, suppressSourceWriteBack: true);

            // Known limitation: each key's import wholesale-replaces DialogueLines, so with a
            // multi-sheet workbook only the last sheet's lines are retained on this asset.
            string multiSheetNote = null;
            if (configDict.Count > 1)
            {
                var lastKey = configDict.Keys.Last();
                multiSheetNote =
                    $"Workbook has {configDict.Count} sheets; only the last imported key " +
                    $"('{lastKey}') is retained as this asset's dialogue lines.";
                Debug.LogWarning($"WitWeaver Spreadsheet: {multiSheetNote}", target);
            }

            // Step 12: Mark dirty and save
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            // Step 13: Build success message
            int totalLines = 0;
            foreach (var kv in configDict)
                totalLines += kv.Value?.Count ?? 0;

            var idInfo = idsGenerated
                ? (writebackWarned ? " (LineIDs generated but NOT written back to .xlsx — see console)" : " (LineIDs generated and written back to .xlsx)")
                : "";

            diagnosticMessage =
                $"WitWeaver Spreadsheet: Import successful from '{fileName}'. " +
                $"{configDict.Count} conversation key(s), {totalLines} total line(s){idInfo}.";

            if (multiSheetNote != null)
                diagnosticMessage += $" {multiSheetNote}";

            return true;
        }

        // ── YAML generation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Generates YAML that is safe to round-trip through <see cref="WitWeaverYamlParser.Parse"/>.
        ///
        /// <c>WitWeaverYamlParser.EnsureQuotesOnLocalizedValues</c> has a <c>\s*</c> backtracking
        /// bug: when a value starts with <c>"</c> or <c>'</c>, the regex gives up the leading
        /// space and instead captures it inside group 3, then double-wraps the already-quoted value —
        /// producing invalid YAML. The root cause cannot be fixed here without modifying the parser.
        ///
        /// The fix: emit <c>LocalizedDialogue</c> as a YAML flow-style mapping (all on one line).
        /// The language codes (<c>en:</c>, <c>fr:</c>, etc.) then appear in the middle of the line,
        /// never at line-start, so the Multiline <c>^([a-z]{2,3}):</c> regex cannot match them at all.
        /// </summary>
        private static string BuildSafeYaml(Dictionary<string, List<DialogueYamlConfig>> dict)
        {
            var sb = new StringBuilder();
            foreach (var kv in dict)
            {
                sb.AppendLine($"{kv.Key}:");
                foreach (var cfg in kv.Value)
                {
                    sb.AppendLine($"- CharacterID: {YamlPlainOrQuote(cfg.CharacterID)}");
                    sb.AppendLine($"  LineID: {YamlPlainOrQuote(cfg.LineID)}");

                    // Flow-style mapping: {en: "...", fr: "...", es: "..."}
                    // Language codes are never at line-start so EnsureQuotesOnLocalizedValues skips them.
                    // Inside double-quoted scalars, } and , are safe literal characters.
                    if (cfg.LocalizedDialogue != null && cfg.LocalizedDialogue.Count > 0)
                    {
                        var entries = string.Join(", ", cfg.LocalizedDialogue
                            .Select(lang => $"{lang.Key}: \"{EscapeDoubleQuoted(lang.Value)}\""));
                        sb.AppendLine($"  LocalizedDialogue: {{{entries}}}");
                    }
                    else
                    {
                        sb.AppendLine("  LocalizedDialogue: {}");
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns the value as a plain YAML scalar if it is safe to do so,
        /// otherwise wraps it in double quotes. Used for CharacterID and LineID.
        /// </summary>
        private static string YamlPlainOrQuote(string value)
        {
            if (string.IsNullOrEmpty(value)) return "~";
            // Plain scalars are safe when they contain only word chars, hyphens, and dots
            foreach (char c in value)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                    return $"\"{EscapeDoubleQuoted(value)}\"";
            }
            return value;
        }

        /// <summary>
        /// Escapes a string for use inside a YAML double-quoted scalar.
        /// Handles backslash, double-quote, and all newline variants (spreadsheet cells can contain
        /// real newlines via Alt+Enter; a literal newline inside a YAML double-quoted scalar
        /// would break the line structure and cause a parse error).
        /// </summary>
        private static string EscapeDoubleQuoted(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace("\\",  "\\\\")   // must be first
                .Replace("\"",  "\\\"")
                .Replace("\r\n","\\n")     // Windows CRLF before individual \r/\n
                .Replace("\n",  "\\n")
                .Replace("\r",  "\\n");
        }
    }
}