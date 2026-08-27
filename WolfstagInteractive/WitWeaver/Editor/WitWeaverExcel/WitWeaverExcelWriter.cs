using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace WolfstagInteractive.ConvoCore.Editor
{
    /// <summary>
    /// Writes generated LineID values back into the source .xlsx file after
    /// <see cref="ConvoCoreLineIDUtility.EnsureLineIds"/> populates them.
    /// All sheets (including non-dialogue sheets like _README) are preserved.
    /// Uses System.IO.Compression and System.Xml.Linq — no external NuGet dependencies.
    ///
    /// Each <see cref="SpreadsheetRowConfig"/> carries the 1-based xlsx row number stamped
    /// by <see cref="ConvoCoreExcelParser"/> at parse time, so writeback is keyed directly
    /// by row number — no skip-logic duplication, no index drift.
    /// </summary>
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreExcelWriter.html")]
    public static class ConvoCoreExcelWriter
    {
        private static readonly XNamespace Ns     = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RNs    = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        /// <summary>
        /// Writes LineID values from <paramref name="data"/> back into the .xlsx at
        /// <paramref name="absolutePath"/>. All other sheets and content are preserved.
        /// Each entry's <see cref="SpreadsheetRowConfig.XlRowNumber"/> is used directly —
        /// no row re-derivation or skip-logic that could cause index drift.
        /// </summary>
        public static bool TryWriteLineIDs(
            string absolutePath,
            ConvoCoreSettings settings,
            Dictionary<string, List<SpreadsheetRowConfig>> data,
            out string error)
        {
            error = null;
            var fileName = Path.GetFileName(absolutePath);

            try
            {
                // Read all zip entries into memory so the file can be closed before we overwrite it
                var entries      = ReadAllZipBytes(absolutePath);
                var modifiedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var sharedStrings = ReadSharedStringsFromEntries(entries);
                var sheetMap      = ReadSheetMapFromEntries(entries);

                foreach (var kv in data)
                {
                    var sheetName  = kv.Key;
                    var rowConfigs = kv.Value;
                    if (rowConfigs == null || rowConfigs.Count == 0) continue;
                    if (!sheetMap.TryGetValue(sheetName, out var entryPath)) continue;
                    if (!entries.ContainsKey(entryPath)) continue;

                    var updated = UpdateWorksheetLineIds(
                        entries[entryPath], sharedStrings, sheetName, settings, rowConfigs, fileName);
                    if (updated != null)
                    {
                        entries[entryPath] = updated;
                        modifiedKeys.Add(entryPath);
                    }
                }

                WriteAllZipBytes(absolutePath, entries, modifiedKeys);
                return true;
            }
            catch (Exception ex)
            {
                error = $"ConvoCore Excel: Failed to write LineIDs back to '{fileName}'. {ex.Message} — " +
                        $"The file may be open in another application or marked read-only.";
                return false;
            }
        }

        // ── ZIP helpers ────────────────────────────────────────────────────────────

        private static Dictionary<string, byte[]> ReadAllZipBytes(string absolutePath)
        {
            var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            using (var fs  = File.OpenRead(absolutePath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    using (var ms = new MemoryStream())
                    using (var es = entry.Open())
                    {
                        es.CopyTo(ms);
                        result[entry.FullName] = ms.ToArray();
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Atomically replaces <paramref name="absolutePath"/> with new zip content.
        /// <see cref="File.Replace"/> keeps the original safe until the new file is fully written —
        /// no data-loss window if the move step fails mid-way.
        /// Entries listed in <paramref name="modifiedKeys"/> are recompressed at Optimal level;
        /// unmodified entries use Fastest so unchanged sheets aren't needlessly CPU-heavy.
        /// </summary>
        private static void WriteAllZipBytes(
            string absolutePath,
            Dictionary<string, byte[]> entries,
            HashSet<string> modifiedKeys)
        {
            var tempPath   = absolutePath + ".tmp";
            var backupPath = absolutePath + ".bak";

            using (var fs  = File.Create(tempPath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var kv in entries)
                {
                    var level = modifiedKeys.Contains(kv.Key)
                        ? CompressionLevel.Optimal
                        : CompressionLevel.Fastest;
                    var e = zip.CreateEntry(kv.Key, level);
                    using (var es = e.Open())
                        es.Write(kv.Value, 0, kv.Value.Length);
                }
            }

            // File.Replace: atomically swaps temp → original, placing original → backup.
            // The backup is a safety net for the Replace call itself; remove it immediately after.
            File.Replace(tempPath, absolutePath, backupPath);
            try { File.Delete(backupPath); } catch { /* non-critical — stale .bak is harmless */ }
        }

        // ── Metadata readers ────────────────────────────────────────────────────────

        private static Dictionary<string, string> ReadSheetMapFromEntries(Dictionary<string, byte[]> entries)
        {
            var result = new Dictionary<string, string>();
            if (!entries.TryGetValue("xl/workbook.xml",           out var wbBytes))    return result;
            if (!entries.TryGetValue("xl/_rels/workbook.xml.rels", out var relsBytes)) return result;

            XDocument wb, relsDoc;
            using (var ms = new MemoryStream(wbBytes))    wb      = XDocument.Load(ms);
            using (var ms = new MemoryStream(relsBytes))  relsDoc = XDocument.Load(ms);

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
                var entryPath = target.StartsWith("/") ? target.TrimStart('/') : $"xl/{target}";
                result[name] = entryPath;
            }
            return result;
        }

        private static List<string> ReadSharedStringsFromEntries(Dictionary<string, byte[]> entries)
        {
            var list = new List<string>();
            if (!entries.TryGetValue("xl/sharedStrings.xml", out var bytes)) return list;
            XDocument doc;
            using (var ms = new MemoryStream(bytes)) doc = XDocument.Load(ms);
            foreach (var si in doc.Descendants(Ns + "si"))
                list.Add(string.Concat(si.Descendants(Ns + "t").Select(t => t.Value)));
            return list;
        }

        // ── Worksheet update ────────────────────────────────────────────────────────

        private static byte[] UpdateWorksheetLineIds(
            byte[] wsBytes,
            List<string> sharedStrings,
            string sheetName,
            ConvoCoreSettings settings,
            List<SpreadsheetRowConfig> rowConfigs,
            string fileName)
        {
            XDocument wsDoc;
            using (var ms = new MemoryStream(wsBytes)) wsDoc = XDocument.Load(ms);

            // Build a map from 1-based xlsx row number to row element
            var rowByNumber = wsDoc
                .Descendants(Ns + "row")
                .Where(r => int.TryParse(r.Attribute("r")?.Value, out _))
                .ToDictionary(r => int.Parse(r.Attribute("r")!.Value));

            // Find LineID column from the header row
            // Header is at sorted-row-list index ExcelHeaderRowIndex
            var sortedRowNumbers = rowByNumber.Keys.OrderBy(k => k).ToList();
            if (sortedRowNumbers.Count <= settings.ExcelHeaderRowIndex)
            {
                Debug.LogWarning(
                    $"ConvoCore Excel: Cannot write LineIDs to sheet '{sheetName}' in '{fileName}': " +
                    $"no row at header index {settings.ExcelHeaderRowIndex}.");
                return null;
            }

            var headerRowNum = sortedRowNumbers[settings.ExcelHeaderRowIndex];
            var headerRow    = rowByNumber[headerRowNum];
            int? lineIdCol   = null;

            foreach (var cell in headerRow.Elements(Ns + "c"))
            {
                var cellRef = cell.Attribute("r")?.Value;
                if (string.IsNullOrEmpty(cellRef)) continue;
                var val    = GetCellValue(cell, sharedStrings)?.Trim();
                var colIdx = ConvoCoreExcelParser.CellRefToColIndex(cellRef);

                if (string.Equals(val, settings.ExcelLineIDHeader, StringComparison.OrdinalIgnoreCase))
                {
                    lineIdCol = colIdx;
                    break;
                }
            }

            if (lineIdCol == null)
            {
                Debug.LogWarning(
                    $"ConvoCore Excel: Cannot write LineIDs to sheet '{sheetName}' in '{fileName}' " +
                    $"because the '{settings.ExcelLineIDHeader}' column was not found in the header row. " +
                    $"Add a '{settings.ExcelLineIDHeader}' column header or update ExcelLineIDHeader in " +
                    $"ConvoCoreSettings > Spreadsheet.");
                return null;
            }

            // Detect the style index (s attribute) used by any existing cell in the LineID column.
            // New cells inherit this style so they don't appear visually inconsistent in Excel.
            string lineIdColStyle = null;
            foreach (var rowEl in rowByNumber.Values)
            {
                var styleCell = rowEl.Elements(Ns + "c").FirstOrDefault(c =>
                {
                    var r = c.Attribute("r")?.Value;
                    return r != null
                        && ConvoCoreExcelParser.CellRefToColIndex(r) == lineIdCol.Value
                        && c.Attribute("s") != null;
                });
                if (styleCell != null)
                {
                    lineIdColStyle = styleCell.Attribute("s")!.Value;
                    break;
                }
            }

            // Write back using the row numbers stamped by the parser — no skip logic needed
            foreach (var src in rowConfigs)
            {
                if (string.IsNullOrEmpty(src.Config.LineID)) continue;
                if (!rowByNumber.TryGetValue(src.XlRowNumber, out var rowEl)) continue;
                UpdateOrInsertCell(rowEl, src.XlRowNumber, lineIdCol.Value, src.Config.LineID, lineIdColStyle);
            }

            // Serialize back — UTF-8 without BOM, no XML declaration indent
            using (var ms = new MemoryStream())
            {
                using (var xw = XmlWriter.Create(ms, new XmlWriterSettings
                {
                    Encoding           = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    Indent             = false,
                    // Omit the declaration entirely: xlsx XML files work without it, and
                    // synthesising standalone="yes" (which Excel writes) is not exposed by
                    // XDocument.Save — so emitting a partial declaration is worse than none.
                    OmitXmlDeclaration = true
                }))
                    wsDoc.Save(xw);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Finds the cell at <paramref name="colIndex"/> in <paramref name="rowEl"/> and
        /// overwrites its value, or inserts a new cell in column order.
        /// <para>
        /// For existing cells: only the value content and type are replaced.
        /// <c>RemoveAll()</c> must NOT be used — it strips the style index (<c>s</c>),
        /// discarding column widths, number formats, fonts, and fill colours.
        /// </para>
        /// <para>
        /// For new cells: <paramref name="columnStyle"/> (sniffed from an existing sibling
        /// in the same column) is applied so new cells match the surrounding formatting.
        /// </para>
        /// </summary>
        private static void UpdateOrInsertCell(
            XElement rowEl, int rowNum, int colIndex, string value, string columnStyle = null)
        {
            var colLetters = ConvoCoreExcelParser.ColIndexToLetters(colIndex);
            var cellRef    = $"{colLetters}{rowNum}";

            var existing = rowEl.Elements(Ns + "c").FirstOrDefault(c =>
                string.Equals(c.Attribute("r")?.Value, cellRef, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                // Surgical update: preserve all attributes (especially s = style index).
                existing.SetAttributeValue("t", "inlineStr");
                existing.Element(Ns + "v")?.Remove();
                existing.Element(Ns + "f")?.Remove();
                existing.Element(Ns + "is")?.Remove();
                existing.Add(new XElement(Ns + "is", new XElement(Ns + "t", value)));
            }
            else
            {
                // Attribute order: r, s (optional), t — mirrors what Excel itself writes.
                var newCell = new XElement(Ns + "c", new XAttribute("r", cellRef));
                if (columnStyle != null)
                    newCell.Add(new XAttribute("s", columnStyle));
                newCell.Add(new XAttribute("t", "inlineStr"));
                newCell.Add(new XElement(Ns + "is", new XElement(Ns + "t", value)));

                // Insert in column order
                var insertBefore = rowEl.Elements(Ns + "c").FirstOrDefault(c =>
                    ConvoCoreExcelParser.CellRefToColIndex(c.Attribute("r")?.Value ?? "") > colIndex);

                if (insertBefore != null)
                    insertBefore.AddBeforeSelf(newCell);
                else
                    rowEl.Add(newCell);
            }
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
            return cell.Element(Ns + "v")?.Value?.Trim() ?? string.Empty;
        }
    }
}
