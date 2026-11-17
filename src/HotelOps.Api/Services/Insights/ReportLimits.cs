using System.Globalization;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

internal static class ReportLimits
{
    internal static Task<int> RowsAsync(HotelOpsDbContext db, CancellationToken ct)
        => ReadAsync(db, "SYS-PAR-12", 50000, 50000, ct);
    internal static Task<int> PeriodAsync(HotelOpsDbContext db, CancellationToken ct)
        => ReadAsync(db, "SYS-PAR-20", 30, DateOnly.MaxValue.DayNumber, ct);
    private static async Task<int> ReadAsync(HotelOpsDbContext db, string key, int fallback, int maximum, CancellationToken ct)
    {
        var values = await db.SystemSettings.AsNoTracking().Where(s => s.BranchId == null && s.SettingKey == key)
            .Select(s => s.SettingValue).ToArrayAsync(ct);
        if (values.Length == 0) return fallback;
        if (values.Length != 1 || !int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            || value < 1 || value > maximum)
            throw new InsightException(503, "REPORT_CONFIGURATION", $"{key} must be a single integer from 1 to {maximum}; report execution is blocked, not truncated.");
        return value;
    }
}
