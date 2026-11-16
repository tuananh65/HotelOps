using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Incidents;

public sealed class IncidentReopenOptions
{
    // SYS-PAR-19 default in SRS; configured on the server, never by the caller.
    public int WindowDays { get; set; } = 30;
}
public sealed record IncidentReopenInput(string Reason, DateTime RecurrenceDiscoveredAt, IncidentEvidenceInput Evidence);
public sealed record IncidentReopenContext(long IncidentId, long PreviousWorkId, DateTime ClosedAt,
    string ClosureNote, DateTime EvaluatedAt, int WindowDays, string Version);
public sealed record IncidentReopenSaved(long IncidentId, long EventId, string Workflow, string Version);

public sealed class IncidentReopenService(HotelOpsDbContext db, ISqlSessionService sessions,
    ICurrentUserAccessor current, IOptions<IncidentReopenOptions> options)
{
    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var result = await sessions.ValidateSessionAsync(ticket, ct);
        if (!result.IsValid || result.Session?.SessionId != sid || result.Session.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
    }

    private async Task AuthorizeAsync(long user, Incident incident, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var paths = await (from role in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on role.RoleId equals grant.RoleId
            where role.UserId == user && role.BranchId == incident.BranchId && role.Role.RoleCode == "MAINTENANCE_LEAD"
                && grant.Permission.PermissionCode == "INCIDENT_MANAGE" && role.User.AccountStatus == "ACTIVE"
                && role.User.EmploymentStatus == "ACTIVE" && role.User.DeletedAt == null
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, role.BranchId,
                role.IsActive, role.Role.IsActive, role.AssignedAt, role.ExpiresAt, role.Branch != null && role.Branch.IsActive,
                db.UserBranchAccesses.Any(b => b.UserId == user && b.BranchId == incident.BranchId && b.RevokedAt == null && b.GrantedAt <= now)))
            .ToArrayAsync(ct);
        if (!paths.Any(p => PermissionGrantRules.IsEligible(p, now) && PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "GLOBAL"))
            throw new InternalRequestReadException(403, "SCOPED_INCIDENT_MANAGE_REQUIRED");
        var read = await IncidentReadAccess.ReadAsync(db, user, incident.BranchId, ct, sourceRecord: true);
        if (!await read.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == incident.IncidentId, ct))
            throw new InternalRequestReadException(404, "INCIDENT_UNAVAILABLE");
        if (!await IncidentReopenStore.InstalledAsync(db, ct))
            throw new InternalRequestReadException(503, "INCIDENT_CYCLE_STORAGE_UNAVAILABLE");
    }

    private async Task<Incident> LockAsync(long id, CancellationToken ct)
        => await db.Incidents.FromSqlInterpolated($"SELECT * FROM dbo.incidents WITH(UPDLOCK,HOLDLOCK) WHERE incident_id={id}")
            .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new InternalRequestReadException(404, "INCIDENT_UNAVAILABLE");

    public async Task<IncidentReopenContext> ContextAsync(SessionReference ticket, long sid, long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        var incident = await LockAsync(id, ct);
        await AuthorizeAsync(ticket.UserId, incident, ct);
        var closure = await IncidentReopenSource.ReadAsync(db, id, options.Value.WindowDays, ct);
        await SessionAsync(ticket, sid, ct);
        await AuthorizeAsync(ticket.UserId, incident, ct);
        await tx.CommitAsync(ct);
        return new(id, closure.WorkId, closure.ClosedAtUtc, closure.Note, closure.EvaluatedAtUtc, options.Value.WindowDays, closure.ETag);
    }

    public async Task<IncidentReopenSaved> ReopenAsync(SessionReference ticket, long sid, long id, string tag,
        IncidentReopenInput input, string evidenceRoot, CancellationToken ct)
    {
        var reason = input.Reason?.Trim();
        if (reason == null || reason.Length is < 10 or > 4000 || reason.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ValidationException("Reopen reason must contain 10 to 4000 characters.");
        if (input.Evidence == null) throw new ValidationException("Evidence of recurrence is required.");
        var evidence = IncidentEvidenceWrite.Validate(input.Evidence);
        if (input.RecurrenceDiscoveredAt.Kind != DateTimeKind.Utc)
            throw new ValidationException("Recurrence discovery must be expressed in UTC.");
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Reopen requires a clean context.");
        using var files = new EvidenceFileLifetime();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        var incident = await LockAsync(id, ct);
        await AuthorizeAsync(ticket.UserId, incident, ct);
        if (await IncidentTriageStore.TagAsync(db, id, ct) != tag) throw new InternalRequestReadException(412, "INCIDENT_CHANGED");
        var closure = await IncidentReopenSource.ReadAsync(db, id, options.Value.WindowDays, ct);
        if (input.RecurrenceDiscoveredAt < closure.ClosedAtUtc || input.RecurrenceDiscoveredAt > closure.EvaluatedAtUtc)
            throw new ValidationException("Recurrence discovery must be between the previous closure and current server time.");
        var now = closure.EvaluatedAtUtc;
        await IncidentEvidenceWrite.StageFilesAsync(db, id, ticket.UserId, now, evidence.Files, evidenceRoot, files, ct);
        var entry = new IncidentEvent { IncidentId = id, EventType = "REOPENED", FromStatus = "CLOSED", ToStatus = "OPEN",
            ActorId = ticket.UserId, OccurredAt = now, Note = reason };
        db.IncidentEvents.Add(entry);
        if (evidence.Note != null) db.IncidentEvents.Add(new IncidentEvent { IncidentId = id, EventType = "EVIDENCE_ADDED",
            FromStatus = "CLOSED", ToStatus = "OPEN", ActorId = ticket.UserId, OccurredAt = now, Note = evidence.Note });
        await db.SaveChangesAsync(ct);
        var audit = new AuditLog { EntityType = "incidents", EntityId = id.ToString(CultureInfo.InvariantCulture), Action = "UPDATE",
            BranchId = incident.BranchId, ActorUserId = ticket.UserId, OccurredAt = now,
            OldValues = JsonSerializer.Serialize(new { status = "CLOSED", closed_at = closure.ClosedAtUtc, closure_audit_id = closure.AuditId,
                previous_work_id = closure.WorkId, resolved_at = incident.ResolvedAt, closure_note = closure.Note,
                response_deadline_at = closure.PreviousSla.ResponseDeadlineAt, resolution_deadline_at = closure.PreviousSla.ResolutionDeadlineAt,
                severity_id = incident.SeverityId, priority_id = incident.PriorityId }),
            NewValues = JsonSerializer.Serialize(new { status = "OPEN", operation = "REOPEN", workflow = "PENDING_TRIAGE", event_id = entry.EventId,
                recurrence_discovered_at = input.RecurrenceDiscoveredAt, reason, evidence_note = evidence.Note,
                attachment_ids = db.Attachments.Local.Select(a => a.AttachmentId).ToArray(), consumed_version = tag,
                previous_technician_id = closure.TechnicianId, notification_kind = "INCIDENT_REOPENED", notification_version = 1,
                reopen_window_days = options.Value.WindowDays }),
            ChangedFields = "[\"status\",\"resolved_at\",\"closed_at\",\"closure_note\",\"response_deadline_at\",\"resolution_deadline_at\"]" };
        db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct);
        await IncidentReopenStore.AppendAsync(db, closure, entry.EventId, audit.AuditId, input.RecurrenceDiscoveredAt, now, ct);
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.incidents SET status='OPEN',resolved_at=NULL,closed_at=NULL,closure_note=NULL,
              response_deadline_at=NULL,resolution_deadline_at=NULL
            WHERE incident_id={id} AND status='CLOSED' AND row_version=CONVERT(binary(8),{tag.Substring(1,16)},2)
            """, ct);
        if (changed != 1) throw new InternalRequestReadException(412, "INCIDENT_CHANGED");
        await SessionAsync(ticket, sid, ct);
        var reopened = await LockAsync(id, ct);
        await AuthorizeAsync(ticket.UserId, reopened, ct);
        if (await IncidentAssignmentWorkflow.ReadAsync(db, reopened, ct) != "PENDING_TRIAGE")
            throw new InternalRequestReadException(409, "INCIDENT_CYCLE_PROVENANCE_INVALID");
        var result = new IncidentReopenSaved(id, entry.EventId, "PENDING_TRIAGE",
            await IncidentTriageStore.TagAsync(db, id, ct) ?? throw new InternalRequestReadException(503, "INCIDENT_VERSION_UNAVAILABLE"));
        await files.BeforeCommitAsync(ct); await tx.CommitAsync(ct);
        return result;
    }
}
