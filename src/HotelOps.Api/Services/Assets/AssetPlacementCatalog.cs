using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

// The first placement uses a server-confirmed effective timestamp.
public sealed class AssetPlacementCatalog(HotelOpsDbContext db,AssetProfileAccess profile,InsightAccess access)
{
    public async Task<object> ReadAsync(long id,CancellationToken ct)
    {
        if(id<=0)throw new InsightException(400,"INVALID_ID","Invalid asset identity.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId],ct);
        var asset=await db.Assets.AsNoTracking().SingleOrDefaultAsync(a=>a.AssetId==id,ct)
            ??throw new InsightException(404,"ASSET_UNAVAILABLE","Asset unavailable.");
        await RequireAsync(asset.BranchId,ct);
        if(!await profile.Query(asset.BranchId).AnyAsync(a=>a.AssetId==id,ct))
            throw new InsightException(409,"ASSET_SCOPE_INCONSISTENT","Resolve inconsistent asset scope before placement.");
        if(asset.CurrentRoomId!=null||asset.CurrentAreaId!=null)
            throw new InsightException(409,"ASSET_ALREADY_PLACED","Use Transfer Location for an asset that already has a location.");
        if(asset.LifecycleStatus is not("IN_USE" or "UNDER_REPAIR")||asset.CurrentDepartmentId is not {} department
            ||!await db.Departments.AsNoTracking().AnyAsync(d=>d.DepartmentId==department&&d.BranchId==asset.BranchId&&d.IsActive,ct))
            throw new InsightException(409,"ASSET_PLACEMENT_INELIGIBLE","An operational asset with active custodian department is required.");
        var rooms=await db.Rooms.AsNoTracking().Where(r=>r.BranchId==asset.BranchId&&r.IsActive&&r.Floor.IsActive
            &&r.Floor.BranchId==asset.BranchId&&r.RoomType.IsActive
            &&(r.Area==null||r.Area.IsActive&&r.Area.BranchId==asset.BranchId&&(r.Area.FloorId==null||r.Area.FloorId==r.FloorId)))
            .OrderBy(r=>r.RoomNumber).Select(r=>new{Id=r.RoomId,Name=r.RoomNumber,Floor=r.Floor.FloorName}).ToArrayAsync(ct);
        var areas=await db.Areas.AsNoTracking().Where(a=>a.BranchId==asset.BranchId&&a.IsActive
            &&(a.Floor==null||a.Floor.IsActive&&a.Floor.BranchId==asset.BranchId))
            .OrderBy(a=>a.AreaCode).Select(a=>new{Id=a.AreaId,Name=a.AreaName,Floor=a.Floor==null?null:a.Floor.FloorName}).ToArrayAsync(ct);
        var version=await AssetVersion.ReadAsync(db,id,ct);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        await RequireAsync(asset.BranchId,ct);await tx.CommitAsync(ct);
        return new{asset.AssetId,asset.AssetCode,asset.AssetName,asset.BranchId,asset.CurrentDepartmentId,asset.CustodianUserId,
            asset.CreatedAt,ServerTimeUtc=DateTime.SpecifyKind(now,DateTimeKind.Utc),Version=version,Rooms=rooms,Areas=areas};
    }
    internal async Task RequireAsync(long branch,CancellationToken ct)
    {
        await profile.RequireAsync(branch,ct);
        await access.BranchesAsync("ASSET_MANAGE",branch,ct);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if(!await db.UserRoles.AsNoTracking().AnyAsync(r=>r.UserId==access.UserId&&r.IsActive&&r.Role.IsActive
            &&r.Role.RoleCode=="OPERATIONS_MANAGER"&&(r.BranchId==null||r.BranchId==branch)
            &&r.AssignedAt<=now&&(r.ExpiresAt==null||r.ExpiresAt>now),ct))
            throw new InsightException(403,"ACCESS_DENIED","Current Operations Manager authority is required.");
    }
}
