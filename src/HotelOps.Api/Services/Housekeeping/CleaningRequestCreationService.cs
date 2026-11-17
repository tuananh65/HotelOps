using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    internal static byte[] CleaningRequestVersion(CleaningRequestLink link)
    {
        byte[] version;
        try { version = Convert.FromBase64String(link.Version ?? ""); }
        catch (FormatException) { throw new ValidationException("Loaded request version required."); }
        if (version.Length != 8 || Convert.ToBase64String(version) != link.Version) throw new ValidationException("Loaded request version required.");
        return version;
    }

    private async Task<PermissionEvaluation> CleaningSourceAuthority(long actor, long branch, long? request, CancellationToken ct)
    {
        var target = await permissions.EvaluateBranchAsync("CLEANING_ASSIGN", branch, ct);
        if (!target.IsGranted) return target;
        var at = clock.GetUtcNow().UtcDateTime;
        if (!await db.UserRoles.AsNoTracking().AnyAsync(a => a.UserId == actor && a.IsActive && a.Role.IsActive
            && a.Role.RoleCode == "HK_SUPERVISOR" && (a.BranchId == null || a.BranchId == branch)
            && a.AssignedAt <= at && (a.ExpiresAt == null || a.ExpiresAt > at), ct))
            return new(PermissionDecision.Denied);
        if (request == null) return target;
        if (!await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await InternalRequestCleaningLinks.InstalledAsync(db, ct)) return new(PermissionDecision.Unavailable);
        if (!await new InternalRequestProcessAuthority(db).AllowsCurrentRequestAsync(actor, request.Value, ct)) return new(PermissionDecision.Denied);
        var now = DateTime.UtcNow; var scope = await new InternalRequestReadAccess(db).ReadAsync(actor, now, ct);
        return new(await scope.Apply(db.InternalRequests.AsNoTracking(), now).AnyAsync(r => r.InternalRequestId == request && r.BranchId == branch, ct)
            ? PermissionDecision.Granted : PermissionDecision.Denied);
    }

    public Task<RoomTypeBatchResult> CleaningRequestContextAsync(SessionReference ticket, long sid, long branch, long request, CancellationToken ct)
    {
        if (request <= 0) throw new ValidationException("Request required.");
        return BatchAsync(ticket, sid, branch, async (_, token) => {
            var row = await db.InternalRequests.AsNoTracking().SingleAsync(r => r.InternalRequestId == request && r.BranchId == branch, token);
            if (row.Status is not ("ACCEPTED" or "IN_PROGRESS")) return new(RoomTypeBatchStatus.Inactive);
            return new(RoomTypeBatchStatus.Success, new CleaningRequestContext(request, branch, row.RoomId, row.PriorityId, row.Description,
                row.DueAt is {} due ? DateTime.SpecifyKind(due, DateTimeKind.Utc) : null,
                Convert.ToBase64String(await InternalRequestDecisionStore.RevisionAsync(db, request, token))));
        }, ct, featurePermission: token => CleaningSourceAuthority(ticket.UserId, branch, request, token));
    }

    private async Task WriteCleaningSourceLink(long actor, long branch, long request, long task, CleaningRequestLink input, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var version = CleaningRequestVersion(input);
        if (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET updated_at={now} WHERE internal_request_id={request} AND row_version={version} AND status IN('ACCEPTED','IN_PROGRESS')", ct) != 1)
            throw new InvalidOperationException("Locked request changed during cleaning creation.");
        var audit = new AuditLog { ActorUserId = actor, BranchId = branch, EntityType = "internal_requests", EntityId = request.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
            NewValues = JsonSerializer.Serialize(new { operation = "CREATE_LINKED_CLEANING_TASK", taskId = task, input.Mandatory }), ChangedFields = "[\"linked_work\"]" };
        db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_cleaning_links(internal_request_id,cleaning_task_id,is_mandatory,audit_id) VALUES({request},{task},{input.Mandatory},{audit.AuditId})", ct);
    }
}
