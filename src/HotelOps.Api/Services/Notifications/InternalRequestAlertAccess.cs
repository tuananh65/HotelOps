using HotelOps.Api.Data;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal static class InternalRequestAlertAccess
{
    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, long user, WorkAlertSourceRow episode, CancellationToken ct)
    {
        if (episode.SourceType != "INTERNAL_REQUEST") return false;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.UserId == user && u.DeletedAt == null
            && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE", ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == user && a.BranchId == episode.BranchId
                && a.RevokedAt == null && a.GrantedAt <= now && a.Branch.IsActive, ct)) return false;
        var scope = await new InternalRequestReadAccess(db).ReadAsync(user, now, ct);
        var request = await scope.Apply(db.InternalRequests.AsNoTracking(), now)
            .Where(r => r.InternalRequestId == episode.SourceId && r.BranchId == episode.BranchId)
            .Select(r => new { r.ReceivingDepartmentId }).SingleOrDefaultAsync(ct);
        if (request == null) return false;
        var assignments = db.InternalRequestAssignments.AsNoTracking().Where(a => a.InternalRequestId == episode.SourceId
            && a.Status == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now);
        if (await assignments.CountAsync(ct) == 1 && await assignments.AnyAsync(a => a.AssigneeId == user, ct)) return true;
        if (await InternalRequestEscalationService.InstalledAsync(db, ct)
            && await db.Database.SqlQuery<int>($"""
                SELECT 1 AS Value FROM dbo.internal_request_escalations
                WHERE internal_request_id={episode.SourceId} AND state='OPEN'
                """).AnyAsync(ct)
            && await new InternalRequestEscalationAuthority(db).AllowsAsync(user, episode.SourceId, ct)) return true;
        return request.ReceivingDepartmentId is { } department
            && await new InternalRequestProcessAuthority(db).InstalledAsync(ct)
            && await new InternalRequestProcessAuthority(db).AllowsDepartmentAsync(user, episode.BranchId, department, ct);
    }
}
