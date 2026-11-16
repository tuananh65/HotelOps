using HotelOps.Api.Data;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

// Availability is not mutation authority: the service still proves the submitted
// quantities, evidence bytes, lifecycle and native version inside its transaction.
internal static class MaintenanceVerifyAvailability
{
    internal static async Task<bool> AllowsAsync(HotelOpsDbContext db, long user, long branch, long work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Review transaction required.");
        if (!await IncidentEscalationClosure.InstalledAsync(db, ct)
            || !await MaintenanceMaterialUsageSchema.InstalledAsync(db, ct))
            return false;
        if (await db.MaintenanceWorkLogs.AsNoTracking().AnyAsync(l => l.MaintenanceWorkId == work && l.MaterialIssueId != null, ct))
            return false; // Header-only legacy material attribution is not reviewable evidence.
        return !await db.MaintenanceMaterialUsages.AsNoTracking().AnyAsync(u => u.MaintenanceWorkId == work, ct)
            || (await MaintenanceUsageRead.ReadAsync(db, user, branch, work, ct)).Available;
    }
}
