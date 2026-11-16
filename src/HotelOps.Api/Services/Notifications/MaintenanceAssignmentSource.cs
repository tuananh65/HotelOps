using HotelOps.Api.Data;
using HotelOps.Api.Services.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal static class MaintenanceAssignmentSource
{
    internal const string Kind="MAINTENANCE_ASSIGNED";
    internal const string SourceType="MAINTENANCE_ASSIGNMENT";
    internal static Task<MaintenanceAssignmentReceipt?> ReadAsync(HotelOpsDbContext db,long audit,CancellationToken ct)
        => MaintenanceAssignmentStore.ReadAsync(db,audit,ct);
    internal static async Task<string?> TargetAsync(HotelOpsDbContext db,long user,MaintenanceAssignmentReceipt r,CancellationToken ct)
    {
        if(r.PreviousAssigneeId==user)
            return await MaintenanceAssignmentStore.CanReadReceiptAsync(db,user,r,ct) ? $"/maintenance-assignment-receipts/{r.AuditId}" : null;
        if(r.AssigneeId!=user) return null;
        var scope=await MaintenanceReadAccess.ReadAsync(db,user,r.BranchId,ct);
        return await scope.Apply(db.MaintenanceWorks.AsNoTracking()).AnyAsync(w=>w.MaintenanceWorkId==r.WorkId && w.IncidentId==r.IncidentId,ct)
            ? $"/operations/maintenance/{r.WorkId}?action=A004A-036" : null;
    }
}
