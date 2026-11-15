using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

public sealed record InternalRequestTaskLinkInput(long TaskId, [property: JsonRequired] bool Mandatory);
public sealed record InternalRequestTaskLinkRow(long TaskId, string Code, string Title, string? State, bool Mandatory, string Path);
public sealed record InternalRequestTaskLinksView(long RequestId, string Version, bool CanLink,
    InternalRequestTaskLinkRow[] Items, int Total, int Page, bool Candidates);
internal sealed class InternalRequestTaskLinkRecord
{
    public long TaskId { get; set; }
    public bool Mandatory { get; set; }
}

// Explicit metadata for operational tasks only. This list is NOT a closure predicate:
// other work types and legacy source references must be reconciled separately by UC120.
public sealed class InternalRequestTaskLinks(HotelOpsDbContext db, InternalRequestProcessAuthority authority,
    ISqlSessionService sessions, ICurrentUserAccessor current)
{
    internal static bool Open(string? state) => state is "PENDING_ASSIGNMENT" or "ASSIGNED" or "ACCEPTED"
        or "IN_PROGRESS" or "PAUSED" or "PENDING_VERIFICATION";

    internal static Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) => InstalledCoreAsync(db, ct);
    private static async Task<bool> InstalledCoreAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.InternalRequestTaskLinks.V1' AND CONVERT(nvarchar(20),value)='1')
              AND COL_LENGTH('dbo.internal_request_task_links','is_mandatory')=1
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.internal_request_task_links')
                AND name IN('FK_ir_task_link_request','FK_ir_task_link_task','FK_ir_task_link_audit')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=3
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_task_links')
                AND name='PK_internal_request_task_links' AND is_primary_key=1 AND is_disabled=0)
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    private IQueryable<InternalRequestTaskLinkRecord> Links(long id) => db.Database.SqlQuery<InternalRequestTaskLinkRecord>($"""
        SELECT task_id AS TaskId,is_mandatory AS Mandatory FROM dbo.internal_request_task_links WHERE internal_request_id={id}
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
            || !await ProgressSchema.InstalledAsync(db, ct)) throw new InternalRequestReadException(503, "IR_TASK_LINK_SCHEMA_REQUIRED");
        // Same request lock used by routing/progress. Scope and target permissions remain separate.
        await db.InternalRequests.FromSqlInterpolated($"SELECT * FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={id}")
            .AsNoTracking().ToArrayAsync(ct);
        var now = DateTime.UtcNow;
        var scope = await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId, now, ct);
        return await scope.Apply(db.InternalRequests.AsNoTracking(), now).SingleOrDefaultAsync(r => r.InternalRequestId == id, ct)
            ?? throw new InternalRequestReadException(404, "IR_NOT_FOUND");
    }

    private async Task<IQueryable<OperationalTask>> VisibleTasksAsync(long user, long branch, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var data = new EfPermissionDataSource(db);
        var paths = await data.ReadPathsAsync(user, "TASK_VIEW", now, ct);
        var eligible = paths.Where(p => p.PermissionCode == "TASK_VIEW" && PermissionGrantRules.IsEligible(p, now)).ToArray();
        var wide = eligible.Any(p => PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
            || p.BranchId == branch && PermissionGrantRules.EffectiveScope(p) == "BRANCH");
        var own = eligible.Any(p => p.BranchId == branch && PermissionGrantRules.EffectiveScope(p) == "OWN");
        var tasks = db.OperationalTasks.AsNoTracking().Where(t => t.BranchId == branch && t.Branch.IsActive
            && t.Department.BranchId == branch && (t.RoomId == null || t.Room!.BranchId == branch)
            && (t.AreaId == null || t.Area!.BranchId == branch));
        if (!await data.IsActiveUserAsync(user, ct) || !wide && !own) return tasks.Where(t => false);
        if (wide) return tasks;
        // Match the existing task read contract, including verified own closed tasks.
        if (await TaskVerificationStore.InstalledAsync(db, ct))
        {
            var closedOwn = TaskVerificationStore.ClosedTaskIds(db, user);
            return tasks.Where(t => t.OperationalTaskAssignments.Count(a => a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now) == 1
                && t.OperationalTaskAssignments.Any(a => a.AssigneeId == user && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now)
                || closedOwn.Contains(t.TaskId));
        }
        return tasks.Where(t => t.OperationalTaskAssignments.Count(a => a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now) == 1
            && t.OperationalTaskAssignments.Any(a => a.AssigneeId == user && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now));
    }

    public async Task<InternalRequestTaskLinksView> ReadAsync(SessionReference ticket, long sid, long id, bool candidates, int page, CancellationToken ct)
    {
        if (id <= 0 || page < 1 || page > int.MaxValue / 25) throw new ValidationException("Invalid linked work page.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await RequestAsync(ticket, sid, id, ct);
        var canLink = request.Status is "ACCEPTED" or "IN_PROGRESS" && await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct);
        if (candidates && !canLink) throw new InternalRequestReadException(403, "IR_PROCESS_REQUIRED");
        var tasks = await VisibleTasksAsync(ticket.UserId, request.BranchId, ct);
        var links = Links(id);
        var states = TaskWorkflowRead.Query(db, DateTime.UtcNow);
        var linked = links.Select(l => l.TaskId);
        if (candidates)
        {
            var open = states.Where(s => s.State != "CLOSED" && s.State != "CANCELLED").Select(s => s.TaskId);
            tasks = tasks.Where(t => open.Contains(t.TaskId) && !linked.Contains(t.TaskId));
        }
        else tasks = tasks.Where(t => linked.Contains(t.TaskId));
        var total = await tasks.CountAsync(ct);
        var rows = await tasks.OrderBy(t => t.TaskId).Skip((page - 1) * 25).Take(25).ToArrayAsync(ct);
        var ids = rows.Select(t => t.TaskId).ToArray();
        var stateMap = await states.Where(s => ids.Contains(s.TaskId)).ToDictionaryAsync(s => s.TaskId, s => s.State, ct);
        var metadata = await links.Where(l => ids.Contains(l.TaskId)).ToDictionaryAsync(l => l.TaskId, l => l.Mandatory, ct);
        await RequestAsync(ticket, sid, id, ct);
        var finalTasks = await VisibleTasksAsync(ticket.UserId, request.BranchId, ct);
        if (await finalTasks.CountAsync(t => ids.Contains(t.TaskId), ct) != ids.Length)
            throw new InternalRequestReadException(403, "IR_TARGET_SCOPE_CHANGED");
        if (candidates)
        {
            var finalOpen = TaskWorkflowRead.Query(db, DateTime.UtcNow).Where(s => s.State != "CLOSED" && s.State != "CANCELLED").Select(s => s.TaskId);
            finalTasks = finalTasks.Where(t => finalOpen.Contains(t.TaskId) && !linked.Contains(t.TaskId));
        }
        else finalTasks = finalTasks.Where(t => linked.Contains(t.TaskId));
        // An expiring grant must not leak the old total on an empty or later page.
        if (await finalTasks.CountAsync(ct) != total) throw new InternalRequestReadException(403, "IR_TARGET_SCOPE_CHANGED");
        canLink = request.Status is "ACCEPTED" or "IN_PROGRESS" && await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct);
        if (candidates && !canLink) throw new InternalRequestReadException(403, "IR_PROCESS_REQUIRED");
        var version = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
        await tx.CommitAsync(ct);
        return new(id, Convert.ToBase64String(version), canLink, rows.Select(t => new InternalRequestTaskLinkRow(t.TaskId,
            t.TaskCode, t.Title, stateMap.GetValueOrDefault(t.TaskId), metadata.GetValueOrDefault(t.TaskId),
            $"/operational-tasks?branchId={request.BranchId}&taskId={t.TaskId}")).ToArray(), total, page, candidates);
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
            var tasks = await VisibleTasksAsync(ticket.UserId, request.BranchId, ct);
            if (!await tasks.AnyAsync(t => t.TaskId == input.TaskId, ct)) throw new InternalRequestReadException(404, "IR_TARGET_NOT_FOUND");
            var state = await TaskWorkflowRead.Query(db, DateTime.UtcNow).Where(s => s.TaskId == input.TaskId).Select(s => s.State).SingleOrDefaultAsync(ct);
            if (!Open(state)) throw new InternalRequestReadException(409, "IR_TARGET_NOT_OPEN");
            if (await Links(id).AnyAsync(l => l.TaskId == input.TaskId, ct)) throw new InternalRequestReadException(409, "IR_TASK_ALREADY_LINKED");
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET updated_at={now} WHERE internal_request_id={id} AND row_version={version}", ct);
            if (changed != 1) throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            var audit = new AuditLog { ActorUserId = ticket.UserId, BranchId = request.BranchId, EntityType = "internal_requests",
                EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
                NewValues = JsonSerializer.Serialize(new { operation = "LINK_OPERATIONAL_TASK", input.TaskId, input.Mandatory }), ChangedFields = "[\"linked_work\"]" };
            db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_task_links(internal_request_id,task_id,is_mandatory,audit_id) VALUES({id},{input.TaskId},{input.Mandatory},{audit.AuditId})", ct);
            await RequestAsync(ticket, sid, id, ct);
            if (!await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct)) throw new InternalRequestReadException(403, "IR_AUTHORITY_CHANGED");
            tasks = await VisibleTasksAsync(ticket.UserId, request.BranchId, ct);
            if (!await tasks.AnyAsync(t => t.TaskId == input.TaskId, ct)) throw new InternalRequestReadException(403, "IR_TARGET_SCOPE_CHANGED");
            var result = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
            await tx.CommitAsync(ct); return Convert.ToBase64String(result);
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
