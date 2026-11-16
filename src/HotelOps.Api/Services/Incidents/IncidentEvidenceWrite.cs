using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

public sealed record IncidentEvidenceInput(string? Note, IReadOnlyList<EvidenceUpload>? Files);
public sealed record IncidentEvidenceSaved(long IncidentId, long EventId, string Version, string Message);

public sealed class IncidentEvidenceWrite(HotelOpsDbContext db, ISqlSessionService sessions, ICurrentUserAccessor current)
{
    internal static IncidentEvidenceInput Validate(IncidentEvidenceInput input)
    {
        var note = input.Note?.Trim();
        if (note?.Length > 4000 || note?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true
            || input.Files?.Count > 5 || string.IsNullOrEmpty(note) && !(input.Files?.Count > 0))
            throw new ValidationException("Enter a note (at most 4000 characters) or up to five complete evidence files.");
        foreach (var file in input.Files ?? [])
            try { InternalRequestEvidence.Validate(file); }
            catch (ValidationException) { throw new InternalRequestReadException(400, "MSG23"); }
        return input with { Note = string.IsNullOrEmpty(note) ? null : note };
    }

    internal static async Task<string> VersionAsync(HotelOpsDbContext db, Incident incident, CancellationToken ct)
    {
        var lastEvent = await db.IncidentEvents.Where(e => e.IncidentId == incident.IncidentId).MaxAsync(e => (long?)e.EventId, ct) ?? 0;
        var lastFile = await db.Attachments.Where(a => a.EntityType == "INCIDENT" && a.EntityId == incident.IncidentId)
            .MaxAsync(a => (long?)a.AttachmentId, ct) ?? 0;
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { incident.IncidentId, incident.Status, lastEvent, lastFile }))));
    }

    internal static async Task<bool> CanAddAsync(HotelOpsDbContext db, long actor, Incident incident, CancellationToken ct)
    {
        if (incident.Status is not ("OPEN" or "IN_PROGRESS" or "RESOLVED")) return false;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var grants = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == actor && assignment.BranchId == incident.BranchId
                && grant.Permission.PermissionCode == "INCIDENT_EVIDENCE_ADD"
                && assignment.User.AccountStatus == "ACTIVE" && assignment.User.EmploymentStatus == "ACTIVE" && assignment.User.DeletedAt == null
            select new IncidentReadAccess.Grant(assignment.Role.RoleCode, new PermissionGrantPath(grant.Permission.PermissionCode,
                grant.DataScope, assignment.BranchId, assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(b => b.UserId == actor && b.BranchId == incident.BranchId && b.RevokedAt == null && b.GrantedAt <= now))))
            .ToArrayAsync(ct);
        var assigned = await IncidentCurrentAssignment.AssignedTo(db.Incidents.AsNoTracking(),actor,true)
            .AnyAsync(i=>i.IncidentId==incident.IncidentId,ct);
        return Allows(grants, now, incident.BranchId, incident.ReportedBy == actor, assigned);
    }

    internal static bool Allows(IEnumerable<IncidentReadAccess.Grant> grants, DateTime now, long branch, bool reporter, bool assigned)
    {
        var valid = grants.Where(g => g.Path.PermissionCode == "INCIDENT_EVIDENCE_ADD" && g.Path.BranchId == branch
            && PermissionGrantRules.IsEligible(g.Path, now)).ToArray();
        if (valid.Any(g => g.Role is "MAINTENANCE_LEAD" or "OPERATIONS_MANAGER" && PermissionGrantRules.EffectiveScope(g.Path) == "BRANCH")) return true;
        if (!valid.Any(g => g.Role is "SYS_ADMIN" or "OPERATIONS_MANAGER" or "HK_SUPERVISOR" or "HK_STAFF" or "MAINTENANCE_LEAD"
                or "MAINTENANCE_STAFF" or "STOREKEEPER" or "DEPT_MANAGER" && PermissionGrantRules.EffectiveScope(g.Path) is "OWN" or "BRANCH")) return false;
        return reporter || assigned;
    }

    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid || actor.IsRestricted || ticket.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
        var result = await sessions.ValidateSessionAsync(ticket, ct);
        if (!result.IsValid || result.Session?.SessionId != sid || result.Session.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
    }

    public async Task<IncidentEvidenceSaved> AddAsync(SessionReference ticket, long sid, long id, string version, IncidentEvidenceInput input, string root, CancellationToken ct)
    {
        input = Validate(input);
        if (db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Evidence write requires a clean context.");
        using var files = new EvidenceFileLifetime();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        var incident = await db.Incidents.FromSqlInterpolated($"SELECT * FROM dbo.incidents WITH(UPDLOCK,HOLDLOCK) WHERE incident_id={id}")
            .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new InternalRequestReadException(404, "INCIDENT_NOT_FOUND");
        async Task Authorize()
        {
            var scope = await IncidentReadAccess.ReadAsync(db, ticket.UserId, incident.BranchId, ct, sourceRecord: true);
            if (!await scope.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == id, ct)) throw new InternalRequestReadException(404, "INCIDENT_NOT_FOUND");
            if (incident.Status is "CLOSED" or "CANCELLED") throw new InternalRequestReadException(409, "MSG24");
            if (!await CanAddAsync(db, ticket.UserId, incident, ct)) throw new InternalRequestReadException(403, "INCIDENT_EVIDENCE_ADD_REQUIRED");
        }
        await Authorize();
        if (await VersionAsync(db, incident, ct) != version) throw new InternalRequestReadException(412, "INCIDENT_CHANGED");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        await StageFilesAsync(db, id, ticket.UserId, now, input.Files, root, files, ct);
        var entry = new IncidentEvent { IncidentId = id, EventType = "EVIDENCE_ADDED", FromStatus = incident.Status, ToStatus = incident.Status,
            ActorId = ticket.UserId, OccurredAt = now, Note = input.Note };
        db.IncidentEvents.Add(entry);
        await db.SaveChangesAsync(ct);
        var attachmentIds = db.Attachments.Local.Select(a => a.AttachmentId).ToArray();
        db.AuditLogs.Add(new AuditLog { BranchId = incident.BranchId, ActorUserId = ticket.UserId, EntityType = "incidents", EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Action = "UPDATE", OccurredAt = now, ChangedFields = "[\"evidence\"]", NewValues = JsonSerializer.Serialize(new { operation = "ADD_EVIDENCE", entry.EventId, input.Note, attachmentIds }) });
        await db.SaveChangesAsync(ct);
        await SessionAsync(ticket, sid, ct); await Authorize();
        var result = new IncidentEvidenceSaved(id, entry.EventId, await VersionAsync(db, incident, ct), "MSG08");
        await files.BeforeCommitAsync(ct); await tx.CommitAsync(ct);
        return result;
    }
    internal static async Task StageFilesAsync(HotelOpsDbContext db, long id, long actor, DateTime now,
        IReadOnlyList<EvidenceUpload>? uploads, string root, EvidenceFileLifetime files, CancellationToken ct)
    {
        foreach (var upload in uploads ?? [])
        {
            var (bytes, extension) = InternalRequestEvidence.Validate(upload);
            var directory = Path.GetFullPath(root); Directory.CreateDirectory(directory);
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new ValidationException("Private storage unavailable.");
            var key = Guid.NewGuid().ToString("N") + extension; var path = Path.Combine(directory, key);
            await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            { files.Files.Add(path); await stream.WriteAsync(bytes, ct); await stream.FlushAsync(ct); }
            db.Attachments.Add(new Attachment { EntityType = "INCIDENT", EntityId = id, FileName = key, OriginalFileName = upload.Name,
                MimeType = upload.MimeType, FileSizeBytes = bytes.Length, StorageKey = key, UploadedBy = actor, UploadedAt = now });
        }
    }
}
