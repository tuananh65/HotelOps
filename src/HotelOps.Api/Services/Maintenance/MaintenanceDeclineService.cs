using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed record MaintenanceDeclineInput(string? Reason);
public sealed record MaintenanceDeclineSaved(long MaintenanceWorkId, long AuditId, string Workflow, DateTime DeclinedAt);

public sealed class MaintenanceDeclineService(HotelOpsDbContext db, ISqlSessionService sessions, ICurrentUserAccessor current)
{
    internal static string ValidateReason(string? input)
    {
        var value = input?.Trim();
        if (value == null || value.Length is < 10 or > 2000
            || value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ValidationException("Decline reason must contain 10–2000 characters.");
        return value;
    }

    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var result = await sessions.ValidateSessionAsync(ticket, ct);
        if (!result.IsValid || result.Session?.SessionId != sid || result.Session.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
    }

    public async Task<MaintenanceDeclineSaved> DeclineAsync(SessionReference ticket, long sid, long id,
        string tag, MaintenanceDeclineInput input, CancellationToken ct)
    {
        var reason = ValidateReason(input.Reason);
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Decline requires a clean owning context.");
        // Discover without retaining a Work lock before the canonical User -> Incident -> Work order.
        var incidentId = await db.MaintenanceWorks.AsNoTracking().Where(w => w.MaintenanceWorkId == id)
            .Select(w => w.IncidentId).SingleOrDefaultAsync(ct) ?? throw new InternalRequestReadException(404, "WORK_NOT_FOUND");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        var incident = await db.Incidents.FromSqlInterpolated($"SELECT * FROM dbo.incidents WITH(UPDLOCK,HOLDLOCK) WHERE incident_id={incidentId}")
            .AsNoTracking().SingleAsync(ct);
        var work = await db.MaintenanceWorks.FromSqlInterpolated($"SELECT * FROM dbo.maintenance_works WITH(UPDLOCK,HOLDLOCK) WHERE maintenance_work_id={id}")
            .AsTracking().SingleAsync(ct);
        if (work.IncidentId != incidentId || !await MaintenanceExecutionAccess.AllowsAsync(db, ticket.UserId, incident.BranchId, id, ct))
            throw new InternalRequestReadException(403, "CURRENT_ASSIGNEE_REQUIRED");
        if (!await MaintenanceDeclineStore.InstalledAsync(db, ct)) throw new InternalRequestReadException(503, "DECLINE_STORAGE_UNAVAILABLE");
        var currentTag = await MaintenanceExecutionVersion.TagAsync(db, id, ct)
            ?? throw new InternalRequestReadException(503, "VERSION_STORAGE_UNAVAILABLE");
        if (currentTag != tag) throw new InternalRequestReadException(412, "WORK_CHANGED");
        if (await MaintenanceAssignmentProof.ReadAsync(db, id, ct) != "ASSIGNED"
            || work.AssigneeId != ticket.UserId || work.ReviewedAt != null || incident.ResolvedAt != null || incident.ClosedAt != null
            || await db.MaintenanceWorkLogs.AsNoTracking().AnyAsync(l => l.MaintenanceWorkId == id, ct))
            throw new InternalRequestReadException(409, "ASSIGNED_UNSTARTED_WORK_REQUIRED");
        var now = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct), DateTimeKind.Utc);
        var audit = new AuditLog { EntityType = "maintenance_works", EntityId = id.ToString(CultureInfo.InvariantCulture),
            BranchId = incident.BranchId, ActorUserId = ticket.UserId, Action = "UPDATE", OccurredAt = now,
            ChangedFields = "[\"assignee_id\",\"decline_receipt\"]",
            OldValues = JsonSerializer.Serialize(new { maintenance_work_id = id, incident_id = incidentId, status = "PENDING", assignee_id = ticket.UserId }),
            NewValues = JsonSerializer.Serialize(new { maintenance_work_id = id, incident_id = incidentId, status = "PENDING",
                assignee_id = (long?)null, previous_assignee_id = ticket.UserId, reason,
                notification_kind = "MAINTENANCE_DECLINED", notification_version = 1 }) };
        work.AssigneeId = null;
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(ct);
        await MaintenanceDeclineStore.WriteAsync(db, audit.AuditId, id, incidentId, ticket.UserId, tag, reason, now, ct);
        // Current grants remain mandatory, but declining does not grant continued access to the work.
        await SessionAsync(ticket, sid, ct);
        var read = await MaintenanceReadAccess.ReadAsync(db, ticket.UserId, incident.BranchId, ct);
        if ((!read.Lead && !read.Staff) || !await MaintenanceExecutionAccess.GrantsAsync(db, ticket.UserId, incident.BranchId, ct))
            throw new InternalRequestReadException(403, "AUTHORITY_CHANGED");
        if (await MaintenanceAssignmentProof.ReadAsync(db, id, ct) != "PENDING_ASSIGNMENT")
            throw new InternalRequestReadException(409, "WORKFLOW_PROOF_CHANGED");
        await tx.CommitAsync(ct);
        return new(id, audit.AuditId, "PENDING_ASSIGNMENT", now);
    }
}
