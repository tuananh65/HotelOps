using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed class AssetProfileReader(HotelOpsDbContext db, AssetProfileAccess authority, InsightAccess access,
    DashboardSessionGuard session, IAssetService assets, AssetEditService editor, AssetRetirementService retirement)
{
    private async Task<bool> CanReadSchedulesAsync(long branch, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var actor = access.UserId;
        var paths = await (from role in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on role.RoleId equals grant.RoleId
            where role.UserId == actor && role.BranchId == branch && role.Role.RoleCode == "MAINTENANCE_LEAD"
                && grant.Permission.PermissionCode == "PM_MANAGE"
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, role.BranchId,
                role.IsActive, role.Role.IsActive, role.AssignedAt, role.ExpiresAt, role.Branch != null && role.Branch.IsActive,
                db.UserBranchAccesses.Any(b => b.UserId == actor && b.BranchId == branch && b.RevokedAt == null && b.GrantedAt <= now)))
            .ToArrayAsync(ct);
        return paths.Any(p => PermissionGrantRules.IsEligible(p, now) && PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "GLOBAL");
    }
    public async Task<object> CatalogAsync(long? branch, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct); await session.RequireAsync(ct);
        var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId)
            .Select(b => new { Id = b.BranchId, Name = b.BranchName }).ToArrayAsync(ct);
        var allowed = new List<long>();
        foreach (var b in branches) if (await authority.AllowedAsync(b.Id, ct)) allowed.Add(b.Id);
        if (allowed.Count == 0 || branch != null && !allowed.Contains(branch.Value)) throw new InsightException(403, "ACCESS_DENIED", "No asset branch available.");
        var chosen = branch ?? allowed[0];
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.BranchId == chosen && r.Floor.BranchId == chosen)
            .OrderBy(r => r.RoomNumber).Select(r => new { Id = r.RoomId, Name = r.RoomNumber }).ToArrayAsync(ct);
        var areas = await db.Areas.AsNoTracking().Where(a => a.BranchId == chosen && (a.Floor == null || a.Floor.BranchId == chosen))
            .OrderBy(a => a.AreaCode).Select(a => new { Id = a.AreaId, Name = a.AreaName }).ToArrayAsync(ct);
        var departments = await db.Departments.AsNoTracking().Where(d => d.BranchId == chosen).OrderBy(d => d.DepartmentName)
            .Select(d => new { Id = d.DepartmentId, Name = d.DepartmentName }).ToArrayAsync(ct);
        var categories = await db.AssetTypes.AsNoTracking().OrderBy(t => t.TypeCode).Select(t => new { Id = t.AssetTypeId, Name = t.TypeName }).ToArrayAsync(ct);
        await authority.RequireAsync(chosen, ct); await tx.CommitAsync(ct);
        return new { Branches = branches.Where(b => allowed.Contains(b.Id)), Branch = chosen, Rooms = rooms, Areas = areas, Departments = departments, Categories = categories };
    }

    public async Task<object> ListAsync(AssetQueryRequest filter, CancellationToken ct)
    {
        if (filter.BranchId is not > 0 || filter.Page < 1 || filter.Page > 100000 || filter.Search?.Length > 200
            || new[] { filter.RoomId, filter.AreaId, filter.DepartmentId, filter.AssetTypeId }.Any(v => v is <= 0))
            throw new InsightException(400, "INVALID_FILTER", "Invalid asset filter.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var branch = filter.BranchId.Value; await authority.RequireAsync(branch, ct);
        if (filter.RoomId != null && !await db.Rooms.AnyAsync(r => r.RoomId == filter.RoomId && r.BranchId == branch, ct)
            || filter.AreaId != null && !await db.Areas.AnyAsync(a => a.AreaId == filter.AreaId && a.BranchId == branch, ct)
            || filter.DepartmentId != null && !await db.Departments.AnyAsync(d => d.DepartmentId == filter.DepartmentId && d.BranchId == branch, ct))
            throw new InsightException(403, "FILTER_SCOPE", "Filter outside branch scope.");
        var query = authority.Query(branch);
        var term = filter.Search?.Trim();
        query = query.Where(a => (filter.RoomId == null || a.CurrentRoomId == filter.RoomId) && (filter.AreaId == null || a.CurrentAreaId == filter.AreaId)
            && (filter.DepartmentId == null || a.CurrentDepartmentId == filter.DepartmentId) && (filter.AssetTypeId == null || a.AssetTypeId == filter.AssetTypeId)
            && (filter.ConditionStatus == null || a.ConditionStatus == filter.ConditionStatus) && (filter.LifecycleStatus == null || a.LifecycleStatus == filter.LifecycleStatus)
            && (term == null || term == "" || a.AssetCode.Contains(term) || a.AssetName.Contains(term) || a.SerialNumber != null && a.SerialNumber.Contains(term)));
        var size = await authority.PageSizeAsync(ct); var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(a => a.AssetCode).ThenBy(a => a.AssetId).Skip((filter.Page - 1) * size).Take(size)
            .Select(a => new { a.AssetId, a.AssetCode, a.AssetName, Category = a.AssetType.TypeName,
                Room = a.CurrentRoom == null ? null : a.CurrentRoom.RoomNumber, Area = a.CurrentArea == null ? null : a.CurrentArea.AreaName,
                Department = a.CurrentDepartment == null ? null : a.CurrentDepartment.DepartmentName,
                Custodian = a.CustodianUser == null ? null : a.CustodianUser.FullName, a.ConditionStatus, a.LifecycleStatus }).ToArrayAsync(ct);
        await authority.RequireAsync(branch, ct); await tx.CommitAsync(ct);
        return new { Items = rows, Total = count, filter.Page, PageSize = size };
    }

    private async Task<long> AssetBranchAsync(long id, CancellationToken ct)
    {
        await session.RequireAsync(ct);
        var branch = await db.Assets.AsNoTracking().Where(a => a.AssetId == id).Select(a => (long?)a.BranchId).SingleOrDefaultAsync(ct);
        if (branch == null) throw new InsightException(404, "ASSET_UNAVAILABLE", "Asset unavailable.");
        await authority.RequireAsync(branch.Value, ct);
        if (!await authority.Query(branch.Value).AnyAsync(a => a.AssetId == id, ct)) throw new InsightException(404, "ASSET_UNAVAILABLE", "Asset placement scope unavailable.");
        return branch.Value;
    }

    public async Task<object> ReadAsync(long id, int page, CancellationToken ct)
    {
        if (id <= 0 || page is < 1 or > 100000) throw new InsightException(400, "INVALID_ID", "Invalid profile page.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var branch = await AssetBranchAsync(id, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var zones = await db.SystemSettings.AsNoTracking().Where(s => s.BranchId == null && s.SettingKey == "SYS-PAR-10")
            .Select(s => s.SettingValue).ToArrayAsync(ct);
        var zone = zones.Length == 0 ? "Asia/Ho_Chi_Minh" : zones.Length == 1 ? zones[0] ?? "" : "";
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(zone); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { throw new InsightException(503, "TIMEZONE_CONFIGURATION", "Invalid SYS-PAR-10."); }
        var size = await authority.PageSizeAsync(ct); var skip = (page - 1) * size;
        var profile = await assets.GetAssetAsync(id, ct);
        var allowed = new List<long>();
        foreach (var b in await db.Branches.AsNoTracking().Where(b => b.IsActive).Select(b => b.BranchId).ToArrayAsync(ct))
            if (await authority.AllowedAsync(b, ct)) allowed.Add(b);
        var history = await ReportAssetHistory.ReadAsync(db, authority.Query(branch).Where(a => a.AssetId == id), allowed.ToArray(), DateTime.MinValue, now.AddTicks(1), ct);
        var incidentScope = await IncidentReadAccess.ReadAsync(db, access.UserId, branch, ct, sourceRecord: true);
        var incidents = incidentScope.Apply(db.Incidents.AsNoTracking()).Where(i => i.AssetId == id && i.Status != "CLOSED" && i.Status != "CANCELLED");
        var incidentRows = await incidents.OrderByDescending(i => i.ReportedAt).ThenByDescending(i => i.IncidentId).Skip(skip).Take(size)
            .Select(i => new { i.IncidentId, i.IncidentNo, i.IncidentTitle, i.Status }).ToArrayAsync(ct);
        var canSchedules = await CanReadSchedulesAsync(branch, ct);
        var schedules = db.PreventiveMaintenanceSchedules.AsNoTracking().Where(s => canSchedules && s.AssetId == id && s.RoomId == null && s.AreaId == null
            && (s.AssignedDepartment == null || s.AssignedDepartment.BranchId == branch));
        var scheduleRows = await schedules.OrderBy(s => s.ScheduleCode).ThenBy(s => s.ScheduleId).Skip(skip).Take(size)
            .Select(s => new { s.ScheduleId, s.ScheduleCode, s.Frequency, s.FrequencyInterval, s.NextDueAt, s.IsActive }).ToArrayAsync(ct);
        var files = db.Attachments.AsNoTracking().Where(a => a.EntityType == "ASSET" && a.EntityId == id && a.DeletedAt == null);
        var documents = await files.OrderByDescending(a => a.UploadedAt).ThenByDescending(a => a.AttachmentId).Skip(skip).Take(size)
            .Select(a => new { a.AttachmentId, Name = a.OriginalFileName ?? a.FileName, a.MimeType, a.FileSizeBytes, a.UploadedAt }).ToArrayAsync(ct);
        var result = new { Profile = profile, CanEdit = await editor.CanEditAsync(id, ct), CanRetire = await retirement.CanOpenAsync(id, ct), Snapshot = DateTime.SpecifyKind(now, DateTimeKind.Utc), TimeZone = zone, Page = page, PageSize = size,
            History = history.Rows.OrderByDescending(h => h.OccurredAt).ThenByDescending(h => h.AuditId).Skip(skip).Take(size), HistoryTotal = history.Rows.Length,
            HistoryIncomplete = history.MissingEvidence, Incidents = incidentRows, IncidentTotal = await incidents.CountAsync(ct),
            IncidentAccess = incidentScope.Lead || incidentScope.Staff ? "ALLOWED" : "ACCESS_DENIED",
            Schedules = scheduleRows, ScheduleTotal = await schedules.CountAsync(ct), ScheduleAccess = canSchedules ? "ALLOWED" : "ACCESS_DENIED",
            Documents = documents, DocumentTotal = await files.CountAsync(ct) };
        foreach (var b in allowed) await authority.RequireAsync(b, ct);
        if (incidentScope != await IncidentReadAccess.ReadAsync(db, access.UserId, branch, ct, sourceRecord: true)
            || canSchedules != await CanReadSchedulesAsync(branch, ct))
            throw new InsightException(403, "SOURCE_AUTHORITY_CHANGED", "Source read authority changed during the snapshot.");
        await AssetBranchAsync(id, ct); await tx.CommitAsync(ct); return result;
    }

    public async Task<InternalRequestEvidenceFile> DownloadAsync(long id, long file, string privateRoot, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct); await AssetBranchAsync(id, ct);
        var row = await db.Attachments.AsNoTracking().SingleOrDefaultAsync(a => a.AttachmentId == file && a.EntityType == "ASSET" && a.EntityId == id && a.DeletedAt == null, ct);
        // Custody uploads have a bounded 72 MiB HTTP envelope; retain access to
        // committed photos when the configured per-file policy exceeds the old 10 MiB default.
        var result = row == null ? null : await InternalRequestEvidence.ReadAsync(row, privateRoot, ct,72*1024*1024);
        if (result == null) throw new InsightException(404, "FILE_UNAVAILABLE", "Private source file unavailable.");
        await AssetBranchAsync(id, ct); await tx.CommitAsync(ct); return result;
    }
}
