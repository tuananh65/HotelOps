using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

public sealed record IncidentClassificationInput(long CategoryId, long SeverityId, bool? AffectsRoomReadiness);
public sealed record IncidentClassificationOption(long Id, string Code, string Name);
public sealed record IncidentClassificationContext(long IncidentId, long? CategoryId, long? SeverityId,
    bool? AffectsRoomReadiness, string Version, IncidentClassificationOption[] Categories, IncidentClassificationOption[] Severities);
public sealed record IncidentClassificationSaved(long IncidentId, long EventId, string Version, bool SuggestRoomBlock);

public sealed class IncidentClassificationService(HotelOpsDbContext db, TimeProvider clock, ICurrentUserAccessor current)
{
    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
        // Keep pending classification evidence tracked by its owner while the
        // session reader uses a clean context enlisted in this same transaction.
        var result = await Dev2SessionValidation.ValidateAsync(db, ticket, clock, ct);
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
        if (!await InstalledAsync(ct))
            throw new InternalRequestReadException(503, "INCIDENT_CLASSIFICATION_STORAGE_UNAVAILABLE");
    }

    private async Task<Incident> LockAsync(long id, CancellationToken ct)
        => await db.Incidents.FromSqlInterpolated($"SELECT * FROM dbo.incidents WITH(UPDLOCK,HOLDLOCK) WHERE incident_id={id}")
            .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new InternalRequestReadException(404, "INCIDENT_UNAVAILABLE");

    private Task<bool> InstalledAsync(CancellationToken ct) => db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
          AND name='HotelOps.IncidentClassification.V1' AND CONVERT(nvarchar(10),value)='1')
          AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incidents')
          AND name='affects_room_readiness' AND system_type_id=104 AND is_nullable=1)
          THEN 1 ELSE 0 END AS Value
        """).AnyAsync(v => v == 1, ct);

    private static void RequireOpen(Incident incident)
    {
        if (incident.Status is "CLOSED" or "CANCELLED")
            throw new InternalRequestReadException(409, "INCIDENT_CLASSIFICATION_CLOSED");
    }
    private sealed class Classification
    {
        public long? CategoryId { get; set; }
        public bool? AffectsRoomReadiness { get; set; }
    }
    private Task<Classification> ReadAsync(long id, CancellationToken ct) => db.Database.SqlQuery<Classification>($"""
        SELECT category_id AS CategoryId,affects_room_readiness AS AffectsRoomReadiness FROM dbo.incidents WHERE incident_id={id}
        """).SingleAsync(ct);

    public async Task<IncidentClassificationContext> ContextAsync(SessionReference ticket, long sid, long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        var incident = await LockAsync(id, ct);
        await AuthorizeAsync(ticket.UserId, incident, ct);
        RequireOpen(incident);
        if (!await IncidentCategoryStore.InstalledAsync(db, ct)) throw new InternalRequestReadException(503, "INCIDENT_CATEGORY_STORAGE_UNAVAILABLE");
        var data = await ReadAsync(id, ct);
        var categories = await db.ReferenceValues.AsNoTracking().Where(r => r.GroupCode == "INCIDENT_CATEGORY" && r.IsActive)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.ReferenceId).Select(r => new IncidentClassificationOption(r.ReferenceId, r.ValueCode, r.DisplayName)).ToArrayAsync(ct);
        var severities = await db.ReferenceValues.AsNoTracking().Where(r => r.GroupCode == "SEVERITY" && r.IsActive
            && (r.ValueCode == "LOW" || r.ValueCode == "NORMAL" || r.ValueCode == "HIGH" || r.ValueCode == "CRITICAL"))
            .OrderBy(r => r.SortOrder).ThenBy(r => r.ReferenceId).Select(r => new IncidentClassificationOption(r.ReferenceId, r.ValueCode, r.DisplayName)).ToArrayAsync(ct);
        var version = await IncidentTriageStore.TagAsync(db, id, ct) ?? throw new InternalRequestReadException(503, "INCIDENT_VERSION_UNAVAILABLE");
        await SessionAsync(ticket, sid, ct);
        await AuthorizeAsync(ticket.UserId, incident, ct);
        await tx.CommitAsync(ct);
        return new(id, data.CategoryId, incident.SeverityId, data.AffectsRoomReadiness, version, categories, severities);
    }

    public async Task<IncidentClassificationSaved> SaveAsync(SessionReference ticket, long sid, long id, string tag,
        IncidentClassificationInput input, CancellationToken ct)
    {
        if (input.CategoryId <= 0 || input.SeverityId <= 0 || input.AffectsRoomReadiness == null)
            throw new ValidationException("Category, severity and room readiness impact are required.");
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Clean classification context required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        var incident = await LockAsync(id, ct);
        await AuthorizeAsync(ticket.UserId, incident, ct);
        RequireOpen(incident);
        if (await IncidentTriageStore.TagAsync(db, id, ct) != tag) throw new InternalRequestReadException(412, "INCIDENT_CHANGED");
        await IncidentCategoryStore.ValidateAsync(db, input.CategoryId, ct);
        var severity = await db.ReferenceValues.FromSqlInterpolated($"SELECT * FROM dbo.reference_values WITH(UPDLOCK,HOLDLOCK) WHERE reference_id={input.SeverityId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (severity is not { IsActive: true, GroupCode: "SEVERITY", ValueCode: "LOW" or "NORMAL" or "HIGH" or "CRITICAL" })
            throw new ValidationException("Select an active Low, Normal, High or Critical severity.");
        if (severity.ValueCode == "CRITICAL" && !await NotificationAcknowledgmentStore.InstalledAsync(db, ct))
            throw new InternalRequestReadException(503, "NOTIFICATION_ACKNOWLEDGMENT_STORAGE_UNAVAILABLE");
        var before = await ReadAsync(id, ct);
        if (before.CategoryId == input.CategoryId && incident.SeverityId == input.SeverityId
            && before.AffectsRoomReadiness == input.AffectsRoomReadiness)
            throw new ValidationException("Thông tin phân loại không thay đổi. Không cần lưu lại.");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var entry = new IncidentEvent { IncidentId=id, EventType="CLASSIFIED", FromStatus=incident.Status, ToStatus=incident.Status,
            ActorId=ticket.UserId, OccurredAt=now, Note="Incident classification updated." };
        db.IncidentEvents.Add(entry);
        await db.SaveChangesAsync(ct);
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.incidents SET category_id={input.CategoryId},severity_id={input.SeverityId},affects_room_readiness={input.AffectsRoomReadiness}
            WHERE incident_id={id} AND row_version=CONVERT(binary(8),{tag.Substring(1,16)},2) AND status NOT IN('CLOSED','CANCELLED')
            """, ct);
        if (changed != 1) throw new InternalRequestReadException(412, "INCIDENT_CHANGED");
        db.AuditLogs.Add(new AuditLog { EntityType="incidents", EntityId=id.ToString(CultureInfo.InvariantCulture), Action="UPDATE",
            ActorUserId=ticket.UserId, BranchId=incident.BranchId, OccurredAt=now,
            OldValues=JsonSerializer.Serialize(new { category_id=before.CategoryId, severity_id=incident.SeverityId, affects_room_readiness=before.AffectsRoomReadiness }),
            NewValues=JsonSerializer.Serialize(new { category_id=input.CategoryId, severity_id=input.SeverityId, severity=severity.ValueCode,
                affects_room_readiness=input.AffectsRoomReadiness, event_id=entry.EventId, consumed_version=tag,
                notification_kind=severity.ValueCode == "CRITICAL" ? "INCIDENT_CRITICAL" : null, notification_version=1 }),
            ChangedFields="[\"category_id\",\"severity_id\",\"affects_room_readiness\"]" });
        await db.SaveChangesAsync(ct);
        await SessionAsync(ticket, sid, ct);
        await AuthorizeAsync(ticket.UserId, incident, ct);
        var version = await IncidentTriageStore.TagAsync(db, id, ct) ?? throw new InternalRequestReadException(503, "INCIDENT_VERSION_UNAVAILABLE");
        await tx.CommitAsync(ct);
        return new(id, entry.EventId, version, input.AffectsRoomReadiness.Value && incident.RoomId != null);
    }
}
