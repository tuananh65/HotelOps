using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using HotelOps.Api.DTOs.Insights;

namespace HotelOps.Api.Services.Insights;

public static class ReportXlsxRenderer
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static ExportFile Render(ReportResult report)
    {
        ReportCsvRenderer.Validate(report);
        using var sheet = new MemoryStream();
        try
        {
            using var xml = XmlWriter.Create(sheet, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false });
            xml.WriteStartDocument();
            xml.WriteStartElement("worksheet", Spreadsheet);
            xml.WriteStartElement("sheetData", Spreadsheet);
            var row = 0;
            foreach (var values in ReportCsvRenderer.Lines(report))
            {
                if (++row > 1048576) throw TooLarge();
                xml.WriteStartElement("row", Spreadsheet);
                xml.WriteAttributeString("r", row.ToString(CultureInfo.InvariantCulture));
                var column = 0;
                foreach (var value in values)
                {
                    if (++column > 16384 || value.Length > 32767) throw TooLarge();
                    // Explicit strings preserve IDs/decimals and cannot become formulas or links.
                    xml.WriteStartElement("c", Spreadsheet);
                    xml.WriteAttributeString("t", "inlineStr");
                    xml.WriteStartElement("is", Spreadsheet);
                    xml.WriteStartElement("t", Spreadsheet);
                    xml.WriteAttributeString("xml", "space", null, "preserve");
                    xml.WriteString(value);
                    xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();
                    xml.Flush();
                    if (sheet.Length > 32 * 1024 * 1024) throw TooLarge();
                }
                xml.WriteEndElement();
            }
            xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndDocument();
        }
        catch (ArgumentException) { throw new InsightException(422, "INVALID_SNAPSHOT", "Report contains unsupported XML characters."); }
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Part(string name, byte[] content)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var stream = entry.Open(); stream.Write(content);
            }
            void Xml(string name, string content) => Part(name, Encoding.UTF8.GetBytes(content));
            Xml("[Content_Types].xml", """
                <?xml version="1.0" encoding="utf-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
                """);
            Xml("_rels/.rels", """
                <?xml version="1.0" encoding="utf-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Xml("xl/workbook.xml", """
                <?xml version="1.0" encoding="utf-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Report" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            Xml("xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="utf-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>
                """);
            Part("xl/worksheets/sheet1.xml", sheet.ToArray());
        }
        return new(output.ToArray(), ContentType, $"{report.Key}-{report.SnapshotId}.xlsx");
    }

    private static InsightException TooLarge() => new(422, "EXPORT_TOO_LARGE", "Report exceeds XLSX cell, sheet or size limits; no content was truncated.");
}
