using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Rooms;

public sealed record RoomCheckInOptions(bool CanCheckIn, string Version, string RecordedStatus, string Readiness)
{
    internal bool Active { get; init; }
    internal bool HasOpenWork { get; init; }
}

public sealed partial class RoomCheckInService(HotelOpsDbContext db, DashboardSessionGuard session,
    ICurrentUserAccessor current, IPermissionService permissions, TimeProvider clock)
{
    internal static bool Eligible(bool active, string recorded, string readiness) =>
        active && recorded is "READY" or "AVAILABLE" && readiness == "READY";

    private async Task<long> RequireAccess(long branch, CancellationToken ct, string permission = "ROOM_CHECKIN")
    {
        await session.RequireAsync(ct);
        if (!current.TryGetCurrentUser(out var actor)) throw new InsightException(401, "SESSION_INVALID", "Phiên đăng nhập đã hết hạn.");
        var now = clock.GetUtcNow().UtcDateTime;
        var paths = await (from r in db.UserRoles.AsNoTracking()
            join p in db.RolePermissions.AsNoTracking() on r.RoleId equals p.RoleId
            where r.UserId == actor.UserId && r.Role.RoleCode == "RECEPTION" && r.BranchId == branch
                && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE" && r.User.DeletedAt == null
                && p.Permission.PermissionCode == permission
            select new PermissionGrantPath(p.Permission.PermissionCode, p.DataScope, r.BranchId,
                r.IsActive, r.Role.IsActive, r.AssignedAt, r.ExpiresAt, r.Branch != null && r.Branch.IsActive,
                db.UserBranchAccesses.Any(a => a.UserId == actor.UserId && a.BranchId == branch
                    && a.RevokedAt == null && a.GrantedAt <= now))).ToArrayAsync(ct);
        if (!paths.Any(p => PermissionGrantRules.IsEligible(p, now) && PermissionGrantRules.EffectiveScope(p) == "BRANCH")
            || !(await permissions.EvaluateBranchAsync("ROOM_VIEW", branch, ct)).IsGranted)
            throw new InsightException(403, "ROOM_OPERATION_FORBIDDEN", "Bạn không có quyền thực hiện thao tác phòng tại chi nhánh này.");
        return actor.UserId;
    }

    private async Task<RoomCheckInOptions> Snapshot(long branch, long id, CancellationToken ct)
    {
        var room = await db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.rooms WITH(UPDLOCK,HOLDLOCK) WHERE room_id={id} AND branch_id={branch}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (room == null) throw new InsightException(404, "ROOM_NOT_FOUND", "Không tìm thấy phòng trong chi nhánh.");
        var active = room.IsActive && await db.Floors.AsNoTracking().AnyAsync(f => f.FloorId == room.FloorId && f.BranchId == branch && f.IsActive, ct)
            && (room.AreaId == null || await db.Areas.AsNoTracking().AnyAsync(a => a.AreaId == room.AreaId && a.BranchId == branch
                && a.IsActive && (a.FloorId == null || a.FloorId == room.FloorId), ct))
            && await db.BranchRoomTypes.AsNoTracking().AnyAsync(t => t.RoomTypeId == room.RoomTypeId && (t.BranchId == null || t.BranchId == branch) && t.IsActive, ct);
        var state = (await new EffectiveRoomReadiness(db).ReadAsync([id], clock.GetUtcNow().UtcDateTime, ct))[id];
        var openWork = await HasOpenWork(id, ct);
        var revisions = await db.Database.SqlQuery<string>($"""
            SELECT (SELECT
                (SELECT MAX(history_id) FROM dbo.room_status_history WITH(HOLDLOCK) WHERE room_id={id}) AS history,
                (SELECT CONVERT(varchar(16),row_version,2) FROM dbo.room_cleaning_cycle_state WITH(HOLDLOCK) WHERE room_id={id}) AS cycle,
                (SELECT CONVERT(varchar(16),row_version,2) FROM dbo.cleaning_tasks WITH(HOLDLOCK) WHERE cleaning_task_id={state.CleaningTaskId}) AS cleaning,
                (SELECT CONVERT(varchar(16),row_version,2) FROM dbo.room_inspections WITH(HOLDLOCK) WHERE inspection_id={state.InspectionId}) AS inspection
                FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES) AS Value
            """).SingleAsync(ct);
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            { branch, id, room.OperationalStatus, room.UpdatedAt, active, state, revisions, openWork }))));
        return new(Eligible(active, room.OperationalStatus, state.Readiness) && !openWork, version, room.OperationalStatus, state.Readiness)
            { Active = active, HasOpenWork = openWork };
    }

    public async Task<RoomCheckInOptions> ReadAsync(long branch, long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await RequireAccess(branch, ct);
        var result = await Snapshot(branch, id, ct);
        await RequireAccess(branch, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<RoomCheckInOptions> CheckInAsync(long branch, long id, string version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var actor = await RequireAccess(branch, ct);
        var before = await Snapshot(branch, id, ct);
        if (!StringComparer.Ordinal.Equals(version, before.Version))
            throw new InsightException(412, "CHECKIN_STALE", "Thông tin phòng đã thay đổi. Hãy tải lại trước khi nhận khách.");
        if (!before.CanCheckIn) throw new InsightException(409, "ROOM_NOT_READY", "Phòng chưa đủ điều kiện nhận khách hoặc đã có khách.");
        await RequireAccess(branch, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var request = Guid.NewGuid().ToString("D");
        var oldValues = JsonSerializer.Serialize(new { operationalStatus = before.RecordedStatus });
        var newValues = JsonSerializer.Serialize(new { operation = "CHECKIN", operationalStatus = "OCCUPIED", readiness = before.Readiness });
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SET XACT_ABORT ON;
            UPDATE dbo.rooms SET operational_status='OCCUPIED',updated_at={now}
                WHERE room_id={id} AND branch_id={branch} AND operational_status={before.RecordedStatus} AND is_active=1;
            IF @@ROWCOUNT<>1 THROW 51803,'Room changed during check-in.',1;
            INSERT dbo.room_status_history(room_id,from_status,to_status,trigger_type,changed_by,changed_at,reason,effective_from,effective_to)
                VALUES({id},{before.RecordedStatus},'OCCUPIED','CHECKIN',{actor},{now},N'Lễ tân xác nhận nhận khách','READY','READY');
            INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,request_id,occurred_at)
                VALUES({branch},{actor},'rooms',{id.ToString()},'UPDATE',{oldValues},{newValues},'["operational_status"]',{request},{now});
            """, ct);
        var result = await Snapshot(branch, id, ct);
        await RequireAccess(branch, ct);
        if (result.Readiness != "READY")
            throw new InsightException(409, "ROOM_NOT_READY", "Phòng không còn đủ điều kiện nhận khách.");
        await tx.CommitAsync(ct);
        return result;
    }
}
