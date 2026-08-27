using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.Editor
{
    /// <summary>
    /// Parses Excel (.xlsx) workbooks into ConvoCore's internal dialogue configuration format.
    /// Implements <see cref="IConvoCoreSpreadsheetReader"/> using built-in System.IO.Compression
    /// and System.Xml.Linq — no external NuGet dependencies.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreExcelParser.html")]
    public class ConvoCoreExcelParser : IConvoCoreSpreadsheetReader
    {
        // Explicit interface implementation
        bool IConvoCoreSpreadsheetReader.TryRead(
            string absolutePath,
            ConvoCoreSettings settings,
            out Dictionary<string, List<SpreadsheetRowConfig>> result,
            out string error)
            => TryRead(absolutePath, settings, out result, out error);

        // OpenXML namespaces
        private static readonly XNamespace Ns     = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RNs    = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        // Matches ISO 639-1/2 and simple BCP-47 tags: en, fr, zh-CN, etc.
        private static readonly Regex LanguageCodePattern =
            new Regex(@"^[a-zA-Z]{2,5}(-[a-zA-Z]{2,4})?$", RegexOptions.Compiled);

        private class RowData
        {
            public readonly Dictionary<int, string> Values   = new Dictionary<int, string>();
            public readonly HashSet<int>            FormulaCols = new HashSet<int>();
        }

        /// <summary>
        /// Reads the spreadsheet and returns a dictionary mapping conversation key to an ordered
        /// list of <see cref="SpreadsheetRowConfig"/> (each pairing the 1-based xlsx row number
        /// with its dialogue configuration). Returns false and populates error on failure.
        /// </summary>
        public bool TryRead(
            string absolutePath,
            ConvoCoreSettings settings,
            out Dictionary<string, List<SpreadsheetRowConfig>> result,
            out string error)
        {
            result = null;
            error  = null;
            try
            {
                result = new Dictionary<string, List<SpreadsheetRowConfig>>();
                using (var fs  = File.OpenRead(absolutePath))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    var sharedStrings = ReadSharedStrings(zip);
                    var sheetMap      = ReadSheetMap(zip);

                    foreach (var kv in sheetMap)
                    {
                        var sheetName = kv.Key;
                        if (!string.IsNullOrEmpty(settings.ExcelSkipSheetPrefix) &&
                            sheetName.StartsWith(settings.ExcelSkipSheetPrefix, StringComparison.Ordinal))
                            continue;

                        var parsed = ParseSheet(zip, kv.Value, sheetName, sharedStrings, settings, absolutePath);
                        if (parsed != null)
                            result[sheetName] = parsed;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                result = null;
                error  = $"ConvoCore Excel: Failed to read '{Path.GetFileName(absolutePath)}'. {ex.Message} " +
                         $"Ensure the file is a valid .xlsx workbook and is not open in another application.";
                return false;
            }
        }

        // ── Internal helpers ────────────────────────────────────────────────────────

        /// <summary>Returns an insertion-ordered map of sheet name → worksheet entry path.</summary>
        private static Dictionary<string, string> ReadSheetMap(ZipArchive zip)
        {
            var result = new Dictionary<string, string>();

            var wbEntry = zip.GetEntry("xl/workbook.xml");
            if (wbEntry == null) return result;
            XDocument wb;
            using (var s = wbEntry.Open()) wb = XDocument.Load(s);

            var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (relsEntry == null) return result;
            XDocument relsDoc;
            using (var s = relsEntry.Open()) relsDoc = XDocument.Load(s);

            var relMap = relsDoc
                .Descendants(PkgRel + "Relationship")
                .ToDictionary(
                    r => r.Attribute("Id")?.Value     ?? string.Empty,
                    r => r.Attribute("Target")?.Value ?? string.Empty);

            foreach (var sheet in wb.Descendants(Ns + "sheet"))
            {
                var name = sheet.Attribute("name")?.Value;
                var rId  = sheet.Attribute(RNs + "id")?.Value;
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(rId)) continue;
                if (!relMap.TryGetValue(rId, out var target) || string.IsNullOrEmpty(target)) continue;

                // Target is relative to xl/
                var entryPath = target.StartsWith("/") ? target.TrimStart('/') : $"xl/{target}";
                result[name] = entryPath;
            }
            return result;
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var list  = new List<string>();
            var entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return list;
            XDocument doc;
            using (var s = entry.Open()) doc = XDocument.Load(s);
            foreach (var si in doc.Descendants(Ns + "si"))
                list.Add(string.Concat(si.Descendants(Ns + "t").Select(t => t.Value)));
            return list;
        }

        /// <summary>Reads all rows from a worksheet document into a sorted map (1-based row# → RowData).</summary>
        private static SortedList<int, RowData> ReadAllRows(XDocument wsDoc, List<string> sharedStrings)
        {
            var rows = new SortedList<int, RowData>();
            foreach (var rowEl in wsDoc.Descendants(Ns + "row"))
            {
                if (!int.TryParse(rowEl.Attribute("r")?.Value, out var rowNum)) continue;
                var rd = new RowData();
                foreach (var cell in rowEl.Elements(Ns + "c"))
                {
                    var cellRef = cell.Attribute("r")?.Value;
                    if (string.IsNullOrEmpty(cellRef)) continue;
                    var colIdx = CellRefToColIndex(cellRef);
                    if (cell.Element(Ns + "f") != null) rd.FormulaCols.Add(colIdx);
                    rd.Values[colIdx] = GetCellValue(cell, sharedStrings);
                }
                rows[rowNum] = rd;
            }
            return rows;
        }

        private static string GetCellValue(XElement cell, List<string> sharedStrings)
        {
            var t = cell.Attribute("t")?.Value;
            if (t == "s")
            {
                if (int.TryParse(cell.Element(Ns + "v")?.Value, out var idx) && idx >= 0 && idx < sharedStrings.Count)
                    return sharedStrings[idx];
                return string.Empty;
            }
            if (t == "inlineStr")
                return string.Concat(cell.Descendants(Ns + "t").Select(e => e.Value)).Trim();
            if (t == "b")
                return cell.Element(Ns + "v")?.Value == "1" ? "TRUE" : "FALSE";
            if (t == "e")  // error cell e.g. #REF!, #VALUE!
                return string.Empty;
            // "str" (formula result string), numeric, date, or untyped — return <v>
            return cell.Element(Ns + "v")?.Value?.Trim() ?? string.Empty;
        }

        private static List<SpreadsheetRowConfig> ParseSheet(
            ZipArchive zip,
            string entryPath,
            string sheetName,
            List<string> sharedStrings,
            ConvoCoreSettings settings,
            string absolutePath)
        {
            var entry = zip.GetEntry(entryPath);
            if (entry == null)
            {
                Debug.LogError(
                    $"ConvoCore Excel: Worksheet entry '{entryPath}' not found in " +
                    $"'{Path.GetFileName(absolutePath)}'. The workbook may be corrupt.");
                return null;
            }

            var fileName = Path.GetFileName(absolutePath);
            XDocument wsDoc;
            using (var s = entry.Open()) wsDoc = XDocument.Load(s);

            var allRows = ReadAllRows(wsDoc, sharedStrings);
            var rowList = allRows.ToList(); // sorted by xlsx row number

            if (rowList.Count <= settings.ExcelHeaderRowIndex)
            {
                Debug.LogError(
                    $"ConvoCore Excel: Sheet '{sheetName}' in '{fileName}' " +
                    $"does not have a row at header index {settings.ExcelHeaderRowIndex}. " +
                    $"Check ExcelHeaderRowIndex in ConvoCoreSettings > Spreadsheet.");
                return null;
            }

            // Build colIndex → header name from the header row
            var headerValues = rowList[settings.ExcelHeaderRowIndex].Value.Values;
            var colToName    = new Dictionary<int, string>();
            foreach (var kv in headerValues)
                if (!string.IsNullOrEmpty(kv.Value)) colToName[kv.Key] = kv.Value;

            int? charIdCol = null, lineIdCol = null;
            var  langCols      = new Dictionary<int, string>();
            var  unrecognized  = new List<string>();

            foreach (var kv in colToName)
            {
                if (string.Equals(kv.Value, settings.ExcelCharacterIDHeader, StringComparison.OrdinalIgnoreCase))
                    charIdCol = kv.Key;
                else if (string.Equals(kv.Value, settings.ExcelLineIDHeader, StringComparison.OrdinalIgnoreCase))
                    lineIdCol = kv.Key;
                else if (LanguageCodePattern.IsMatch(kv.Value))
                    langCols[kv.Key] = kv.Value;
                else
                    unrecognized.Add(kv.Value);
            }

            if (charIdCol == null)
            {
                Debug.LogError(
                    $"ConvoCore Excel: Sheet '{sheetName}' in '{fileName}' " +
                    $"is missing the required '{settings.ExcelCharacterIDHeader}' column. " +
                    $"Check your header row and confirm the column is spelled exactly as configured in " +
                    $"ConvoCoreSettings > Spreadsheet > Character ID Header.");
                return null;
            }

            if (langCols.Count == 0)
            {
                Debug.LogError(
                    $"ConvoCore Excel: Sheet '{sheetName}' in '{fileName}' " +
                    $"has no language code columns (e.g. 'en', 'fr', 'zh-CN'). " +
                    $"Add at least one language column to the header row.");
                return null;
            }

            if (settings.ExcelWarnOnUnrecognizedColumns)
                foreach (var col in unrecognized)
                    Debug.LogWarning(
                        $"ConvoCore Excel: Sheet '{sheetName}' in '{fileName}' " +
                        $"has unrecognized column header '{col}'. " +
                        $"It is not the CharacterID header, LineID header, or a recognized language code " +
                        $"(2-5 letter ISO code). This column will be ignored during import.");

            // ── Data rows ────────────────────────────────────────────────────────────
            var configs = new List<SpreadsheetRowConfig>();

            for (int i = settings.ExcelHeaderRowIndex + 1; i < rowList.Count; i++)
            {
                var rd               = rowList[i].Value;
                var spreadsheetRowNo = rowList[i].Key; // actual 1-based xlsx row number

                // Formula handling
                if (rd.FormulaCols.Count > 0)
                {
                    switch (settings.ExcelFormulaCellBehavior)
                    {
                        case ExcelFormulaCellBehavior.TreatAsError:
                        {
                            var colName = colToName.TryGetValue(rd.FormulaCols.First(), out var cn)
                                ? cn : ColIndexToLetters(rd.FormulaCols.First());
                            Debug.LogError(
                                $"ConvoCore Excel: Sheet '{sheetName}' in '{fileName}' " +
                                $"contains a formula cell at row {spreadsheetRowNo}, column '{colName}'. " +
                                $"Set ExcelFormulaCellBehavior to UseCachedValue or SkipRow in " +
                                $"ConvoCoreSettings > Spreadsheet, or remove formulas from the spreadsheet.");
                            return null;
                        }
                        case ExcelFormulaCellBehavior.SkipRow:
                        {
                            var colName = colToName.TryGetValue(rd.FormulaCols.First(), out var cn)
                                ? cn : ColIndexToLetters(rd.FormulaCols.First());
                            Debug.LogWarning(
                                $"ConvoCore Excel: Skipping row {spreadsheetRowNo} in sheet '{sheetName}' " +
                                $"in '{fileName}' because column '{colName}' contains a formula. " +
                                $"To suppress this warning, use UseCachedValue in ConvoCoreSettings > Spreadsheet.");
                            continue;
                        }
                        // UseCachedValue: use the <v> cached value — already done
                    }
                }

                if (settings.ExcelSkipEmptyRows && IsRowEmpty(rd.Values)) continue;

                rd.Values.TryGetValue(charIdCol.Value, out var charId);
                if (string.IsNullOrWhiteSpace(charId)) continue;

                string lineId = null;
                if (lineIdCol.HasValue) rd.Values.TryGetValue(lineIdCol.Value, out lineId);

                var localizedDialogue = new Dictionary<string, string>();
                foreach (var lk in langCols)
                {
                    rd.Values.TryGetValue(lk.Key, out var text);
                    localizedDialogue[lk.Value] = text ?? string.Empty;
                }

                configs.Add(new SpreadsheetRowConfig(spreadsheetRowNo, new DialogueYamlConfig
                {
                    CharacterID       = charId,
                    LineID            = string.IsNullOrWhiteSpace(lineId) ? null : lineId,
                    LocalizedDialogue = localizedDialogue
                }));
            }

            return configs;
        }

        private static bool IsRowEmpty(Dictionary<int, string> values)
        {
            foreach (var v in values.Values)
                if (!string.IsNullOrEmpty(v)) return false;
            return true;
        }

        // ── Column index utilities (also used by ConvoCoreExcelWriter) ───────────────

        /// <summary>Converts a cell reference like "A1" or "AA12" to a 0-based column index.</summary>
        public static int CellRefToColIndex(string cellRef)
        {
            int result = 0, i = 0;
            while (i < cellRef.Length && char.IsLetter(cellRef[i]))
                result = result * 26 + (char.ToUpperInvariant(cellRef[i++]) - 'A' + 1);
            return result - 1;
        }

        /// <summary>Converts a 0-based column index to column letters (0 → "A", 26 → "AA").</summary>
        public static string ColIndexToLetters(int colIndex)
        {
            var sb = new StringBuilder();
            for (int n = colIndex + 1; n > 0; n = (n - 1) / 26)
                sb.Insert(0, (char)('A' + (n - 1) % 26));
            return sb.ToString();
        }
    }
}
