using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed class AssetLocationFilter
{
    public long Branch { get; set; }
    public long? Floor { get; set; }
    public long? Area { get; set; }
    public long? Room { get; set; }
    public long? Department { get; set; }
    public long? Category { get; set; }
    public string? Condition { get; set; }
    public string? Lifecycle { get; set; }
    public string GroupBy { get; set; } = "LOCATION";
    public string? Group { get; set; }
    public int Page { get; set; } = 1;
}

// Current placement only: history rows must not multiply current asset counts.
public sealed class AssetLocationReview(HotelOpsDbContext db, InsightAccess access, DashboardSessionGuard sessions)
{
    public async Task<bool> AllowedAsync(long branch, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var actor = access.UserId;
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == actor && r.IsActive && r.Role.IsActive
            && (r.Role.RoleCode == "OPERATIONS_MANAGER" || r.Role.RoleCode == "MAINTENANCE_LEAD")
            && (r.BranchId == null || r.BranchId == branch) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && r.User.DeletedAt == null && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE", ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(b => b.UserId == actor && b.BranchId == branch
                && b.Branch.IsActive && b.GrantedAt <= now && b.RevokedAt == null, ct)) return false;
        try { await access.BranchesAsync("ASSET_VIEW", branch, ct); return true; }
        catch (InsightException e) when (e.Status == 403) { return false; }
    }

    private async Task RequireAsync(long branch, CancellationToken ct)
    {
        await sessions.RequireAsync(ct);
        if (!await AllowedAsync(branch, ct)) throw new InsightException(403, "ACCESS_DENIED", "Current asset-view branch scope and Operations Manager/Maintenance Lead role required.");
    }

    public async Task<object> OptionsAsync(long? branch, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await sessions.RequireAsync(ct);
        var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId)
            .Select(b => new { Id = b.BranchId, Name = b.BranchName }).ToArrayAsync(ct);
        var allowed = new List<long>();
        foreach (var b in branches) if (await AllowedAsync(b.Id, ct)) allowed.Add(b.Id);
        if (allowed.Count == 0 || branch != null && !allowed.Contains(branch.Value))
            throw new InsightException(403, "ACCESS_DENIED", "No authorized branch.");
        var chosen = branch ?? allowed[0];
        // Inactive locations remain valid filters for historical/retired assets still placed there.
        var floors = await db.Floors.AsNoTracking().Where(f => f.BranchId == chosen).OrderBy(f => f.FloorCode)
            .Select(f => new { Id = f.FloorId, Name = f.FloorName }).ToArrayAsync(ct);
        var areas = await db.Areas.AsNoTracking().Where(a => a.BranchId == chosen && (a.Floor == null || a.Floor.BranchId == chosen))
            .OrderBy(a => a.AreaCode).Select(a => new { Id = a.AreaId, Name = a.AreaName }).ToArrayAsync(ct);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.BranchId == chosen && r.Floor.BranchId == chosen)
            .OrderBy(r => r.RoomNumber).Select(r => new { Id = r.RoomId, Name = r.RoomNumber }).ToArrayAsync(ct);
        var departments = await db.Departments.AsNoTracking().Where(d => d.BranchId == chosen).OrderBy(d => d.DepartmentName)
            .Select(d => new { Id = d.DepartmentId, Name = d.DepartmentName }).ToArrayAsync(ct);
        var categories = await db.AssetTypes.AsNoTracking().OrderBy(t => t.TypeCode)
            .Select(t => new { Id = t.AssetTypeId, Name = t.TypeName }).ToArrayAsync(ct);
        await RequireAsync(chosen, ct); await tx.CommitAsync(ct);
        return new { Branches = branches.Where(b => allowed.Contains(b.Id)), Branch = chosen, Floors = floors, Areas = areas,
            Rooms = rooms, Departments = departments, Categories = categories };
    }

    private IQueryable<Asset> Scoped(long branch) => db.Assets.AsNoTracking().Where(a => a.BranchId == branch
        && !(a.CurrentRoomId != null && a.CurrentAreaId != null)
        && (a.CurrentDepartment == null || a.CurrentDepartment.BranchId == branch)
        && (a.CurrentRoom == null || a.CurrentRoom.BranchId == branch && a.CurrentRoom.Floor.BranchId == branch
            && (a.CurrentRoom.Area == null || a.CurrentRoom.Area.BranchId == branch))
        && (a.CurrentArea == null || a.CurrentArea.BranchId == branch && (a.CurrentArea.Floor == null || a.CurrentArea.Floor.BranchId == branch)));

    public async Task<object> ReadAsync(AssetLocationFilter filter, CancellationToken ct)
    {
        if (filter.Branch <= 0 || filter.Page is < 1 or > 100000 || filter.Group?.Length > 80
            || new[] { filter.Floor, filter.Area, filter.Room, filter.Department, filter.Category }.Any(id => id is <= 0)
            || filter.GroupBy is not ("LOCATION" or "BRANCH" or "FLOOR" or "AREA" or "ROOM" or "DEPARTMENT" or "CATEGORY" or "CONDITION")
            || filter.Condition is not (null or "GOOD" or "FAIR" or "POOR" or "FAULTY")
            || filter.Lifecycle is not (null or "IN_USE" or "UNDER_REPAIR" or "RETIRED"))
            throw new InsightException(400, "INVALID_FILTER", "Invalid asset location filter.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await RequireAsync(filter.Branch, ct);
        // Reject foreign filter identities before counts/search/drill-down can reveal them.
        if (filter.Floor != null && !await db.Floors.AnyAsync(f => f.FloorId == filter.Floor && f.BranchId == filter.Branch, ct)
            || filter.Area != null && !await db.Areas.AnyAsync(a => a.AreaId == filter.Area && a.BranchId == filter.Branch, ct)
            || filter.Room != null && !await db.Rooms.AnyAsync(r => r.RoomId == filter.Room && r.BranchId == filter.Branch, ct)
            || filter.Department != null && !await db.Departments.AnyAsync(d => d.DepartmentId == filter.Department && d.BranchId == filter.Branch, ct)
            || filter.Category != null && !await db.AssetTypes.AnyAsync(t => t.AssetTypeId == filter.Category, ct))
            throw new InsightException(403, "FILTER_SCOPE", "Filter is unavailable in the selected scope.");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var settings = await db.SystemSettings.AsNoTracking().Where(s => s.BranchId == null && s.SettingKey == "SYS-PAR-11")
            .Select(s => s.SettingValue).ToArrayAsync(ct);
        var size = 20;
        if (settings.Length > 1 || settings.Length == 1 && (!int.TryParse(settings[0], out size) || size is < 1 or > 100))
            throw new InsightException(503, "PAGE_SIZE_CONFIGURATION", "Invalid SYS-PAR-11.");
        var all = Scoped(filter.Branch);
        var incomplete = await db.Assets.AsNoTracking().CountAsync(a => a.BranchId == filter.Branch, ct) != await all.CountAsync(ct);
        var query = all.Where(a => (filter.Floor == null || a.CurrentRoom != null && a.CurrentRoom.FloorId == filter.Floor
                    || a.CurrentArea != null && a.CurrentArea.FloorId == filter.Floor)
            && (filter.Area == null || a.CurrentAreaId == filter.Area || a.CurrentRoom != null && a.CurrentRoom.AreaId == filter.Area)
            && (filter.Room == null || a.CurrentRoomId == filter.Room) && (filter.Department == null || a.CurrentDepartmentId == filter.Department)
            && (filter.Category == null || a.AssetTypeId == filter.Category) && (filter.Condition == null || a.ConditionStatus == filter.Condition)
            && (filter.Lifecycle == null || a.LifecycleStatus == filter.Lifecycle));
        var rows = await query.OrderBy(a => a.AssetCode).ThenBy(a => a.AssetId).Take(50001).Select(a => new LocationAsset(
            a.AssetId, a.AssetCode, a.AssetName, a.BranchId, a.Branch.BranchName, a.AssetTypeId, a.AssetType.TypeName,
            a.CurrentRoomId, a.CurrentRoom == null ? null : a.CurrentRoom.RoomNumber,
            a.CurrentAreaId ?? (a.CurrentRoom == null ? null : a.CurrentRoom.AreaId),
            a.CurrentArea != null ? a.CurrentArea.AreaName : a.CurrentRoom != null && a.CurrentRoom.Area != null ? a.CurrentRoom.Area.AreaName : null,
            a.CurrentRoom != null ? a.CurrentRoom.FloorId : a.CurrentArea != null ? a.CurrentArea.FloorId : null,
            a.CurrentRoom != null ? a.CurrentRoom.Floor.FloorName : a.CurrentArea != null && a.CurrentArea.Floor != null ? a.CurrentArea.Floor.FloorName : null,
            a.CurrentDepartmentId, a.CurrentDepartment == null ? null : a.CurrentDepartment.DepartmentName,
            a.ConditionStatus, a.LifecycleStatus)).ToArrayAsync(ct);
        if (rows.Length > 50000) throw new InsightException(422, "ASSET_POPULATION_LIMIT", "Narrow the asset filters.");
        (string Key, string Name) Group(LocationAsset a) => filter.GroupBy switch
        {
            "BRANCH" => ("BRANCH:" + a.BranchId, a.BranchName),
            "CATEGORY" => ("CATEGORY:" + a.CategoryId, a.CategoryName),
            "CONDITION" => ("CONDITION:" + a.Condition, a.Condition),
            "DEPARTMENT" => ("DEPARTMENT:" + a.DepartmentId, a.DepartmentName ?? "N/A"),
            "FLOOR" => ("FLOOR:" + a.FloorId, a.FloorName ?? "N/A"),
            "AREA" => ("AREA:" + a.AreaId, a.AreaName ?? "N/A"),
            "ROOM" => ("ROOM:" + a.RoomId, a.RoomName ?? "N/A"),
            _ => a.RoomId != null ? ("ROOM:" + a.RoomId, a.RoomName!) : a.AreaId != null ? ("AREA:" + a.AreaId, a.AreaName!) : ("UNASSIGNED", "Unassigned")
        };
        var groups = rows.GroupBy(Group).OrderBy(g => g.Key.Key, StringComparer.Ordinal).Select(g => new
        {
            g.Key.Key, g.Key.Name, RegisteredCount = g.Count(), ActiveCount = g.Count(a => a.Lifecycle is "IN_USE" or "UNDER_REPAIR"),
            RetiredCount = g.Count(a => a.Lifecycle == "RETIRED"),
            States = g.GroupBy(a => new { a.Lifecycle, a.Condition }).OrderBy(s => s.Key.Lifecycle).ThenBy(s => s.Key.Condition)
                .Select(s => new { s.Key.Lifecycle, s.Key.Condition, Count = s.Count() }).ToArray()
        }).ToArray();
        var selected = filter.Group == null ? [] : rows.Where(a => Group(a).Key == filter.Group).ToArray();
        await RequireAsync(filter.Branch, ct); await tx.CommitAsync(ct);
        return new { Snapshot = DateTime.SpecifyKind(now, DateTimeKind.Utc), Filters = filter, PageSize = size,
            GroupCount = groups.Length, Groups = groups.Skip((filter.Page - 1) * size).Take(size),
            AssetCount = selected.Length, Assets = selected.Skip((filter.Page - 1) * size).Take(size),
            MissingSources = incomplete ? new[] { "LOCATION_SCOPE_INCOMPLETE" } : [] };
    }

    private sealed record LocationAsset(long Id, string Code, string Name, long BranchId, string BranchName, long CategoryId, string CategoryName,
        long? RoomId, string? RoomName, long? AreaId, string? AreaName, long? FloorId, string? FloorName,
        long? DepartmentId, string? DepartmentName, string Condition, string Lifecycle);
}
