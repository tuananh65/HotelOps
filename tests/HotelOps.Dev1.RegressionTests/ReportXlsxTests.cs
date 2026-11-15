using System.IO.Compression;
using System.Xml.Linq;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.Insights;
using Xunit;

public sealed class ReportXlsxTests
{
    private static ReportResult Snapshot(string value)
    {
        var rows = new[] { new Dictionary<string, object?> { ["value"] = value, ["hidden"] = "secret" } };
        return new(new string('a', 32), "tasks", DateTimeOffset.UtcNow, new([1], null, new(2026, 11, 1), new(2026, 11, 16)),
            ["value"], rows, 1, [new("tasks", ["value"], rows)], ["Unavailable indicator"], ["csv", "xlsx"]);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"https://example.invalid\")")]
    [InlineData(" +SUM(1,2)")]
    [InlineData("@SUM(1,2)")]
    [InlineData("-1")]
    [InlineData("Phòng <A> & B\n  ")]
    [InlineData("9223372036854775807")]
    public void WorkbookPreservesExactTextWithoutExecutableCells(string value)
    {
        var snapshot = Snapshot(value);
        var file = ReportXlsxRenderer.Render(snapshot);
        Assert.Equal(ReportXlsxRenderer.ContentType, file.ContentType);
        Assert.EndsWith(".xlsx", file.Name);
        using var zip = new ZipArchive(new MemoryStream(file.Content));
        Assert.Equal(5, zip.Entries.Count);
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            Assert.DoesNotContain(document.Descendants(), e => e.Name.LocalName is "f" or "hyperlink" or "externalLink");
            Assert.DoesNotContain(document.Descendants().Attributes(), a => a.Name.LocalName == "TargetMode" && a.Value == "External");
        }
        using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(sheet);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var text = xml.Descendants(ns + "t").Select(t => t.Value).ToArray();
        Assert.Contains(value, text); Assert.Contains(snapshot.SnapshotId, text);
        Assert.Contains("Unavailable indicator", text); Assert.DoesNotContain("secret", text);
        Assert.All(xml.Descendants(ns + "c"), c => Assert.Equal("inlineStr", c.Attribute("t")!.Value));
        Assert.Equal(file.Content, ReportXlsxRenderer.Render(snapshot).Content);
    }

    [Fact]
    public void CellOverflowRejectsRatherThanTruncates() => Assert.Equal(422,
        Assert.Throws<InsightException>(() => ReportXlsxRenderer.Render(Snapshot(new string('x', 32768)))).Status);

    [Fact]
    public void InvalidXmlFailsWithValidationError() => Assert.Equal(422,
        Assert.Throws<InsightException>(() => ReportXlsxRenderer.Render(Snapshot("bad\0value"))).Status);

    [Fact]
    public void CountsAndColumnsRemainStrict()
    {
        var report = Snapshot("ok");
        Assert.Throws<InsightException>(() => ReportXlsxRenderer.Render(report with { TotalCount = 2 }));
        report.Sections[0].Rows[0].Remove("value");
        Assert.Throws<InsightException>(() => ReportXlsxRenderer.Render(report));
    }
}
