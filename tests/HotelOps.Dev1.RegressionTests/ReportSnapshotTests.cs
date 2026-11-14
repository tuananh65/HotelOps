using System.Text;
using System.Text.Json;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.Insights;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ReportSnapshotTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static ReportResult Report(params string[] values)
    {
        var rows = values.Select(value => new Dictionary<string, object?> { ["value"] = value }).ToArray();
        return new(Guid.NewGuid().ToString("N"), "tasks", new Clock().Now,
            new([1], null, new(2026, 11, 1), new(2026, 11, 15)), ["value"], rows,
            rows.Length, [new("tasks", ["value"], rows)], ["Effort unavailable"], []);
    }

    [Fact]
    public void SnapshotIsOwnedFrozenAndExpiresAtBoundary()
    {
        var clock = new Clock(); var store = new ReportSnapshotStore(clock);
        var report = Report("original"); store.Put(1, report);
        report.Rows[0]["value"] = "mutated";
        Assert.Equal(404, Assert.Throws<InsightException>(() => store.Get(2, report.SnapshotId)).Status);
        var copy = store.Get(1, report.SnapshotId);
        Assert.Equal("original", ((JsonElement)copy.Rows[0]["value"]!).GetString());
        copy.Rows[0]["value"] = "another mutation";
        Assert.Contains("original", Encoding.UTF8.GetString(ReportCsvRenderer.Render(store.Get(1, report.SnapshotId)).Content));
        clock.Now = clock.Now.AddMinutes(15);
        Assert.Equal(404, Assert.Throws<InsightException>(() => store.Get(1, report.SnapshotId)).Status);
    }

    [Fact]
    public void DuplicateCannotReplaceOwnerAndCapacityEvictsOldest()
    {
        var clock = new Clock(); var store = new ReportSnapshotStore(clock); var first = Report("a");
        store.Put(1, first);
        Assert.Throws<InvalidOperationException>(() => store.Put(2, first));
        for (var i = 0; i < 20; i++) { clock.Now = clock.Now.AddSeconds(1); store.Put(1, Report("b")); }
        Assert.Throws<InsightException>(() => store.Get(1, first.SnapshotId));
    }

    [Theory]
    [InlineData("=SUM(1,2)")]
    [InlineData("+cmd")]
    [InlineData("-42")]
    [InlineData("@SUM(A1)")]
    [InlineData("  =1+1")]
    [InlineData("\tplain")]
    [InlineData("\r=1+1")]
    public void SpreadsheetCellsAreNeutralized(string value)
    {
        var csv = Encoding.UTF8.GetString(ReportCsvRenderer.Render(Report(value)).Content);
        Assert.Contains("\"'" + value.Replace("\"", "\"\"") + "\"\r\n", csv);
    }

    [Fact]
    public void UnicodeQuotesNewlinesAndMetadataSurvive()
    {
        var report = Report("Phòng \"A\",\nB"); var file = ReportCsvRenderer.Render(report);
        Assert.True(file.Content.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        var csv = Encoding.UTF8.GetString(file.Content);
        Assert.Contains("\"Phòng \"\"A\"\",\nB\"", csv);
        Assert.Contains(report.SnapshotId, csv); Assert.Contains("Effort unavailable", csv);
        Assert.Contains("2026-11-01", csv); Assert.Contains("2026-11-15", csv);
    }

    [Fact]
    public void OnlyDeclaredColumnsAndAllSectionsAreRendered()
    {
        var report = Report("first"); report.Rows[0]["password"] = "secret-must-not-export";
        report = report with { TotalCount = 2, Sections = [report.Sections[0], new("second", ["value"], [new() { ["value"] = "second row" }])] };
        var csv = Encoding.UTF8.GetString(ReportCsvRenderer.Render(report).Content);
        Assert.Contains("first", csv); Assert.Contains("second row", csv); Assert.DoesNotContain("secret", csv);
        Assert.DoesNotContain("password", csv);
    }

    [Fact]
    public void EmptyReportRetainsHeadersAndMissingIndicators()
    {
        var csv = Encoding.UTF8.GetString(ReportCsvRenderer.Render(Report()).Content);
        Assert.Contains("\"value\"\r\n", csv); Assert.Contains("Effort unavailable", csv);
    }

    [Fact]
    public void RowLimitIsAcrossSectionsAndDoesNotTruncate()
    {
        var report = Report(Enumerable.Repeat("ok", 50000).ToArray());
        Assert.NotEmpty(ReportCsvRenderer.Render(report).Content);
        var excess = report with { TotalCount = 50001, Sections = [report.Sections[0], new("extra", ["value"], [new() { ["value"] = "overflow" }])] };
        Assert.Equal("REPORT_ROW_LIMIT", Assert.Throws<InsightException>(() => ReportCsvRenderer.Render(excess)).Code);
        Assert.Throws<InsightException>(() => ReportCsvRenderer.Render(report with { TotalCount = 2 }));
    }

    [Fact]
    public void MissingOrNestedCellsAndUnsafeIdentityAreRefused()
    {
        var report = Report("test");
        Assert.Throws<InsightException>(() => ReportCsvRenderer.Render(report with { SnapshotId = "../../file" }));
        report.Rows[0].Clear();
        Assert.Throws<InsightException>(() => ReportCsvRenderer.Render(report));
        report.Rows[0]["value"] = JsonSerializer.SerializeToElement(new { secret = "hidden" });
        Assert.Throws<InsightException>(() => ReportCsvRenderer.Render(report));
    }

    [Fact]
    public void SqlTimestampAndDecimalSerializationAreExplicit()
    {
        var options = new JsonSerializerOptions(); options.Converters.Add(new InsightUtcJson()); options.Converters.Add(new InsightDecimalJson());
        Assert.Equal("\"2026-11-15T01:02:03Z\"", JsonSerializer.Serialize(new DateTime(2026,11,15,1,2,3), options));
        Assert.Equal("\"123456789012345.1234\"", JsonSerializer.Serialize(123456789012345.1234m, options));
    }

    [Fact]
    public void UnsupportedAndInvalidFiltersAreNotIgnored()
    {
        Assert.Throws<InsightException>(() => ReportSnapshotQueries.ValidateFilters("tasks", new() { FloorId = 1 }));
        Assert.Throws<InsightException>(() => ReportSnapshotQueries.ValidateFilters("assets", new() { Condition = "bad\n" }));
        Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(() => ReportSnapshotQueries.ValidateFilters("rooms", new() { FloorId = 0 }));
    }
}
