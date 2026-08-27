using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.Editor
{
    /// <summary>
    /// Entry point for the ConvoCore Excel round-trip pipeline.
    /// Converts an .xlsx file into a fully populated <see cref="ConvoCoreConversationData"/> ScriptableObject
    /// by parsing the spreadsheet, generating LineIDs, writing them back to the .xlsx,
    /// serializing to YAML, and calling <see cref="ConvoCoreYamlUtilities.ImportFromYamlForKey"/> for each key.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreExcelUtilities.html")]
    public static class ConvoCoreExcelUtilities
    {
        /// <summary>
        /// Runs the full Excel-to-ScriptableObject pipeline for the given conversation data asset.
        /// </summary>
        /// <param name="target">The ConvoCoreConversationData asset to populate.</param>
        /// <param name="excelAssetPath">Unity asset-relative path to the .xlsx file (e.g. "Assets/Dialogue/Forest.xlsx").</param>
        /// <param name="diagnosticMessage">Human-readable result message (success summary or error).</param>
        /// <returns>True on success, false on any failure.</returns>
        public static bool RunFullPipeline(
            ConvoCoreConversationData target,
            string excelAssetPath,
            out string diagnosticMessage)
        {
            // Step 1: Load settings
            var settings = ConvoCoreSettings.Instance;
            if (settings == null)
            {
                diagnosticMessage =
                    "ConvoCore Excel: Could not load ConvoCoreSettings. " +
                    "Create one via Assets > Create > ConvoCore > Settings.";
                return false;
            }

            // Step 2: Resolve absolute path
            var absolutePath = Path.GetFullPath(excelAssetPath);
            var fileName = Path.GetFileName(absolutePath);

            // Step 3: Parse Excel — returns SpreadsheetRowConfig (row number + DialogueYamlConfig)
            var parser = new ConvoCoreExcelParser();
            if (!parser.TryRead(absolutePath, settings, out var rowConfigDict, out var parseError))
            {
                diagnosticMessage = parseError;
                return false;
            }

            if (rowConfigDict == null || rowConfigDict.Count == 0)
            {
                diagnosticMessage =
                    $"ConvoCore Excel: No conversation data found in '{fileName}'. " +
                    $"Ensure sheet tab names correspond to ConversationKeys and that each sheet has " +
                    $"a '{settings.ExcelCharacterIDHeader}' column and at least one language code column.";
                return false;
            }

            // Step 4: Build a plain config dict (for EnsureLineIds and YAML generation)
            // Config objects are shared references — mutations by EnsureLineIds are visible in rowConfigDict too
            var configDict = rowConfigDict.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ConvertAll(src => src.Config));

            // Step 5: Ensure LineIDs
            bool idsGenerated = ConvoCoreLineIDUtility.EnsureLineIds(configDict, out var idError);
            if (idError != null)
            {
                diagnosticMessage =
                    $"ConvoCore Excel: LineID validation failed in '{fileName}'. {idError}";
                return false;
            }

            // Step 6: Write back LineIDs if any were generated
            bool writebackWarned = false;
            if (idsGenerated)
            {
                if (!ConvoCoreExcelWriter.TryWriteLineIDs(absolutePath, settings, rowConfigDict, out var writeError))
                {
                    Debug.LogWarning(
                        $"ConvoCore Excel: LineIDs were generated but could not be written back to '{fileName}'. " +
                        $"{writeError} — The file is now out of sync. " +
                        $"Save the .xlsx again to trigger a fresh import.");
                    writebackWarned = true;
                }
                else
                {
                    // Reimport the xlsx so Unity picks up the LineID changes
                    AssetDatabase.ImportAsset(excelAssetPath, ImportAssetOptions.Default);
                }
            }

            // ConvoCoreYamlSerializer (YamlDotNet) may produce single-quoted or folded scalars for
            // strings that start with '*', '...', or are very long. Those are then corrupted by
            // ConvoCoreYamlParser.EnsureQuotesOnLocalizedValues (a \s* backtracking bug causes the
            // space before a single-quote to be captured). By generating YAML with all localized
            // values pre-double-quoted on a single line we sidestep the issue entirely.
            var yamlText = BuildSafeYaml(configDict);

            // Validate that the generated YAML round-trips through the parser before embedding it.
            if (!ConvoCoreYamlParser.TryParse(yamlText, out _, out string yamlValidationError))
            {
                diagnosticMessage =
                    $"ConvoCore Excel: Internal YAML generation error for '{fileName}'. " +
                    $"The generated YAML could not be parsed: {yamlValidationError} — " +
                    $"Please report this as a bug with your spreadsheet content.";
                return false;
            }

            // Step 8: Remove existing "EmbeddedYaml" subasset
            var convoAssetPath = AssetDatabase.GetAssetPath(target);
            var representations = AssetDatabase.LoadAllAssetRepresentationsAtPath(convoAssetPath);
            if (representations != null)
            {
                foreach (var rep in representations)
                {
                    if (rep is TextAsset { name: "EmbeddedYaml" } existing)
                        UnityEngine.Object.DestroyImmediate(existing, true);
                }
            }

            // Step 9: Create new embedded subasset and assign
            var embedded = new TextAsset(yamlText) { name = "EmbeddedYaml" };
            AssetDatabase.AddObjectToAsset(embedded, target);
            target.ConversationYaml = embedded;

            // Step 10: Set default FilePath if empty
            if (string.IsNullOrEmpty(target.FilePath))
            {
                var baseName = Path.GetFileNameWithoutExtension(excelAssetPath);
                target.FilePath = $"ConvoCore/Dialogue/{baseName}";
            }

            // Step 11: ImportFromYamlForKey for each conversation key
            var utils = new ConvoCoreYamlUtilities(target);
            foreach (var key in configDict.Keys)
                utils.ImportFromYamlForKey(key);

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
                $"ConvoCore Excel: Import successful from '{fileName}'. " +
                $"{configDict.Count} conversation key(s), {totalLines} total line(s){idInfo}.";

            return true;
        }

        // ── YAML generation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Generates YAML that is safe to round-trip through <see cref="ConvoCoreYamlParser.Parse"/>.
        ///
        /// <c>ConvoCoreYamlParser.EnsureQuotesOnLocalizedValues</c> has a <c>\s*</c> backtracking
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
        /// Handles backslash, double-quote, and all newline variants (Excel cells can contain
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