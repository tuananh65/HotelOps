using System.Globalization;
using System.Text;
using System.Text.Json;
using HotelOps.Api.DTOs.Insights;

namespace HotelOps.Api.Services.Insights;

// Pure file generation from a SERVER snapshot. This does not authorize an export
// or record its audit. An eventual endpoint must do both before sending bytes.
public static class ReportCsvRenderer
{
    public static ExportFile Render(ReportResult report)
    {
        Validate(report);
        var text = new StringBuilder();
        void Line(IEnumerable<string> values)
        {
            text.AppendJoin(',', values.Select(Cell)).Append("\r\n");
            if (text.Length > 32 * 1024 * 1024)
                throw new InsightException(422, "EXPORT_TOO_LARGE", "Narrow the report filters.");
        }
        foreach (var row in Lines(report)) Line(row);
        // UTF-8 BOM supports Vietnamese text in spreadsheet applications.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
        return new(bytes, "text/csv; charset=utf-8", $"{report.Key}-{report.SnapshotId}.csv");
    }

    internal static void Validate(ReportResult report)
    {
        if (!Guid.TryParseExact(report.SnapshotId, "N", out _) || !ReportSnapshotQueries.Keys.Contains(report.Key))
            throw new InsightException(422, "INVALID_SNAPSHOT", "Invalid report identity.");
        var rows = report.Sections.Sum(section => (long)section.Rows.Count);
        if (rows > 50000 || rows != report.TotalCount)
            throw new InsightException(422, "REPORT_ROW_LIMIT", "Report row count is invalid; no rows were truncated.");
    }

    internal static IEnumerable<IEnumerable<string>> Lines(ReportResult report)
    {
        yield return ["Report", report.Key];
        yield return ["Snapshot ID", report.SnapshotId];
        yield return ["Snapshot UTC", report.Snapshot.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)];
        yield return ["Branches", string.Join(';', report.Filters.BranchIds)];
        yield return ["Department", report.Filters.DepartmentId?.ToString(CultureInfo.InvariantCulture) ?? "All permitted"];
        yield return ["Period UTC", report.Filters.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            report.Filters.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)];
        if (report.AppliedFilters is { } filters)
            foreach (var filter in filters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                yield return ["Filter", filter.Key, filter.Value ?? ""];
        foreach (var missing in report.MissingSources) yield return ["Unavailable indicator", missing];
        foreach (var section in report.Sections)
        {
            yield return ["Section", section.Key];
            yield return section.Columns;
            foreach (var row in section.Rows)
                yield return section.Columns.Select(column => row.TryGetValue(column, out var value)
                    ? Scalar(value) : throw new InsightException(422, "INVALID_SNAPSHOT", "A report column is missing."));
        }
    }

    private static string Cell(string value)
    {
        // Quoting alone does not stop spreadsheet formulas. Inspect after leading
        // whitespace/control characters, and protect control-prefixed cells too.
        var probe = value.AsSpan().TrimStart();
        if (value.Length > 0 && (char.IsControl(value[0])
            || probe.Length > 0 && "=+-@".Contains(probe[0]))) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string Scalar(object? value) => value switch
    {
        null => "",
        JsonElement json => json.ValueKind switch
        {
            JsonValueKind.Null => "", JsonValueKind.String => json.GetString()!,
            JsonValueKind.Number => json.GetRawText(), JsonValueKind.True => "true", JsonValueKind.False => "false",
            _ => throw new InsightException(422, "INVALID_SNAPSHOT", "Report cells must be scalar values.")
        },
        DateTime date => DateTime.SpecifyKind(date, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        string text => text,
        bool flag => flag ? "true" : "false",
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => throw new InsightException(422, "INVALID_SNAPSHOT", "Unsupported report cell.")
    };
}
