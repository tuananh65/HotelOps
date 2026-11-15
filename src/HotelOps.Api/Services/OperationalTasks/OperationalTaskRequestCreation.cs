using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    internal static byte[] RequestCreationVersion(TaskRequestLink link)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(link.Version ?? ""); }
        catch (FormatException) { throw new ValidationException("Loaded Internal Request version required."); }
        if (bytes.Length != 8 || Convert.ToBase64String(bytes) != link.Version)
            throw new ValidationException("Loaded Internal Request version required.");
        return bytes;
    }

    // Runs inside BatchAsync at both gates. Never borrow target TASK_MANAGE as
    // authority over the source request, or infer a department from the actor.
    private async Task<PermissionEvaluation> LinkedTaskCreationAuthority(long actor, long branch, long? request, CancellationToken ct)
    {
        var target = await VerificationManager(actor, branch, ct);
        if (!target.IsGranted || request == null) return target;
        if (!await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await InternalRequestTaskLinks.InstalledAsync(db, ct))
            return new(PermissionDecision.Unavailable);
        if (!await new InternalRequestProcessAuthority(db).AllowsCurrentRequestAsync(actor, request.Value, ct))
            return new(PermissionDecision.Denied);
        var now = DateTime.UtcNow;
        var scope = await new InternalRequestReadAccess(db).ReadAsync(actor, now, ct);
        return new(await scope.Apply(db.InternalRequests.AsNoTracking(), now).AnyAsync(r => r.InternalRequestId == request && r.BranchId == branch, ct)
            ? PermissionDecision.Granted : PermissionDecision.Denied);
    }

    public Task<RoomTypeBatchResult> TaskRequestContextAsync(SessionReference ticket, long sid, long branch, long request, CancellationToken ct)
    {
        if (request <= 0) throw new ValidationException("Request required.");
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            var row = await db.InternalRequests.AsNoTracking().SingleAsync(r => r.InternalRequestId == request && r.BranchId == branch, token);
            if (row.Status is not ("ACCEPTED" or "IN_PROGRESS")) return new(RoomTypeBatchStatus.Inactive);
            var version = await InternalRequestDecisionStore.RevisionAsync(db, request, token);
            return new(RoomTypeBatchStatus.Success, new TaskRequestContext(request, branch, row.ReceivingDepartmentId!.Value,
                row.Subject, row.Description, row.PriorityId, row.RoomId, row.AreaId, TaskPlanUtc(row.DueAt), Convert.ToBase64String(version)));
        }, ct, requireRoomTypeSchema: false, featurePermission: token => LinkedTaskCreationAuthority(ticket.UserId, branch, request, token));
    }

    private async Task<bool> CreatedTaskRequestLinkMatches(long request, long task, bool mandatory, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.internal_request_task_links l
            JOIN dbo.audit_logs a ON a.audit_id=l.audit_id
            WHERE l.internal_request_id={request} AND l.task_id={task} AND l.is_mandatory={mandatory}
              AND a.entity_type='internal_requests' AND a.entity_id=CONVERT(nvarchar(50),{request})
            """).SingleAsync(ct) == 1;

    private async Task WriteCreatedTaskRequestLink(long actor, long branch, long request, long task, TaskRequestLink input, DateTime now, CancellationToken ct)
    {
        var version = RequestCreationVersion(input);
        if (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET updated_at={now} WHERE internal_request_id={request} AND row_version={version} AND status IN('ACCEPTED','IN_PROGRESS')", ct) != 1)
            throw new InvalidOperationException("Locked request changed during linked creation.");
        var audit = new AuditLog { ActorUserId = actor, BranchId = branch, EntityType = "internal_requests", EntityId = request.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Action = "UPDATE", OccurredAt = now, NewValues = JsonSerializer.Serialize(new { operation = "CREATE_LINKED_OPERATIONAL_TASK", taskId = task, input.Mandatory }), ChangedFields = "[\"linked_work\"]" };
        db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_task_links(internal_request_id,task_id,is_mandatory,audit_id) VALUES({request},{task},{input.Mandatory},{audit.AuditId})", ct);
    }
}
