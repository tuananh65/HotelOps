using System.Globalization;
using System.Text;
using HotelOps.Api.DTOs.Insights;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace HotelOps.Api.Services.Insights;

// Pure snapshot rendering. ReportService owns live authorization, CSRF and audit commit.
public static class ReportPdfRenderer
{
    public const string ContentType = "application/pdf";
    private const int MaxPages = 1000, MaxCharacters = 2_000_000;

    public static ExportFile Render(ReportResult report, CancellationToken ct = default)
    {
        ReportCsvRenderer.Validate(report);
        ct.ThrowIfCancellationRequested();
        using var document = new PdfDocument();
        document.Info.Title = "HotelOps " + report.Key;
        document.Info.Subject = "Snapshot " + report.SnapshotId;
        document.Info.Author = "HotelOps";
        var font = ReportPdfFonts.Create(9);
        var heading = ReportPdfFonts.Create(10);
        XGraphics? graphics = null;
        double y = 0, width = 0, bottom = 0;
        var characters = 0;
        var checkedGlyphs = new HashSet<int>();
        void NewPage()
        {
            ct.ThrowIfCancellationRequested();
            if (document.PageCount >= MaxPages) throw TooLarge();
            graphics?.Dispose();
            var page = document.AddPage();
            page.Size = PageSize.A4;
            graphics = XGraphics.FromPdfPage(page);
            width = page.Width.Point - 72;
            bottom = page.Height.Point - 48;
            graphics.DrawString($"HotelOps / {report.Key} / Page {document.PageCount}", heading, XBrushes.Black, new XPoint(36, 30));
            graphics.DrawString(report.SnapshotId, font, XBrushes.Gray, new XPoint(36, page.Height.Point - 24));
            y = 50;
        }
        void Line(string text)
        {
            if (graphics == null || y > bottom) NewPage();
            graphics!.DrawString(text, font, XBrushes.Black, new XPoint(36, y));
            y += 13;
        }
        try
        {
            NewPage();
            foreach (var values in ReportCsvRenderer.Lines(report))
            {
                ct.ThrowIfCancellationRequested();
                var raw = string.Join(" | ", values);
                characters += raw.Length;
                if (characters > MaxCharacters) throw TooLarge();
                string text;
                try { text = raw.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ").Normalize(NormalizationForm.FormC); }
                catch (ArgumentException) { throw UnsupportedText(); }
                foreach (var rune in text.EnumerateRunes())
                    if (rune.Value != '\n' && (Rune.IsControl(rune)
                        || checkedGlyphs.Add(rune.Value) && GlyphHelper.GlyphIndexFromCodePoint(rune.Value, font) == 0))
                        throw UnsupportedText();
                foreach (var paragraph in text.Split('\n'))
                {
                    var line = new StringBuilder();
                    var elements = StringInfo.GetTextElementEnumerator(paragraph);
                    while (elements.MoveNext())
                    {
                        ct.ThrowIfCancellationRequested();
                        var element = elements.GetTextElement();
                        if (graphics!.MeasureString(line.ToString() + element, font).Width > width)
                        {
                            if (line.Length == 0) throw UnsupportedText();
                            Line(line.ToString()); line.Clear();
                        }
                        line.Append(element);
                    }
                    Line(line.ToString());
                }
                y += 4;
            }
            graphics?.Dispose(); graphics = null;
            ct.ThrowIfCancellationRequested();
            using var output = new MemoryStream();
            document.Save(output, closeStream: false);
            if (output.Length > 32 * 1024 * 1024) throw TooLarge();
            return new(output.ToArray(), ContentType, $"{report.Key}-{report.SnapshotId}.pdf");
        }
        finally { graphics?.Dispose(); }
    }

    private static InsightException TooLarge() => new(422, "EXPORT_TOO_LARGE", "Narrow the report filters; PDF was not truncated.");
    private static InsightException UnsupportedText() => new(422, "PDF_TEXT_UNSUPPORTED", "PDF cannot faithfully render some characters. Use CSV or XLSX.");
}
