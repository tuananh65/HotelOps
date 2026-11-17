using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed record AssetCrossBranchDecisionInput(string Action, string? Reason, string? ConfirmationNote);

public sealed partial class AssetCrossBranchTransfer
{
    private sealed class TransferRequest
    {
        public long SourceBranchId { get; set; }
        public long DestinationBranchId { get; set; }
        public long? SourceRoomId { get; set; }
        public long? SourceAreaId { get; set; }
        public long? TargetRoomId { get; set; }
        public long? TargetAreaId { get; set; }
        public string Reason { get; set; } = "";
        public string? ConfirmationNote { get; set; }
        public string Snapshot { get; set; } = "";
    }

    private Task<TransferRequest?> RequestAsync(long handover, CancellationToken ct) => db.Database.SqlQuery<TransferRequest>($"""
        SELECT t.source_branch_id AS SourceBranchId,t.destination_branch_id AS DestinationBranchId,
          t.source_room_id AS SourceRoomId,t.source_area_id AS SourceAreaId,t.target_room_id AS TargetRoomId,
          t.target_area_id AS TargetAreaId,t.reason AS Reason,t.confirmation_note AS ConfirmationNote,v.snapshot_json AS Snapshot
        FROM dbo.asset_transfer_requests t WITH(HOLDLOCK)
        JOIN dbo.asset_handover_versions v WITH(HOLDLOCK) ON v.handover_id=t.handover_id
        JOIN dbo.asset_handovers h WITH(HOLDLOCK) ON h.handover_id=t.handover_id
        JOIN dbo.audit_logs a WITH(HOLDLOCK) ON a.audit_id=v.submission_audit_id
        WHERE t.handover_id={handover} AND a.entity_type='asset_handovers' AND a.action='CREATE'
          AND a.entity_id=CONVERT(varchar(30),h.handover_id) AND a.branch_id=t.source_branch_id
          AND a.actor_user_id=h.created_by AND a.occurred_at=h.created_at AND a.old_values IS NULL
          AND a.new_values=v.snapshot_json AND ISJSON(v.snapshot_json)=1
        """).SingleOrDefaultAsync(ct);

