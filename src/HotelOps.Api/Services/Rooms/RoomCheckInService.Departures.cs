using System.Data;
using System.Text.Json;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Rooms;

public sealed record RoomDepartureOptions(bool CanDepart, string Version, string RecordedStatus, string? UnavailableReason);
public sealed record RoomMoveTarget(long RoomId, string RoomNumber, string FloorName, string RoomTypeName, string Version);
public sealed record RoomMoveOptions(RoomDepartureOptions Source, IReadOnlyList<RoomMoveTarget> Targets);
public sealed record RoomDepartureReceipt(long RoomId, string RecordedStatus, string Readiness, long? TargetRoomId = null);

public sealed partial class RoomCheckInService
{
    internal static bool CanDepart(bool active, string status, bool openWork) => active && status == "OCCUPIED" && !openWork;

    private async Task<bool> HasOpenWork(long room, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN
              EXISTS(SELECT 1 FROM dbo.cleaning_tasks WITH(HOLDLOCK) WHERE room_id={room}
                AND (status NOT IN('COMPLETED','CANCELLED') OR workflow_status NOT IN('COMPLETED','CANCELLED')))
              OR EXISTS(SELECT 1 FROM dbo.room_inspections WITH(HOLDLOCK) WHERE room_id={room}
                AND (status NOT IN('COMPLETED','CANCELLED') OR workflow_status NOT IN('PASSED','FAILED','CANCELLED')))
              OR EXISTS(SELECT 1 FROM dbo.cleaning_reworks w WITH(HOLDLOCK)
                JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=w.cleaning_task_id
                WHERE t.room_id={room} AND (w.status NOT IN('COMPLETED','CANCELLED')
                  OR w.workflow_status NOT IN('COMPLETED','FAILED','CANCELLED')))
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    private static RoomDepartureOptions Departure(RoomCheckInOptions room) => new(
        CanDepart(room.Active, room.RecordedStatus, room.HasOpenWork), room.Version, room.RecordedStatus,
        !room.Active ? "Phòng hoặc vị trí đang ngừng hoạt động."
        : room.RecordedStatus != "OCCUPIED" ? "Phòng hiện không có khách."
        : room.HasOpenWork ? "Phòng còn công việc vệ sinh, kiểm tra hoặc làm lại chưa kết thúc. Cần người phụ trách xử lý trước."
        : null);

    private static void ValidateDeparture(RoomCheckInOptions source, string version)
    {
        if (!StringComparer.Ordinal.Equals(source.Version, version))
            throw new InsightException(412, "ROOM_OPERATION_STALE", "Thông tin phòng đã thay đổi. Hãy tải lại trước khi thực hiện.");
        var option = Departure(source);
        if (!option.CanDepart) throw new InsightException(409, "ROOM_DEPARTURE_UNAVAILABLE", option.UnavailableReason!);
    }

    public async Task<RoomDepartureOptions> ReadCheckOutAsync(long branch, long room, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await RequireAccess(branch, ct, "ROOM_CHECKOUT");
        var result = Departure(await Snapshot(branch, room, ct));
        await RequireAccess(branch, ct, "ROOM_CHECKOUT");
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<RoomMoveOptions> ReadMoveOptionsAsync(long branch, long room, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await RequireAccess(branch, ct, "ROOM_OCCUPANCY_MOVE");
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.BranchId == branch
                && (r.RoomId == room || r.IsActive && (r.OperationalStatus == "READY" || r.OperationalStatus == "AVAILABLE")))
            .OrderBy(r => r.RoomId).Select(r => new { r.RoomId, r.RoomNumber, r.Floor.FloorName, r.RoomType.TypeName }).ToArrayAsync(ct);
        if (!rooms.Any(r => r.RoomId == room)) throw new InsightException(404, "ROOM_NOT_FOUND", "Không tìm thấy phòng trong chi nhánh.");
        RoomDepartureOptions? source = null;
        var targets = new List<RoomMoveTarget>();
        foreach (var r in rooms)
        {
            var snapshot = await Snapshot(branch, r.RoomId, ct);
            if (r.RoomId == room) source = Departure(snapshot);
            else if (snapshot.CanCheckIn) targets.Add(new(r.RoomId, r.RoomNumber, r.FloorName, r.TypeName, snapshot.Version));
        }
        await RequireAccess(branch, ct, "ROOM_OCCUPANCY_MOVE");
        await tx.CommitAsync(ct);
        return new(source!, source!.CanDepart ? targets.OrderBy(r => r.RoomNumber).ToArray() : []);
    }

    public async Task<RoomDepartureReceipt> CheckOutAsync(long branch, long room, string version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var actor = await RequireAccess(branch, ct, "ROOM_CHECKOUT");
        var source = await Snapshot(branch, room, ct);
        ValidateDeparture(source, version);
        var now = clock.GetUtcNow().UtcDateTime;
        await WriteOccupancyTransition(branch, room, actor, source, "DIRTY", "CHECKOUT", "CHECKOUT",
            "Lễ tân xác nhận trả phòng", Guid.NewGuid().ToString("D"), now, null, ct);
        var after = await Snapshot(branch, room, ct);
        ValidateReleasedRoom(after);
        await RequireAccess(branch, ct, "ROOM_CHECKOUT");
        await tx.CommitAsync(ct);
        return new(room, after.RecordedStatus, after.Readiness);
    }

    public async Task<RoomDepartureReceipt> MoveAsync(long branch, long room, string version,
        long target, string targetVersion, string reason, CancellationToken ct)
    {
        reason = reason.Trim();
        if (target <= 0 || target == room || reason.Length is < 1 or > 500 || reason.Any(char.IsControl))
            throw new InsightException(400, "INVALID_ROOM_MOVE", "Chọn phòng đích khác phòng hiện tại và nhập lý do tối đa 500 ký tự.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var actor = await RequireAccess(branch, ct, "ROOM_OCCUPANCY_MOVE");
        // Both room locks follow one global order, regardless of movement direction.
        var first = await Snapshot(branch, Math.Min(room, target), ct);
        var second = await Snapshot(branch, Math.Max(room, target), ct);
        var source = room < target ? first : second;
        var destination = room < target ? second : first;
        ValidateDeparture(source, version);
        if (!StringComparer.Ordinal.Equals(destination.Version, targetVersion))
            throw new InsightException(412, "ROOM_TARGET_STALE", "Phòng đích đã thay đổi. Hãy chọn lại phòng.");
        if (!destination.CanCheckIn)
            throw new InsightException(409, "ROOM_TARGET_UNAVAILABLE", "Phòng đích chưa sẵn sàng hoặc đã có khách.");
        var names = await db.Rooms.AsNoTracking().Where(r => r.RoomId == room || r.RoomId == target)
            .ToDictionaryAsync(r => r.RoomId, r => r.RoomNumber, ct);
        var requestId = Guid.NewGuid().ToString("D");
        var now = clock.GetUtcNow().UtcDateTime;
        var description = $"Chuyển từ phòng {names[room]} sang phòng {names[target]}: {reason}";
        var linked = new { sourceRoomId = room, sourceRoomNumber = names[room], targetRoomId = target, targetRoomNumber = names[target], reason };
        await WriteOccupancyTransition(branch, room, actor, source, "DIRTY", "CHECKOUT", "OCCUPANCY_MOVE", description, requestId, now, linked, ct);
        await WriteOccupancyTransition(branch, target, actor, destination, "OCCUPIED", "CHECKIN", "OCCUPANCY_MOVE", description, requestId, now, linked, ct);
        var after = await Snapshot(branch, room, ct);
        ValidateReleasedRoom(after);
        var targetAfter = await Snapshot(branch, target, ct);
        if (targetAfter.RecordedStatus != "OCCUPIED" || targetAfter.Readiness != "READY" || targetAfter.HasOpenWork)
            throw new InsightException(409, "ROOM_TARGET_UNAVAILABLE", "Phòng đích không còn đủ điều kiện chuyển khách.");
        await RequireAccess(branch, ct, "ROOM_OCCUPANCY_MOVE");
        await tx.CommitAsync(ct);
        return new(room, after.RecordedStatus, after.Readiness, target);
    }

    private static void ValidateReleasedRoom(RoomCheckInOptions room)
    {
        if (room.RecordedStatus != "DIRTY" || room.Readiness is not ("PENDING_CLEANING" or "BLOCKED"))
            throw new InsightException(409, "ROOM_RELEASE_CONFLICT", "Chưa xác nhận được trạng thái chờ vệ sinh. Thao tác chưa được lưu.");
    }

    private async Task WriteOccupancyTransition(long branch, long room, long actor, RoomCheckInOptions before,
        string status, string trigger, string operation, string reason, string request, DateTime now, object? linked, CancellationToken ct)
    {
        var effective = status == "DIRTY" ? (before.Readiness == "BLOCKED" ? "BLOCKED" : "PENDING_CLEANING") : before.Readiness;
        var oldValues = JsonSerializer.Serialize(new { operationalStatus = before.RecordedStatus, readiness = before.Readiness });
        var newValues = JsonSerializer.Serialize(new { operation, operationalStatus = status, readiness = effective, linked });
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SET XACT_ABORT ON;
            UPDATE dbo.rooms SET operational_status={status},updated_at={now}
                WHERE room_id={room} AND branch_id={branch} AND operational_status={before.RecordedStatus} AND is_active=1;
            IF @@ROWCOUNT<>1 THROW 51803,'Room occupancy changed.',1;
            INSERT dbo.room_status_history(room_id,from_status,to_status,trigger_type,changed_by,changed_at,reason,effective_from,effective_to)
                VALUES({room},{before.RecordedStatus},{status},{trigger},{actor},{now},{reason},{before.Readiness},{effective});
            INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,request_id,occurred_at)
                VALUES({branch},{actor},'rooms',{room.ToString()},'UPDATE',{oldValues},{newValues},'["operational_status"]',{request},{now});
            """, ct);
    }
}
