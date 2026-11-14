using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Dependencies;

// Same Branch U lock used by UC014/015/016. Caller owns commit/rollback.
public sealed class Dev2PlacementGuard(HotelOpsDbContext db)
{
    private Guid? transactionId;
    private long lockedBranch;
    public sealed record Location(long BranchId, bool Active, long? FloorId, long? FloorBranchId,
        bool? FloorActive, long? AreaId, long? AreaBranchId, bool? AreaActive, long? AreaFloorId);

    public static void Validate(long branch, Location value)
    {
        if (branch <= 0 || value.BranchId != branch || !value.Active || value.FloorId is <= 0 || value.AreaId is <= 0 ||
            value.FloorId != null && (value.FloorBranchId != branch || value.FloorActive != true) ||
            value.AreaId != null && (value.AreaBranchId != branch || value.AreaActive != true ||
                value.AreaFloorId != null && value.AreaFloorId != value.FloorId))
            throw new ValidationException("Location must be active with consistent parents in the authorized branch.");
    }

    public async Task LockBranchAsync(long branch, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is null || System.Transactions.Transaction.Current != null)
            throw new InvalidOperationException("Placement requires the caller's local SQL transaction.");
        if (branch <= 0) throw new ValidationException("Invalid branch.");
        var row = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={branch}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (row?.IsActive != true) throw new ValidationException("Branch must be active.");
        transactionId = db.Database.CurrentTransaction!.TransactionId;
        lockedBranch = branch;
    }

    // Invoke after LockBranchAsync, at the effective write boundary, not preflight.
    public async Task ValidateReferencesAsync(long branch, long? roomId, long? areaId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (db.Database.CurrentTransaction is not { } tx || tx.TransactionId != transactionId || lockedBranch != branch)
            throw new InvalidOperationException("Retained Branch lock in this caller transaction required.");
        if (roomId is <= 0 || areaId is <= 0) throw new ValidationException("Location IDs must be positive.");
        if (roomId is long room)
        {
            var row = await db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.rooms WITH(HOLDLOCK) WHERE room_id={room}")
                .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new ValidationException("Location unavailable.");
            if (row.BranchId != branch) throw new ValidationException("Location unavailable.");
            var floor = await db.Floors.FromSqlInterpolated($"SELECT * FROM dbo.floors WITH(HOLDLOCK) WHERE floor_id={row.FloorId}")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            var area = row.AreaId is long parent ? await db.Areas.FromSqlInterpolated($"SELECT * FROM dbo.areas WITH(HOLDLOCK) WHERE area_id={parent}")
                .AsNoTracking().SingleOrDefaultAsync(ct) : null;
            Validate(branch, new(row.BranchId,row.IsActive,row.FloorId,floor?.BranchId,floor?.IsActive,
                row.AreaId,area?.BranchId,area?.IsActive,area?.FloorId));
            if (areaId != null && row.AreaId != areaId) throw new ValidationException("Room and Area must have the authoritative parent relationship.");
        }
        if (areaId is long target)
        {
            var area = await db.Areas.FromSqlInterpolated($"SELECT * FROM dbo.areas WITH(HOLDLOCK) WHERE area_id={target}")
                .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new ValidationException("Location unavailable.");
            if (area.BranchId != branch) throw new ValidationException("Location unavailable.");
            var floor = area.FloorId is long parent ? await db.Floors.FromSqlInterpolated($"SELECT * FROM dbo.floors WITH(HOLDLOCK) WHERE floor_id={parent}")
                .AsNoTracking().SingleOrDefaultAsync(ct) : null;
            Validate(branch,new(area.BranchId,area.IsActive,area.FloorId,floor?.BranchId,floor?.IsActive,null,null,null,null));
        }
    }
}
