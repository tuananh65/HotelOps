using System.Globalization;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed record WorkAlertParameters(int ScanMinutes, decimal ThresholdPercent)
{
    internal static async Task<WorkAlertParameters> ReadAsync(HotelOpsDbContext db, CancellationToken ct)
    {
        var rows = await db.SystemSettings.AsNoTracking().Where(s => s.BranchId == null
            && (s.SettingKey == "SYS-PAR-15" || s.SettingKey == "SYS-PAR-21"))
            .Select(s => new { s.SettingKey, s.SettingValue }).ToArrayAsync(ct);
        if (rows.GroupBy(s => s.SettingKey).Any(g => g.Count() != 1))
            throw new InvalidOperationException("Ambiguous global JOB01 parameters.");
        // Published SRS defaults; explicit malformed configuration fails closed.
        var scanRow = rows.SingleOrDefault(s => s.SettingKey == "SYS-PAR-15");
        var riskRow = rows.SingleOrDefault(s => s.SettingKey == "SYS-PAR-21");
        var scan = scanRow == null ? "5" : scanRow.SettingValue;
        var risk = riskRow == null ? "80" : riskRow.SettingValue;
        if (!int.TryParse(scan, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes is < 1 or > 1440
            || !decimal.TryParse(risk, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var percent) || percent is <= 0 or > 100)
            throw new InvalidOperationException("Invalid JOB01 parameters.");
        return new(minutes, percent);
    }
}