    private static bool Matches(TransferRequest request, AssetHandover handover)
    {
        try
        {
            using var document = JsonDocument.Parse(request.Snapshot);
            var root = document.RootElement;
            long? Id(string key) => root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var id) ? id : null;
            string? Text(string key) => root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            return Id("handover_id") == handover.HandoverId && Id("asset_id") == handover.AssetId
                && Id("from_branch_id") == request.SourceBranchId && Id("to_branch_id") == request.DestinationBranchId
                && Id("from_department_id") == handover.FromDepartmentId && Id("to_department_id") == handover.ToDepartmentId
                && Id("from_user_id") == handover.FromUserId && Id("to_user_id") == handover.ToUserId
                && Id("source_room_id") == request.SourceRoomId && Id("source_area_id") == request.SourceAreaId
                && Id("target_room_id") == request.TargetRoomId && Id("target_area_id") == request.TargetAreaId
                && Text("reason") == request.Reason && Text("confirmation_note") == request.ConfirmationNote
                && Text("condition_note") == handover.ConditionNote && Text("handover_status") == "PENDING";
        }
        catch (JsonException) { return false; }
    }

    public async Task<object> DecideAsync(long id, long handoverId, string? expected,
        AssetCrossBranchDecisionInput input, CancellationToken ct)
    {
        var action = input.Action?.Trim().ToUpperInvariant();
        var reason = input.Reason?.Trim(); var confirmation = input.ConfirmationNote?.Trim();
        if (action is not ("ACCEPT" or "REJECT" or "CANCEL") || reason?.Length > 4000 || confirmation?.Length > 4000
            || action != "ACCEPT" && (reason?.Length ?? 0) < 10)
            throw new InsightException(400, "MSG03", "Select a valid decision; rejection/cancellation requires a reason of 10-4000 characters.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await RequireStorageAsync(ct);
        var asset = await AssetAsync(id, ct);
        var token = await AssetVersion.RequireAsync(db, id, expected, ct);
        var handover = await db.AssetHandovers.FromSqlInterpolated($"SELECT * FROM dbo.asset_handovers WITH(UPDLOCK,HOLDLOCK) WHERE handover_id={handoverId} AND asset_id={id}")
            .SingleOrDefaultAsync(ct) ?? throw new InsightException(404, "HANDOVER_UNAVAILABLE", "Handover unavailable.");
        var request = await RequestAsync(handoverId, ct);
        if (request == null || !Matches(request, handover))
            throw new InsightException(409, "HANDOVER_PROVENANCE_UNAVAILABLE", "Transfer submission evidence is incomplete.");
        if (handover.HandoverStatus != "PENDING") throw new InsightException(409, "HANDOVER_FINALIZED", "Handover already decided.");
        if (asset.BranchId != request.SourceBranchId || asset.CurrentRoomId != request.SourceRoomId || asset.CurrentAreaId != request.SourceAreaId
            || asset.CurrentDepartmentId != handover.FromDepartmentId || asset.CustodianUserId != handover.FromUserId)
            throw new InsightException(409, "CUSTODY_CHANGED", "Asset custody or placement differs from the submitted snapshot.");
        if (action == "CANCEL")
        {
            if (handover.CreatedBy != access.UserId) throw new InsightException(403, "ACCESS_DENIED", "Only the authorized sender can cancel.");
            await placement.RequireAsync(request.SourceBranchId, ct, "ASSET_TRANSFER");
            await placement.RequireAsync(request.DestinationBranchId, ct, "ASSET_TRANSFER");
        }
        else if (handover.ToUserId != access.UserId || handover.ToDepartmentId is not {} department
            || !await AssetHandoverAuthority.AllowsAsync(db, access.UserId, request.DestinationBranchId, department, true, ct)
            || !await ReceiverCanReadSourceAsync(access.UserId, request.SourceBranchId, ct))
            throw new InsightException(403, "ACCESS_DENIED", "Current receiving Head authority and source asset read access are required.");

        var accept = action == "ACCEPT";
        if (accept)
        {
            await TargetAsync(request.DestinationBranchId, request.TargetRoomId, request.TargetAreaId, ct);
            if (await HasOpenWorkAsync(id, ct) && string.IsNullOrWhiteSpace(confirmation))
                throw new InsightException(400, "SOURCE_WORK_CONFIRMATION_REQUIRED", "Confirm that linked open work remains at its source branch.");
        }
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        // CANCEL is a rejected handover with an explicit cancellation event, never a new physical enum.
        handover.HandoverStatus = accept ? "ACCEPTED" : "REJECTED";
        handover.AcceptedAt = accept ? now : null;
        var receipt = new AuditLog
        {
            EntityType = "asset_handovers", EntityId = handoverId.ToString(CultureInfo.InvariantCulture),
            BranchId = request.SourceBranchId, ActorUserId = access.UserId, Action = "UPDATE", OccurredAt = now,
            OldValues = request.Snapshot,
            NewValues = JsonSerializer.Serialize(new { handover_id = handoverId, asset_id = id,
                branch_id = request.SourceBranchId, from_branch_id = request.SourceBranchId, to_branch_id = request.DestinationBranchId,
                from_department_id = handover.FromDepartmentId, to_department_id = handover.ToDepartmentId,
                from_user_id = handover.FromUserId, to_user_id = handover.ToUserId,
                handover_status = handover.HandoverStatus, decision = action, accepted_at = handover.AcceptedAt,
                decided_at = now, reason, confirmation_note = confirmation, sender_id = handover.CreatedBy,
                source_work_remains_at_origin = true, notification_kind = "ASSET_HANDOVER_DECIDED", notification_version = 1 })
        };
        db.AuditLogs.Add(receipt); await db.SaveChangesAsync(ct);
        object? linked = null;
        if (accept) linked = await CaptureWorkAsync(asset, handoverId, receipt.AuditId, now, ct);
        var branch = accept ? request.DestinationBranchId : asset.BranchId;
        var room = accept ? request.TargetRoomId : asset.CurrentRoomId;
        var area = accept ? request.TargetAreaId : asset.CurrentAreaId;
        var targetDepartment = accept ? handover.ToDepartmentId : asset.CurrentDepartmentId;
        var custodian = accept ? handover.ToUserId : asset.CustodianUserId;
        if (await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.assets SET branch_id={branch},current_room_id={room},current_area_id={area},
              current_department_id={targetDepartment},custodian_user_id={custodian},updated_at={now}
            WHERE asset_id={id} AND row_version={token}
            """, ct) != 1) throw new InsightException(412, "MSG04", "Asset changed; reload before deciding.");
        if (accept)
        {
            var movement = new AssetLocationHistory { AssetId = id, FromLocationType = asset.CurrentRoomId != null ? "ROOM" : "AREA",
                FromLocationId = asset.CurrentRoomId ?? asset.CurrentAreaId, ToLocationType = room != null ? "ROOM" : "AREA",
                ToLocationId = (room ?? area)!.Value, EffectiveAt = now, MovedBy = access.UserId, Reason = request.Reason };
            db.AssetLocationHistories.Add(movement); await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog { EntityType = "assets", EntityId = id.ToString(CultureInfo.InvariantCulture), BranchId = request.SourceBranchId,
                ActorUserId = access.UserId, Action = "UPDATE", OccurredAt = now,
                OldValues = JsonSerializer.Serialize(AssetEditService.Snapshot(asset)),
                NewValues = JsonSerializer.Serialize(new { branch_id = branch, current_department_id = targetDepartment,
                    custodian_user_id = custodian, current_room_id = room, current_area_id = area, handover_id = handoverId, linked_source_work = linked }),
                ChangedFields = "[\"branch_id\",\"current_department_id\",\"custodian_user_id\",\"current_room_id\",\"current_area_id\"]" });
            db.AuditLogs.Add(new AuditLog { EntityType = "asset_location_history", EntityId = movement.LocationHistoryId.ToString(CultureInfo.InvariantCulture),
                BranchId = request.SourceBranchId, ActorUserId = access.UserId, Action = "CREATE", OccurredAt = now,
                NewValues = JsonSerializer.Serialize(new { asset_id = id, from_branch_id = request.SourceBranchId, to_branch_id = branch,
                    from_location_type = movement.FromLocationType, from_location_id = movement.FromLocationId,
                    to_location_type = movement.ToLocationType, to_location_id = movement.ToLocationId,
                    effective_at = now, recorded_at = now, moved_by = access.UserId, reason = request.Reason, handover_id = handoverId }) });
            await db.SaveChangesAsync(ct);
        }
        // Revalidate authority without requiring the receiver to own source work or granting it at destination.
        if (action != "CANCEL" && (!await AssetHandoverAuthority.AllowsAsync(db, access.UserId, request.DestinationBranchId, handover.ToDepartmentId!.Value, true, ct)
            || !await ReceiverCanReadSourceAsync(access.UserId, request.SourceBranchId, ct)))
            throw new InsightException(403, "ACCESS_DENIED", "Receiving authority changed before commit.");
        if (action == "CANCEL")
        {
            await placement.RequireAsync(request.SourceBranchId, ct, "ASSET_TRANSFER");
            await placement.RequireAsync(request.DestinationBranchId, ct, "ASSET_TRANSFER");
        }
        var result = new { HandoverId = handoverId, State = accept ? "ACKNOWLEDGED" : action == "CANCEL" ? "CANCELLED" : "REJECTED",
            Version = await AssetVersion.ReadAsync(db, id, ct) };
        await tx.CommitAsync(ct); return result;
    }
}
