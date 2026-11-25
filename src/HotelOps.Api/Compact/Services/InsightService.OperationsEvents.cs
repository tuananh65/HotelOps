using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class InsightService
{
    private sealed record OperationsEventRow(string Category, DateTime OccurredAt, string Subject,
        string Action, string Person, string? Detail);

    // Read persisted domain events, not current statuses masquerading as historical events.
    private async Task<object> OperationsEvents(long branch, DateTime start, DateTime end, CancellationToken ct, bool endExclusive = false)
    {
        const int limit = 10000;
        var result = new List<OperationsEventRow>();
        if (await access.Allows("SHIFT_MANAGE", branch, ct: ct))
        {
            var rows = await (from e in db.WorkdayEvents.AsNoTracking()
                join a in db.WorkdayAssignments on e.AssignmentId equals a.AssignmentId
                join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
                join u in db.Users on a.UserId equals u.UserId
                where s.BranchId == branch && e.OccurredAt >= start && (e.OccurredAt < end || !endExclusive && e.OccurredAt == end)
                orderby e.OccurredAt descending, e.EventId descending
                select new { e.OccurredAt, e.Action, e.Reason, s.Name, a.WorkDate, u.FullName })
                .Take(limit).ToArrayAsync(ct);
            result.AddRange(rows.Select(e => new OperationsEventRow("Ca làm việc", e.OccurredAt,
                $"{e.Name} · {e.WorkDate:dd/MM/yyyy}", OperationEventLabel(e.Action), e.FullName, e.Reason)));
            // Automatic attendance closure has no human actor and is persisted in audit,
            // rather than workday_events (whose actor is mandatory).
            var automatic = await (from e in db.AuditLogs.AsNoTracking()
                join a in db.WorkdayAssignments on e.EntityKey equals a.AssignmentId.ToString()
                join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
                join u in db.Users on a.UserId equals u.UserId
                where e.EntityType == "Workday" && e.Action == "AUTO_END" && e.ActorUserId == null
                    && e.BranchId == branch && s.BranchId == branch && e.OccurredAt >= start
                    && (e.OccurredAt < end || !endExclusive && e.OccurredAt == end)
                orderby e.OccurredAt descending, e.AuditId descending
                select new { e.OccurredAt, e.Action, s.Name, a.WorkDate, u.FullName })
                .Take(limit).ToArrayAsync(ct);
            result.AddRange(automatic.Select(e => new OperationsEventRow("Ca làm việc", e.OccurredAt,
                $"{e.Name} · {e.WorkDate:dd/MM/yyyy}", OperationEventLabel(e.Action), e.FullName,
                "Hệ thống kết thúc chấm công khi hết giờ ca; vật tư đang giữ được đối soát riêng.")));
        }
        if (await access.Allows("INCIDENT_VIEW", branch, ct: ct))
        {
            var rows = await (from e in db.WorkEvents.AsNoTracking()
                join w in db.WorkItems on e.WorkId equals w.WorkId
                join u in db.Users on e.ActorUserId equals u.UserId
                where w.BranchId == branch && e.BranchId == branch && w.Kind == "INCIDENT"
                    && e.OccurredAt >= start && (e.OccurredAt < end || !endExclusive && e.OccurredAt == end)
                orderby e.OccurredAt descending, e.WorkEventId descending
                select new { e.OccurredAt, e.EventType, e.Reason, w.Title, u.FullName })
                .Take(limit).ToArrayAsync(ct);
            result.AddRange(rows.Select(e => new OperationsEventRow("Sự cố phòng", e.OccurredAt,
                e.Title, OperationEventLabel(e.EventType), e.FullName, e.Reason)));
        }
        if (await access.Allows("ROOM_VIEW", branch, ct: ct))
        {
            var rows = await (from e in db.RoomEvents.AsNoTracking()
                join r in db.Rooms on e.RoomId equals r.RoomId
                join u in db.Users on e.ActorUserId equals u.UserId
                where e.BranchId == branch && r.BranchId == branch && e.OccurredAt >= start && (e.OccurredAt < end || !endExclusive && e.OccurredAt == end)
                orderby e.OccurredAt descending, e.RoomEventId descending
                select new { e.OccurredAt, e.EventType, e.Reason, r.Code, u.FullName })
                .Take(limit).ToArrayAsync(ct);
            result.AddRange(rows.Select(e => new OperationsEventRow("Phòng", e.OccurredAt,
                $"Phòng {e.Code}", OperationEventLabel(e.EventType), e.FullName, e.Reason)));
        }
        if (await access.Allows("INVENTORY_VIEW", branch, ct: ct))
        {
            var rows = await (from e in db.DocumentEvents.AsNoTracking()
                join d in db.InventoryDocuments on e.DocumentId equals d.DocumentId
                join u in db.Users on e.ActorUserId equals u.UserId
                where e.BranchId == branch && d.BranchId == branch && e.OccurredAt >= start && (e.OccurredAt < end || !endExclusive && e.OccurredAt == end)
                orderby e.OccurredAt descending, e.DocumentEventId descending
                select new { e.OccurredAt, e.EventType, e.Reason, d.DocumentNo, d.Kind, u.FullName })
                .Take(limit).ToArrayAsync(ct);
            result.AddRange(rows.Select(e => new OperationsEventRow("Kho", e.OccurredAt,
                $"{InventoryDocumentLabel(e.Kind)} · {e.DocumentNo}", OperationEventLabel(e.EventType), e.FullName, e.Reason)));
        }
        if (await access.Allows("ASSET_VIEW", branch, ct: ct))
        {
            var rows = await (from e in db.AssetEvents.AsNoTracking()
                join a in db.Assets on e.AssetId equals a.AssetId
                join u in db.Users on e.ActorUserId equals u.UserId
                where e.BranchId == branch && e.OccurredAt >= start && (e.OccurredAt < end || !endExclusive && e.OccurredAt == end)
                orderby e.OccurredAt descending, e.AssetEventId descending
                select new { e.OccurredAt, e.EventType, e.Note, a.Name, a.AssetCode, u.FullName })
                .Take(limit).ToArrayAsync(ct);
            result.AddRange(rows.Select(e => new OperationsEventRow("Tài sản", e.OccurredAt,
                $"{e.Name} · {e.AssetCode}", OperationEventLabel(e.EventType), e.FullName, e.Note)));
        }
        return result.OrderByDescending(e => e.OccurredAt).ThenBy(e => e.Category).Take(limit).ToArray();
    }

    private static string InventoryDocumentLabel(string kind) => kind switch
    {
        "RECEIPT" => "Nhập kho", "ISSUE" => "Xuất kho", "RETURN" => "Hoàn kho",
        "TRANSFER" => "Điều chuyển", "TRANSFER_REQUEST" => "Yêu cầu điều chuyển",
        "TRANSFER_RECEIPT" => "Nhận điều chuyển", "STOCKTAKE" => "Kiểm kê",
        "ADJUSTMENT" => "Điều chỉnh", "MATERIAL_REQUEST" => "Yêu cầu vật tư", _ => kind
    };

    private static string OperationEventLabel(string action) => action.ToUpperInvariant() switch
    {
        "START" => "Bắt đầu", "END" => "Kết thúc ca", "AUTO_END" => "Tự kết thúc ca hết giờ",
        "CREATE" or "CREATED" => "Đã tạo", "ASSIGN" or "ASSIGNED" => "Phân công",
        "CANCEL" or "CANCELLED" => "Đã hủy", "SUBMIT" or "SUBMITTED" => "Đã gửi",
        "APPROVE" or "APPROVED" => "Đã duyệt", "REJECT" or "REJECTED" => "Đã từ chối",
        "POST" or "POSTED" => "Ghi sổ", "COMPLETE" => "Hoàn thành", "VERIFY" => "Xác nhận",
        "MOVE" => "Chuyển vị trí", "CONDITION" => "Cập nhật tình trạng",
        "ROOM_ISSUE_REPORTED" => "Phát hiện sự cố", "ROOM_ISSUE_CONFIRMED" => "Xác nhận sự cố",
        "ROOM_ISSUE_REPAIR_REQUESTED" => "Chuyển sửa chữa", "ROOM_ISSUE_REPAIR_CREATED" => "Tạo việc sửa chữa",
        "ROOM_ISSUE_REPAIR_COMPLETED" => "Sửa chữa hoàn tất", "ROOM_ISSUE_REINSPECTION_REQUIRED" => "Chờ kiểm tra sau khắc phục",
        "ROOM_ISSUE_CLOSED" => "Đã xử lý sự cố", "ROOM_ISSUE_REJECTED" => "Từ chối báo cáo",
        "REPLACEMENT_OUT" => "Đưa tài sản cũ ra khỏi phòng", "REPLACEMENT_IN" => "Lắp tài sản thay thế",
        _ => action
    };
}
