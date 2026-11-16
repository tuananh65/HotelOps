using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

public sealed record MaterialTaskLinkInput(long TaskId, string ExpectedRowVersion);
public sealed record MaterialTaskLinkRow(long TaskId, string Code, string Title, string? State);
public sealed record MaterialTaskLinksView(long RequestId, long BranchId, string Version, bool CanLink,
    bool Candidates, int Page, int Total, MaterialTaskLinkRow[] Items);

public sealed class MaterialTaskLinks(HotelOpsDbContext db, ISqlSessionService sessions, ICurrentUserAccessor current)
{
    private static InternalRequestReadException Error(int status, string code) => new(status, code);
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
                AND name='HotelOps.MaterialTaskLinks.V1' AND CONVERT(nvarchar(20),value)='1')
              AND (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.material_request_task_links')
                AND name IN('request_id','task_id','audit_id') AND system_type_id=127 AND is_nullable=0)=3
              AND (SELECT COUNT(*) FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
                WHERE f.parent_object_id=OBJECT_ID('dbo.material_request_task_links')
                AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=0 AND f.update_referential_action=0
                AND (f.name='FK_mr_task_request' AND f.referenced_object_id=OBJECT_ID('dbo.material_requests')
                  AND COL_NAME(c.parent_object_id,c.parent_column_id)='request_id' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)='request_id'
                 OR f.name='FK_mr_task_task' AND f.referenced_object_id=OBJECT_ID('dbo.operational_tasks')
                  AND COL_NAME(c.parent_object_id,c.parent_column_id)='task_id' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)='task_id'
                 OR f.name='FK_mr_task_audit' AND f.referenced_object_id=OBJECT_ID('dbo.audit_logs')
                  AND COL_NAME(c.parent_object_id,c.parent_column_id)='audit_id' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)='audit_id'))=3
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.material_request_task_links')
                AND name='PK_material_request_task_links' AND is_primary_key=1 AND is_disabled=0)
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;
    private IQueryable<long> Linked(long id) => db.Database.SqlQuery<long>(
        $"SELECT task_id AS Value FROM dbo.material_request_task_links WHERE request_id={id}");

    private async Task<MaterialRequest> RequestAsync(SessionReference ticket, long sid, long id, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw Error(401, "SESSION_INVALID");
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        var session = await sessions.ValidateSessionAsync(ticket, ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted) throw Error(401, "SESSION_INVALID");
        if (!await InstalledAsync(db, ct) || !await ProgressSchema.InstalledAsync(db, ct)) throw Error(503, "MR_TASK_LINK_SCHEMA_REQUIRED");
        var row = await db.MaterialRequests.FromSqlInterpolated(
            $"SELECT * FROM dbo.material_requests WITH(UPDLOCK,HOLDLOCK) WHERE request_id={id}").AsNoTracking().SingleOrDefaultAsync(ct)
            ?? throw Error(404, "MR_NOT_FOUND");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var scope = await MaterialRequestReadAccess.ReadAsync(db, ticket.UserId, row.BranchId, now, ct);
        if (!await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == id, ct)
            || !await MaterialReadProvenance.ValidAsync(db, id, row.BranchId, ct)) throw Error(404, "MR_NOT_FOUND");
        return row;
    }
    private async Task<bool> CanLink(long user, MaterialRequest request, CancellationToken ct)
    {
        if (request.RequesterId != user || request.Status is "CANCELLED" or "REJECTED") return false;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        return (await MaterialCreationAccess.BranchesAsync(db, user, now, ct)).Any(b => b.BranchId == request.BranchId);
    }
    private async Task<IQueryable<OperationalTask>> Population(long user, MaterialRequest request, bool candidates, CancellationToken ct)
    {
        var tasks = await InternalRequestTaskLinks.VisibleTasksAsync(db, user, request.BranchId, ct);
        var linked = Linked(request.RequestId);
        if (!candidates) return tasks.Where(t => linked.Contains(t.TaskId));
        var open = TaskWorkflowRead.Query(db, DateTime.UtcNow)
            .Where(s => s.State == "PENDING_ASSIGNMENT" || s.State == "ASSIGNED" || s.State == "ACCEPTED"
                || s.State == "IN_PROGRESS" || s.State == "PAUSED" || s.State == "PENDING_VERIFICATION").Select(s => s.TaskId);
        return tasks.Where(t => open.Contains(t.TaskId) && !linked.Contains(t.TaskId));
    }
    public async Task<MaterialTaskLinksView> ReadAsync(SessionReference ticket, long sid, long id, bool candidates, int page, CancellationToken ct)
    {
        if (id <= 0 || page < 1 || page > int.MaxValue / 25) throw Error(400, "INVALID_PAGE");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await RequestAsync(ticket, sid, id, ct);
        var canLink = await CanLink(ticket.UserId, request, ct);
        if (candidates && !canLink) throw Error(403, "MR_LINK_FORBIDDEN");
        var tasks = await Population(ticket.UserId, request, candidates, ct);
        var total = await tasks.CountAsync(ct);
        var rows = await tasks.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.TaskId)
            .Skip((page - 1) * 25).Take(25).Select(t => new { t.TaskId, t.TaskCode, t.Title }).ToArrayAsync(ct);
        var ids = rows.Select(t => t.TaskId).ToArray();
        var states = await TaskWorkflowRead.Query(db, DateTime.UtcNow).Where(s => ids.Contains(s.TaskId))
            .ToDictionaryAsync(s => s.TaskId, s => s.State, ct);
        request = await RequestAsync(ticket, sid, id, ct);
        var finalCanLink = await CanLink(ticket.UserId, request, ct);
        if (canLink != finalCanLink) throw Error(403, "MR_LINK_AUTHORITY_CHANGED");
        var final = await Population(ticket.UserId, request, candidates, ct);
        if (await final.CountAsync(ct) != total || await final.CountAsync(t => ids.Contains(t.TaskId), ct) != ids.Length)
            throw Error(403, "MR_TARGET_SCOPE_CHANGED");
        await tx.CommitAsync(ct);
        return new(id, request.BranchId, Convert.ToBase64String(request.RowVersion), canLink, candidates, page, total,
            rows.Select(t => new MaterialTaskLinkRow(t.TaskId, t.TaskCode, t.Title, states.GetValueOrDefault(t.TaskId))).ToArray());
    }
    public async Task<string> LinkAsync(SessionReference ticket, long sid, long id, MaterialTaskLinkInput input, CancellationToken ct)
    {
        byte[] version;
        try { version = Convert.FromBase64String(input.ExpectedRowVersion ?? ""); }
        catch (FormatException) { throw Error(400, "MR_VERSION_INVALID"); }
        if (id <= 0 || input.TaskId <= 0 || version.Length != 8 || Convert.ToBase64String(version) != input.ExpectedRowVersion)
            throw Error(400, "MR_LINK_INPUT_INVALID");
        if (db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Clean link context required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var request = await RequestAsync(ticket, sid, id, ct);
            if (!await CanLink(ticket.UserId, request, ct)) throw Error(403, "MR_LINK_FORBIDDEN");
            if (!request.RowVersion.SequenceEqual(version)) throw Error(412, "MR_VERSION_CHANGED");
            if (await Linked(id).ContainsAsync(input.TaskId, ct)) throw Error(409, "MR_TASK_ALREADY_LINKED");
            if (!await (await Population(ticket.UserId, request, true, ct)).AnyAsync(t => t.TaskId == input.TaskId, ct))
                throw Error(404, "MR_OPEN_TASK_NOT_FOUND");
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            if (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.material_requests SET updated_at={now} WHERE request_id={id} AND row_version={version}", ct) != 1)
                throw Error(412, "MR_VERSION_CHANGED");
            var audit = new AuditLog { ActorUserId = ticket.UserId, BranchId = request.BranchId,
                EntityType = "material_requests", EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Action = "UPDATE", OccurredAt = now, ChangedFields = "[\"linked_operational_tasks\"]",
                NewValues = JsonSerializer.Serialize(new { operation = "LINK_OPERATIONAL_TASK", input.TaskId }) };
            db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.material_request_task_links(request_id,task_id,audit_id) VALUES({id},{input.TaskId},{audit.AuditId})", ct);
            request = await RequestAsync(ticket, sid, id, ct);
            if (!await CanLink(ticket.UserId, request, ct)) throw Error(403, "MR_LINK_AUTHORITY_CHANGED");
            if (!await (await Population(ticket.UserId, request, false, ct)).AnyAsync(t => t.TaskId == input.TaskId, ct)) throw Error(403, "MR_TARGET_SCOPE_CHANGED");
            var state = await TaskWorkflowRead.Query(db, now).Where(s => s.TaskId == input.TaskId).Select(s => s.State).SingleOrDefaultAsync(ct);
            if (!InternalRequestTaskLinks.Open(state)) throw Error(409, "MR_TARGET_NOT_OPEN");
            await tx.CommitAsync(ct);
            return Convert.ToBase64String(request.RowVersion);
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
