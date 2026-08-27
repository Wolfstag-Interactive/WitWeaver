#if UNITY_EDITOR
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace WolfstagInteractive.ConvoCore.Editor
{
    /// <summary>
    /// Converts a ConvoCore YAML dialogue file to a .xlsx spreadsheet.
    ///
    /// Each conversation key in the YAML becomes one sheet in the workbook.
    /// Columns: LineID | CharacterID | [one column per language code] ...
    /// Row 1 is a frozen bold header row.
    ///
    /// Uses <see cref="System.IO.Compression.ZipArchive"/> and <see cref="System.Xml.Linq.XDocument"/>
    /// — the same infrastructure as <see cref="ConvoCoreExcelParser"/> and
    /// <see cref="ConvoCoreExcelWriter"/>. No external dependencies.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreYamlToExcelExporter.html")]
    public static class ConvoCoreYamlToExcelExporter
    {
        private static readonly XNamespace Ns     = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RNs    = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        // ------------------------------------------------------------------
        // Public entry point
        // ------------------------------------------------------------------

        /// <summary>
        /// Reads the YAML at <paramref name="yamlPath"/> and writes a .xlsx to
        /// <paramref name="outputPath"/>. Returns null on success or an error message on failure.
        /// </summary>
        public static string Export(string yamlPath, string outputPath)
        {
            try
            {
                var yaml = File.ReadAllText(yamlPath, Encoding.UTF8);
                var conversations = ConvoCoreYamlParser.Parse(yaml);

                if (conversations == null || conversations.Count == 0)
                    return "YAML file contains no conversation data.";

                WriteXlsx(conversations, outputPath);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Writes the provided conversation data to the specified file path in .xlsx format.
        /// </summary>
        /// <param name="conversations">A dictionary containing conversation data where the key is a string identifier, and the value is a list of <see cref="DialogueYamlConfig"/> representing the dialogues.</param>
        /// <param name="path">The output file path where the .xlsx file will be written.</param>
        private static void WriteXlsx(Dictionary<string, List<DialogueYamlConfig>> conversations, string path)
        {
            // Build a shared string table so all cells reference it.
            var sharedStrings = new List<string>();
            var ssIndex = new Dictionary<string, int>(StringComparer.Ordinal);

            int SS(string value)
            {
                if (value == null) value = "";
                if (ssIndex.TryGetValue(value, out var i)) return i;
                i = sharedStrings.Count;
                sharedStrings.Add(value);
                ssIndex[value] = i;
                return i;
            }

            // Build sheet data: (name, rows) where rows[0] is the header.
            var sheets = new List<(string name, List<List<int>> rows)>();

            foreach (var kv in conversations)
            {
                var lines = kv.Value;
                if (lines == null || lines.Count == 0) continue;

                // Collect language codes in order of first appearance.
                var langs = new List<string>();
                var seenLangs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in lines)
                    if (line.LocalizedDialogue != null)
                        foreach (var lang in line.LocalizedDialogue.Keys)
                            if (seenLangs.Add(lang.ToUpperInvariant()))
                                langs.Add(lang.ToUpperInvariant());

                var header = new List<int> { SS("LineID"), SS("CharacterID") };
                foreach (var lang in langs) header.Add(SS(lang));

                var rows = new List<List<int>> { header };
                foreach (var line in lines)
                {
                    var row = new List<int> { SS(line.LineID ?? ""), SS(line.CharacterID ?? "") };
                    foreach (var lang in langs)
                    {
                        string text = "";
                        if (line.LocalizedDialogue != null &&
                            !line.LocalizedDialogue.TryGetValue(lang, out text) &&
                            !line.LocalizedDialogue.TryGetValue(lang.ToUpperInvariant(), out text))
                            text = "";
                        row.Add(SS(text ?? ""));
                    }
                    rows.Add(row);
                }

                sheets.Add((SanitizeSheetName(kv.Key), rows));
            }

            // Write ZIP entries.
            if (File.Exists(path)) File.Delete(path);

            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            WriteEntry(archive, "[Content_Types].xml",       BuildContentTypes(sheets.Count));
            WriteEntry(archive, "_rels/.rels",                BuildRels());
            WriteEntry(archive, "xl/workbook.xml",            BuildWorkbook(sheets));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRels(sheets.Count));
            WriteEntry(archive, "xl/sharedStrings.xml",       BuildSharedStrings(sharedStrings));
            WriteEntry(archive, "xl/styles.xml",              BuildStyles());

            for (int i = 0; i < sheets.Count; i++)
                WriteEntry(archive, $"xl/worksheets/sheet{i + 1}.xml",
                    BuildSheet(sheets[i].rows));
        }

        /// <summary>
        /// Generates an XML document defining the Content Types for an OpenXML package,
        /// based on the specified number of worksheet parts.
        /// </summary>
        /// <param name="sheetCount">The total number of worksheet parts to include in the Content Types.</param>
        /// <returns>An <see cref="XDocument"/> containing the Content Types definition.</returns>
        private static XDocument BuildContentTypes(int sheetCount)
        {
            var overrides = new List<XElement>
            {
                new XElement("Override",
                    new XAttribute("PartName", "/xl/workbook.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement("Override",
                    new XAttribute("PartName", "/xl/sharedStrings.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml")),
                new XElement("Override",
                    new XAttribute("PartName", "/xl/styles.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")),
            };

            for (int i = 1; i <= sheetCount; i++)
                overrides.Add(new XElement("Override",
                    new XAttribute("PartName", $"/xl/worksheets/sheet{i}.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));

            var ns = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/content-types");
            return new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(ns + "Types",
                    new XElement(ns + "Default",
                        new XAttribute("Extension", "rels"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(ns + "Default",
                        new XAttribute("Extension", "xml"),
                        new XAttribute("ContentType", "application/xml")),
                    overrides.Select(o => new XElement(ns + o.Name.LocalName,
                        o.Attributes().Select(a => new XAttribute(a.Name, a.Value))))));
        }

        private static XDocument BuildRels() =>
            new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(PkgRel + "Relationships",
                    new XElement(PkgRel + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                        new XAttribute("Target", "xl/workbook.xml"))));

        private static XDocument BuildWorkbook(List<(string name, List<List<int>> rows)> sheets) =>
            new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(Ns + "workbook",
                    new XAttribute(XNamespace.Xmlns + "r", RNs.NamespaceName),
                    new XElement(Ns + "sheets",
                        sheets.Select((s, i) => new XElement(Ns + "sheet",
                            new XAttribute("name", s.name),
                            new XAttribute("sheetId", i + 1),
                            new XAttribute(RNs + "id", $"rId{i + 1}"))))));

        private static XDocument BuildWorkbookRels(int sheetCount)
        {
            var rels = new List<XElement>();
            for (int i = 1; i <= sheetCount; i++)
                rels.Add(new XElement(PkgRel + "Relationship",
                    new XAttribute("Id", $"rId{i}"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", $"worksheets/sheet{i}.xml")));

            rels.Add(new XElement(PkgRel + "Relationship",
                new XAttribute("Id", $"rId{sheetCount + 1}"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"),
                new XAttribute("Target", "sharedStrings.xml")));

            rels.Add(new XElement(PkgRel + "Relationship",
                new XAttribute("Id", $"rId{sheetCount + 2}"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                new XAttribute("Target", "styles.xml")));

            return new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(PkgRel + "Relationships", rels));
        }

        private static XDocument BuildSharedStrings(List<string> strings) =>
            new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(Ns + "sst",
                    new XAttribute("count", strings.Count),
                    new XAttribute("uniqueCount", strings.Count),
                    strings.Select(s =>
                        new XElement(Ns + "si",
                            new XElement(Ns + "t",
                                new XAttribute(XNamespace.Xml + "space", "preserve"),
                                s)))));

        private static XDocument BuildStyles() =>
            new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(Ns + "styleSheet",
                    new XElement(Ns + "fonts",
                        new XElement(Ns + "font",
                            new XElement(Ns + "sz", new XAttribute("val", "11")),
                            new XElement(Ns + "name", new XAttribute("val", "Calibri"))),
                        new XElement(Ns + "font",                                        // index 1: bold
                            new XElement(Ns + "b"),
                            new XElement(Ns + "sz", new XAttribute("val", "11")),
                            new XElement(Ns + "name", new XAttribute("val", "Calibri")))),
                    new XElement(Ns + "fills",
                        new XElement(Ns + "fill", new XElement(Ns + "patternFill", new XAttribute("patternType", "none"))),
                        new XElement(Ns + "fill", new XElement(Ns + "patternFill", new XAttribute("patternType", "gray125")))),
                    new XElement(Ns + "borders",
                        new XElement(Ns + "border",
                            new XElement(Ns + "left"), new XElement(Ns + "right"),
                            new XElement(Ns + "top"), new XElement(Ns + "bottom"), new XElement(Ns + "diagonal"))),
                    new XElement(Ns + "cellStyleXfs",
                        new XElement(Ns + "xf",
                            new XAttribute("numFmtId", 0), new XAttribute("fontId", 0),
                            new XAttribute("fillId", 0), new XAttribute("borderId", 0))),
                    new XElement(Ns + "cellXfs",
                        new XElement(Ns + "xf",                                          // index 0: normal
                            new XAttribute("numFmtId", 0), new XAttribute("fontId", 0),
                            new XAttribute("fillId", 0), new XAttribute("borderId", 0), new XAttribute("xfId", 0)),
                        new XElement(Ns + "xf",                                          // index 1: bold header
                            new XAttribute("numFmtId", 0), new XAttribute("fontId", 1),
                            new XAttribute("fillId", 0), new XAttribute("borderId", 0), new XAttribute("xfId", 0)))));

        private static XDocument BuildSheet(List<List<int>> rows)
        {
            var sheetData = new XElement(Ns + "sheetData");
            for (int r = 0; r < rows.Count; r++)
            {
                int rowNum = r + 1;
                bool isHeader = r == 0;
                var rowEl = new XElement(Ns + "row", new XAttribute("r", rowNum));
                var row = rows[r];
                for (int c = 0; c < row.Count; c++)
                {
                    string cellRef = ConvoCoreExcelParser.ColIndexToLetters(c) + rowNum;
                    var cellEl = new XElement(Ns + "c",
                        new XAttribute("r", cellRef),
                        new XAttribute("t", "s"));                    // shared string type
                    if (isHeader)
                        cellEl.Add(new XAttribute("s", "1"));         // bold style index
                    cellEl.Add(new XElement(Ns + "v", row[c]));
                    rowEl.Add(cellEl);
                }
                sheetData.Add(rowEl);
            }

            return new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(Ns + "worksheet",
                    new XElement(Ns + "sheetViews",
                        new XElement(Ns + "sheetView",
                            new XAttribute("workbookViewId", 0),
                            new XElement(Ns + "pane",                 // freeze row 1
                                new XAttribute("ySplit", 1),
                                new XAttribute("topLeftCell", "A2"),
                                new XAttribute("activePane", "bottomLeft"),
                                new XAttribute("state", "frozen")))),
                    sheetData));
        }

        private static void WriteEntry(ZipArchive archive, string entryName, XDocument doc)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var xw = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding           = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent             = false,
                OmitXmlDeclaration = false
            });
            doc.Save(xw);
        }
        private static string SanitizeSheetName(string name)
        {
            foreach (var ch in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                name = name.Replace(ch.ToString(), "");
            if (name.Length > 31) name = name.Substring(0, 31);
            return string.IsNullOrWhiteSpace(name) ? "Sheet" : name;
        }
    }
}
#endif