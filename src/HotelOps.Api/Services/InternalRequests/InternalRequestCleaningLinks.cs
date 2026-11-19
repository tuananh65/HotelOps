using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

// Explicit cleaning links; canonical workflow_status determines open candidates. This list is NOT a closure predicate:
// other work types and legacy source references must be reconciled separately by UC120.
public sealed class InternalRequestCleaningLinks(HotelOpsDbContext db, InternalRequestProcessAuthority authority,
    ISqlSessionService sessions, ICurrentUserAccessor current)
{
    internal static bool Open(string? state) => state is "PENDING_ASSIGNMENT" or "ASSIGNED" or "IN_PROGRESS" or "ON_HOLD" or "PENDING_INSPECTION" or "REWORK_REQUIRED";

    internal static Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) => InstalledCoreAsync(db, ct);
    private static async Task<bool> InstalledCoreAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.InternalRequestCleaningLinks.V1' AND CONVERT(nvarchar(20),value)='1')
              AND COL_LENGTH('dbo.internal_request_cleaning_links','is_mandatory')=1
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.internal_request_cleaning_links')
                AND name IN('FK_ir_cleaning_link_request','FK_ir_cleaning_link_task','FK_ir_cleaning_link_audit')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=3
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_cleaning_links')
                AND name='PK_internal_request_cleaning_links' AND is_primary_key=1 AND is_disabled=0)
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    private IQueryable<InternalRequestTaskLinkRecord> Links(long id) => db.Database.SqlQuery<InternalRequestTaskLinkRecord>($"""
        SELECT cleaning_task_id AS TaskId,is_mandatory AS Mandatory FROM dbo.internal_request_cleaning_links WHERE internal_request_id={id}
        """);

    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var session = await sessions.ValidateSessionAsync(ticket, ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
    }

    private async Task<InternalRequest> RequestAsync(SessionReference ticket, long sid, long id, CancellationToken ct)
    {
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        if (!await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await InstalledAsync(db, ct)
            || !await HousekeepingSchema.InstalledAsync(db, ct)) throw new InternalRequestReadException(503, "IR_CLEANING_LINK_SCHEMA_REQUIRED");
        // Same request lock used by routing/progress. Scope and target permissions remain separate.
        await db.InternalRequests.FromSqlInterpolated($"SELECT * FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={id}")
            .AsNoTracking().ToArrayAsync(ct);
        var now = DateTime.UtcNow;
        var scope = await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId, now, ct);
        return await scope.Apply(db.InternalRequests.AsNoTracking(), now).SingleOrDefaultAsync(r => r.InternalRequestId == id, ct)
            ?? throw new InternalRequestReadException(404, "IR_NOT_FOUND");
    }

    private IQueryable<CleaningLinkState> States() => db.Database.SqlQuery<CleaningLinkState>($"SELECT cleaning_task_id AS TaskId,workflow_status AS State FROM dbo.cleaning_tasks");
    private static async Task<bool> CanMonitorAsync(HotelOpsDbContext db, long user, long branch, DateTime now, CancellationToken ct)
    {
        var grants = await (from role in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on role.RoleId equals grant.RoleId
            where role.UserId == user && (role.Role.RoleCode == "HK_SUPERVISOR" || role.Role.RoleCode == "OPERATIONS_MANAGER")
                && grant.Permission.PermissionCode == "CLEANING_VIEW" && (role.BranchId == branch || role.BranchId == null)
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, role.BranchId,
                role.IsActive, role.Role.IsActive, role.AssignedAt, role.ExpiresAt, role.Branch == null || role.Branch.IsActive,
                db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now)))
            .ToArrayAsync(ct);
        return CleaningMonitorService.SupervisorCanMonitor(grants, branch, now);
    }

    internal static IQueryable<CleaningTask> ApplyVisibility(IQueryable<CleaningTask> tasks, long user, DateTime now, bool monitor, bool readOwn)
    {
        if (monitor) return tasks;
        if (!readOwn) return tasks.Where(t => false);
        return tasks.Where(t => t.CleaningAssignments.Count(a => a.AssigneeUserId == user && a.AssignmentStatus == "ACTIVE"
            && a.EndedAt == null && a.AssignedAt <= now) == 1);
    }
    internal static async Task<IQueryable<CleaningTask>> VisibleTasksAsync(HotelOpsDbContext db, long user, long branch, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var data = new EfPermissionDataSource(db);
        var eligible = (await HotelOps.Api.Services.Housekeeping.HousekeepingFeatureAccess.PathsAsync(db, user, "CLEANING_VIEW", now, ct))
            .Where(p => p.PermissionCode == "CLEANING_VIEW" && PermissionGrantRules.IsEligible(p, now)).ToArray();
        var wide = await CanMonitorAsync(db, user, branch, now, ct);
        var own = eligible.Any(p => PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
            || p.BranchId == branch && PermissionGrantRules.EffectiveScope(p) is "OWN" or "BRANCH");
        var tasks = db.CleaningTasks.AsNoTracking().Where(t => t.Room.BranchId == branch && t.Room.IsActive && t.Room.Branch.IsActive);
        if (!await data.IsActiveUserAsync(user, ct) || !wide && !own) return tasks.Where(t => false);
        // A raw BRANCH grant does not authorize the supervisor monitor.
        return ApplyVisibility(tasks, user, now, wide, own);
    }
    public async Task<InternalRequestTaskLinksView> ReadAsync(SessionReference ticket, long sid, long id, bool candidates, int page, CancellationToken ct)
    {
        if (id <= 0 || page < 1 || page > int.MaxValue / 25) throw new ValidationException("Invalid linked work page.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await RequestAsync(ticket, sid, id, ct);
        var canLink = request.Status is "ACCEPTED" or "IN_PROGRESS" && await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct);
        if (candidates && !canLink) throw new InternalRequestReadException(403, "IR_PROCESS_REQUIRED");
        var tasks = await VisibleTasksAsync(db, ticket.UserId, request.BranchId, ct);
        var links = Links(id);
        var states = States();
        var linked = links.Select(l => l.TaskId);
        if (candidates)
        {
            var open = states.Where(s => s.State == "PENDING_ASSIGNMENT" || s.State == "ASSIGNED" || s.State == "IN_PROGRESS" || s.State == "ON_HOLD" || s.State == "PENDING_INSPECTION" || s.State == "REWORK_REQUIRED").Select(s => s.TaskId);
            tasks = tasks.Where(t => open.Contains(t.CleaningTaskId) && !linked.Contains(t.CleaningTaskId));
        }
        else tasks = tasks.Where(t => linked.Contains(t.CleaningTaskId));
        var total = await tasks.CountAsync(ct);
        var rows = await tasks.OrderBy(t => t.CleaningTaskId).Skip((page - 1) * 25).Take(25).ToArrayAsync(ct);
        var ids = rows.Select(t => t.CleaningTaskId).ToArray();
        var stateMap = await states.Where(s => ids.Contains(s.TaskId)).ToDictionaryAsync(s => s.TaskId, s => s.State, ct);
        var metadata = await links.Where(l => ids.Contains(l.TaskId)).ToDictionaryAsync(l => l.TaskId, l => l.Mandatory, ct);
        await RequestAsync(ticket, sid, id, ct);
        var finalTasks = await VisibleTasksAsync(db, ticket.UserId, request.BranchId, ct);
        if (await finalTasks.CountAsync(t => ids.Contains(t.CleaningTaskId), ct) != ids.Length)
            throw new InternalRequestReadException(403, "IR_TARGET_SCOPE_CHANGED");
        if (candidates)
        {
            var finalOpen = States().Where(s => s.State == "PENDING_ASSIGNMENT" || s.State == "ASSIGNED" || s.State == "IN_PROGRESS" || s.State == "ON_HOLD" || s.State == "PENDING_INSPECTION" || s.State == "REWORK_REQUIRED").Select(s => s.TaskId);
            finalTasks = finalTasks.Where(t => finalOpen.Contains(t.CleaningTaskId) && !linked.Contains(t.CleaningTaskId));
        }
        else finalTasks = finalTasks.Where(t => linked.Contains(t.CleaningTaskId));
        // An expiring grant must not leak the old total on an empty or later page.
        if (await finalTasks.CountAsync(ct) != total) throw new InternalRequestReadException(403, "IR_TARGET_SCOPE_CHANGED");
        canLink = request.Status is "ACCEPTED" or "IN_PROGRESS" && await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct);
        if (candidates && !canLink) throw new InternalRequestReadException(403, "IR_PROCESS_REQUIRED");
        var version = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
        var monitor = await CanMonitorAsync(db, ticket.UserId, request.BranchId, DateTime.UtcNow, ct);
        await tx.CommitAsync(ct);
        return new(id, Convert.ToBase64String(version), canLink, rows.Select(t => new InternalRequestTaskLinkRow(t.CleaningTaskId,
            t.TaskNo, t.Instruction ?? t.TaskNo, stateMap.GetValueOrDefault(t.CleaningTaskId), metadata.GetValueOrDefault(t.CleaningTaskId),
            $"/cleaning/tasks/{t.CleaningTaskId}?branchId={request.BranchId}&{(monitor ? "monitor=1&kind=cleaning" : "execution=1")}")).ToArray(), total, page, candidates, request.BranchId);
    }

    public async Task<string> LinkAsync(SessionReference ticket, long sid, long id, byte[] version, InternalRequestTaskLinkInput input, CancellationToken ct)
    {
        if (id <= 0 || input.TaskId <= 0 || version.Length != 8) throw new ValidationException("Request, task and loaded version required.");
        if (db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Linking requires a clean context.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var request = await RequestAsync(ticket, sid, id, ct);
            if (!await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct)) throw new InternalRequestReadException(403, "IR_PROCESS_REQUIRED");
            if (!(await InternalRequestDecisionStore.RevisionAsync(db, id, ct)).SequenceEqual(version))
                throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            if (request.Status is not ("ACCEPTED" or "IN_PROGRESS")) throw new InternalRequestReadException(409, "IR_LINK_STATE_INVALID");
            var tasks = await VisibleTasksAsync(db, ticket.UserId, request.BranchId, ct);
            if (!await tasks.AnyAsync(t => t.CleaningTaskId == input.TaskId, ct)) throw new InternalRequestReadException(404, "IR_TARGET_NOT_FOUND");
            var state = await States().Where(s => s.TaskId == input.TaskId).Select(s => s.State).SingleOrDefaultAsync(ct);
            if (!Open(state)) throw new InternalRequestReadException(409, "IR_TARGET_NOT_OPEN");
            if (await Links(id).AnyAsync(l => l.TaskId == input.TaskId, ct)) throw new InternalRequestReadException(409, "IR_TASK_ALREADY_LINKED");
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET updated_at={now} WHERE internal_request_id={id} AND row_version={version}", ct);
            if (changed != 1) throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            var audit = new AuditLog { ActorUserId = ticket.UserId, BranchId = request.BranchId, EntityType = "internal_requests",
                EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
                NewValues = JsonSerializer.Serialize(new { operation = "LINK_CLEANING_TASK", input.TaskId, input.Mandatory }), ChangedFields = "[\"linked_work\"]" };
            db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_cleaning_links(internal_request_id,cleaning_task_id,is_mandatory,audit_id) VALUES({id},{input.TaskId},{input.Mandatory},{audit.AuditId})", ct);
            await RequestAsync(ticket, sid, id, ct);
            if (!await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct)) throw new InternalRequestReadException(403, "IR_AUTHORITY_CHANGED");
            tasks = await VisibleTasksAsync(db, ticket.UserId, request.BranchId, ct);
            if (!await tasks.AnyAsync(t => t.CleaningTaskId == input.TaskId, ct)) throw new InternalRequestReadException(403, "IR_TARGET_SCOPE_CHANGED");
            var result = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
            await tx.CommitAsync(ct); return Convert.ToBase64String(result);
        }
        finally { db.ChangeTracker.Clear(); }
    }
}

internal sealed class CleaningLinkState { public long TaskId { get; set; } public string? State { get; set; } }
