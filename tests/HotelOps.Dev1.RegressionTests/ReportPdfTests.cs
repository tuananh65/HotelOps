using System.Text;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.Insights;
using Xunit;

public sealed class ReportPdfTests
{
    private static ReportResult Snapshot(params string[] values)
    {
        var rows = values.Select(v => new Dictionary<string, object?> { ["value"] = v, ["hidden"] = "HIDDEN_SECRET" }).ToArray();
        return new(new string('a', 32), "tasks", new DateTimeOffset(2026, 11, 16, 1, 2, 3, TimeSpan.Zero),
            new([1], null, new(2026, 11, 1), new(2026, 11, 16)), ["value"], rows, rows.Length,
            [new("tasks", ["value"], rows)], ["Unavailable indicator"], ["csv", "xlsx", "pdf"],
            new Dictionary<string, string?> { ["employeeId"] = "42" });
    }

    [Theory]
    [InlineData("Phòng kiểm tra Nguyễn Thị Ánh")]
    [InlineData("Pho\u0300ng kie\u0302\u0309m tra")]
    [InlineData("9223372036854775807 0.123456789123456789")]
    [InlineData("=HYPERLINK(\"https://example.invalid\") <script>alert(1)</script>")]
    public void IndependentParserReadsUnicodeAndExactScalarText(string value)
    {
        var snapshot = Snapshot(value);
        var file = ReportPdfRenderer.Render(snapshot);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.EndsWith(".pdf", file.Name);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(file.Content);
        var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
        Assert.Contains(value.Normalize(), text);
        Assert.Contains(snapshot.SnapshotId, text);
        Assert.Contains("employeeId", text);
        Assert.Contains("Unavailable indicator", text);
        Assert.DoesNotContain("HIDDEN_SECRET", text);
        Assert.DoesNotContain("/JavaScript", Encoding.Latin1.GetString(file.Content));
        Assert.DoesNotContain("/URI", Encoding.Latin1.GetString(file.Content));
        Assert.DoesNotContain("/EmbeddedFile", Encoding.Latin1.GetString(file.Content));
    }

    [Fact]
    public void AllRowsSurviveWrappingAndPageBreaks()
    {
        var rows = Enumerable.Range(0, 300).Select(i => $"UNIQUE_ROW_{i:D4} " + new string('w', 180)).ToArray();
        var file = ReportPdfRenderer.Render(Snapshot(rows));
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(file.Content);
        Assert.True(pdf.NumberOfPages > 1);
        var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
        for (var i = 0; i < rows.Length; i++) Assert.Contains($"UNIQUE_ROW_{i:D4}", text);
        Assert.Equal(300 * 180, text.Count(c => c == 'w')); // Every long value survives line and page wrapping.
        foreach (var page in pdf.GetPages())
            Assert.All(page.Letters, letter => Assert.True(letter.BoundingBox.Left >= 30 && letter.BoundingBox.Right < page.Width - 25));
    }

    [Theory]
    [InlineData("missing glyph \U0001f680")]
    [InlineData("bad\0value")]
    public void UnsupportedTextFailsExplicitlyInsteadOfReplacingCharacters(string value) =>
        Assert.Equal("PDF_TEXT_UNSUPPORTED", Assert.Throws<InsightException>(() => ReportPdfRenderer.Render(Snapshot(value))).Code);

    [Fact]
    public void InvalidUtf16FailsWithoutTestRunnerSerialization()
    {
        var value = "bad" + (char)0xd800 + "value";
        Assert.Equal("PDF_TEXT_UNSUPPORTED", Assert.Throws<InsightException>(() => ReportPdfRenderer.Render(Snapshot(value))).Code);
    }

    [Fact]
    public void OversizeCancellationAndInvalidSnapshotDoNotProducePartialFiles()
    {
        Assert.Equal(422, Assert.Throws<InsightException>(() => ReportPdfRenderer.Render(Snapshot(new string('x', 2_000_001)))).Status);
        Assert.Throws<OperationCanceledException>(() => ReportPdfRenderer.Render(Snapshot("text"), new CancellationToken(true)));
        Assert.Throws<InsightException>(() => ReportPdfRenderer.Render(Snapshot("text") with { TotalCount = 2 }));
    }
}
