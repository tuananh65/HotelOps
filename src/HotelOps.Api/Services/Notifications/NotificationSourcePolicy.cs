using HotelOps.Api.Data;
using HotelOps.Api.Entities.Notifications;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

public sealed class NotificationSourcePolicy(HotelOpsDbContext db, IPermissionDataSource grants, TimeProvider clock)
{
    public const string MaintenanceView = "maintenance.view.v1";
    // Exact FS020 policy at 8d49a9087ce75fa39c89f35be9a90cd27faa3216; permission remains CLEANING_VIEW.
    public const string CleaningView = "cleaning.assignment.view.v1";
    // Uses the existing explicit-user grant data path; never impersonates a session.
    public async Task<IQueryable<NotificationIntent>> VisibleAsync(long userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        // Honor persisted expiry; no default retention period or destructive cleanup is invented.
        var query = db.NotificationIntents.AsNoTracking().Where(x => x.RecipientUserId == userId && x.State == "DELIVERED" &&
            db.Notifications.Any(n => n.NotificationId == x.NotificationId && (n.ExpiresAt == null || n.ExpiresAt > now)));
        if (!await grants.IsActiveUserAsync(userId, ct)) return query.Where(x => false);
        var sources = ReadableSources(userId)
            .Select(s => new { s.Work.MaintenanceWorkId, s.BranchId });
        // Cleaning stays hidden until an authoritative durable cycle/revision/operation binding
        // is reconciled. A successful grant predicate alone must never expose an unbound source.
        return query.Where(n => n.ReadPolicy == MaintenanceView && n.ActionPath == "/maintenance/tasks/" + n.SourceId && sources.Any(s =>
            s.MaintenanceWorkId == n.SourceId && s.BranchId == n.BranchId));
    }

    public async Task<bool> AllowsAsync(NotificationIntent intent, CancellationToken ct)
    {
        // Evaluate the full source/grant predicate in the same SQL statement as the resource read.
        var reference = NotificationSourceReference.Resolve(intent);
        if (reference is null || !await grants.IsActiveUserAsync(intent.RecipientUserId, ct)) return false;
        if (reference.SourceType == NotificationSourceReference.CleaningType)
        {
            var readable = NotificationCleaningReadQuery.Apply(db.CleaningAssignments.AsNoTracking(),
                db.Branches.AsNoTracking(), db.UserRoles.AsNoTracking(), db.RolePermissions.AsNoTracking(),
                db.UserBranchAccesses.AsNoTracking(), intent.RecipientUserId, clock.GetUtcNow().UtcDateTime);
            if (!await readable.AnyAsync(a => a.AssignmentId == intent.OccurrenceId &&
                a.CleaningTaskId == intent.SourceId && a.CleaningTask.Room.BranchId == intent.BranchId, ct)) return false;
            // C04 has no durable operation GUID/fingerprint/cycle/revision binding. Do not convert
            // infrastructure/contract unavailability into a definitive authorization denial.
            throw new NotificationContractUnavailableException();
        }
        return await ReadableSources(intent.RecipientUserId)
            .AnyAsync(s => s.Work.MaintenanceWorkId == intent.SourceId && s.BranchId == intent.BranchId &&
                db.Branches.Any(b => b.BranchId == intent.BranchId && b.IsActive), ct);
    }

    private IQueryable<MaintenanceWorkSource> ReadableSources(long userId)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        // Same ALL/BRANCH/OWN rules as MaintenanceWorkResourceResolver/PermissionDataSource.
        // Correlation keeps count, pagination and payload authorization inside SQL, even after revocation.
        return MaintenanceWorkSources.Project(db.MaintenanceWorks.AsNoTracking()).Where(s =>
            s.BranchId != null && db.Branches.Any(b=>b.BranchId==s.BranchId&&b.IsActive) &&
            db.UserRoles.Any(a=>a.UserId==userId && a.IsActive && a.Role.IsActive && a.AssignedAt<=now &&
                (a.ExpiresAt==null||a.ExpiresAt>now) && a.User.AccountStatus=="ACTIVE" && a.User.DeletedAt==null &&
                db.RolePermissions.Any(p=>p.RoleId==a.RoleId&&p.Permission.PermissionCode=="MAINTENANCE_VIEW" &&
                    ((p.DataScope=="ALL"&&a.BranchId==null) ||
                     (a.BranchId==s.BranchId && db.UserBranchAccesses.Any(b=>b.UserId==userId&&b.BranchId==s.BranchId&&b.RevokedAt==null&&b.GrantedAt<=now) &&
                      (p.DataScope=="ALL"||p.DataScope=="BRANCH"||p.DataScope=="OWN"&&s.Work.AssigneeId==userId))))));
    }
}

// Candidate adapter only. NOT registered in the production publisher catalog.
// Its occurrence is a persisted ReturnWork audit, never a client recipient or timestamp guess.
public sealed class ReturnWorkNotificationCandidate(HotelOpsDbContext db) : INotificationEventProfile
{
    public string EventType => "maintenance.returned";
    public short Version => 1;
    public async Task<ResolvedNotification> ResolveAsync(long occurrenceId, CancellationToken ct)
    {
        var audit = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(x =>
            x.AuditId == occurrenceId && x.EntityType == "maintenance_works", ct);
        if (audit?.OldValues is null || audit.NewValues is null || !long.TryParse(audit.EntityId, out var workId))
            throw new NotificationContractUnavailableException();
        using var oldState = System.Text.Json.JsonDocument.Parse(audit.OldValues);
        using var newState = System.Text.Json.JsonDocument.Parse(audit.NewValues);
        if (!oldState.RootElement.TryGetProperty("status", out var before) || before.GetString() != "COMPLETED" ||
            !newState.RootElement.TryGetProperty("status", out var after) || after.GetString() != "IN_PROGRESS")
            throw new NotificationContractUnavailableException();
        var row = await db.MaintenanceWorks.AsNoTracking().Where(x => x.MaintenanceWorkId == workId)
            .Select(x => new { x.MaintenanceWorkId, x.AssigneeId,
                BranchId = (long?)x.Incident!.BranchId, x.IncidentId }).SingleOrDefaultAsync(ct);
        if (row?.AssigneeId is not > 0 || row.BranchId is not > 0 || row.IncidentId is not > 0)
            throw new NotificationContractUnavailableException();
        // A later reassignment cannot silently retarget this historical occurrence.
        if (!newState.RootElement.TryGetProperty("assignee_id", out var assignee) ||
            assignee.GetInt64() != row.AssigneeId || audit.BranchId != row.BranchId)
            throw new NotificationContractUnavailableException();
        return new(row.MaintenanceWorkId, row.BranchId.Value, row.AssigneeId.Value,
            NotificationSourcePolicy.MaintenanceView, "Maintenance returned", "Maintenance work requires your review.",
            $"/maintenance/tasks/{row.MaintenanceWorkId}");
    }
}
