using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

// Called inside the authorized report snapshot transaction. Never reconstruct
// historical branch ownership from an asset's current location or department.
internal static class ReportAssetHistory
{
    internal sealed record Row(long AuditId, long AssetId, string EventType, long RecordId,
        long SourceBranchId, long DestinationBranchId, DateTime OccurredAt,
        string? FromLocationType, long? FromLocationId, string? ToLocationType, long? ToLocationId,
        long? FromDepartmentId, long? ToDepartmentId, long? FromUserId, long? ToUserId, string? Status);
    internal sealed record Result(Row[] Rows, bool MissingEvidence);

    internal static async Task<Result> ReadAsync(HotelOpsDbContext db, IQueryable<Asset> assets,
        long[] authorizedBranches, DateTime start, DateTime end, CancellationToken ct)
    {
        var ids = assets.Select(a => a.AssetId);
        var moves = await db.AssetLocationHistories.AsNoTracking()
            .Where(h => ids.Contains(h.AssetId) && h.EffectiveAt >= start && h.EffectiveAt < end)
            .OrderBy(h => h.LocationHistoryId).Take(50001).ToArrayAsync(ct);
        var handovers = await db.AssetHandovers.AsNoTracking()
            .Where(h => ids.Contains(h.AssetId) && h.CreatedAt < end
                && (h.CreatedAt >= start || db.AuditLogs.Any(a => a.EntityType == "asset_handovers"
                    && a.EntityId == h.HandoverId.ToString() && a.Action == "UPDATE"
                    && a.OccurredAt >= start && a.OccurredAt < end
                    && a.BranchId != null && authorizedBranches.Contains(a.BranchId.Value))))
            .OrderBy(h => h.HandoverId).Take(50001).ToArrayAsync(ct);
        if (moves.Length + handovers.Length > 50000)
            throw new InsightException(422, "REPORT_ROW_LIMIT", "Asset history exceeds the report limit; narrow the period.");
        var moveIds = moves.Select(h => h.LocationHistoryId.ToString(CultureInfo.InvariantCulture)).ToArray();
        var handoverIds = handovers.Select(h => h.HandoverId.ToString(CultureInfo.InvariantCulture)).ToArray();
        var audits = await db.AuditLogs.AsNoTracking().Where(a => (a.Action == "CREATE"
                || a.Action == "UPDATE" && a.OccurredAt >= start && a.OccurredAt < end)
            && a.BranchId != null && authorizedBranches.Contains(a.BranchId.Value)
            && (a.EntityType == "asset_location_history" && moveIds.Contains(a.EntityId)
                || a.EntityType == "asset_handovers" && handoverIds.Contains(a.EntityId)))
            .OrderBy(a => a.AuditId).Take(50001).ToArrayAsync(ct);
        if (audits.Length > 50000)
            throw new InsightException(422, "REPORT_ROW_LIMIT", "Asset history evidence exceeds the report limit.");
        var rows = new List<Row>();
        var missing = false;
        foreach (var move in moves)
        {
            var proof = audits.Where(a => a.EntityType == "asset_location_history"
                && a.EntityId == move.LocationHistoryId.ToString(CultureInfo.InvariantCulture)
                && a.Action == "CREATE" && a.ActorUserId == move.MovedBy).ToArray();
            if (proof.Length != 1) { missing = true; continue; }
            using var json = Parse(proof[0].NewValues);
            if (json == null) { missing = true; continue; }
            var p = json.RootElement;
            if (!Branches(p, proof[0], authorizedBranches, out var source, out var destination)
                || Number(p, "asset_id") != move.AssetId
                || Text(p, "from_location_type") != move.FromLocationType || Number(p, "from_location_id") != move.FromLocationId
                || Text(p, "to_location_type") != move.ToLocationType || Number(p, "to_location_id") != move.ToLocationId
                || Number(p, "moved_by") != move.MovedBy || Timestamp(p, "effective_at") != move.EffectiveAt
                || (Timestamp(p, "recorded_at") is { } recorded ? recorded != proof[0].OccurredAt || recorded < move.EffectiveAt : proof[0].OccurredAt != move.EffectiveAt))
            { missing = true; continue; }
            rows.Add(new(proof[0].AuditId, move.AssetId, "LOCATION_CHANGED", move.LocationHistoryId,
                source, destination, move.EffectiveAt, move.FromLocationType, move.FromLocationId,
                move.ToLocationType, move.ToLocationId, null, null, null, null, null));
        }
        foreach (var handover in handovers)
        {
            var proof = audits.Where(a => a.EntityType == "asset_handovers"
                && a.EntityId == handover.HandoverId.ToString(CultureInfo.InvariantCulture)
                && a.Action == "CREATE" && a.OccurredAt == handover.CreatedAt && a.ActorUserId == handover.CreatedBy).ToArray();
            if (proof.Length != 1) { missing = true; continue; }
            using var json = Parse(proof[0].NewValues);
            if (json == null) { missing = true; continue; }
            var p = json.RootElement;
            if (!Branches(p, proof[0], authorizedBranches, out var source, out var destination)
                || Number(p, "asset_id") != handover.AssetId || Number(p, "handover_id") != handover.HandoverId
                || Number(p, "from_department_id") != handover.FromDepartmentId || Number(p, "to_department_id") != handover.ToDepartmentId
                || Number(p, "from_user_id") != handover.FromUserId || Number(p, "to_user_id") != handover.ToUserId
                || Timestamp(p, "created_at") != handover.CreatedAt)
            { missing = true; continue; }
            // This is the immutable submission event, not an inferred acceptance.
            if (handover.CreatedAt >= start) rows.Add(new(proof[0].AuditId, handover.AssetId, "HANDOVER_SUBMITTED", handover.HandoverId,
                source, destination, handover.CreatedAt, null, null, null, null,
                handover.FromDepartmentId, handover.ToDepartmentId, handover.FromUserId, handover.ToUserId,
                Text(p, "handover_status")));
            foreach (var decision in audits.Where(a => a.EntityType == "asset_handovers" && a.Action == "UPDATE"
                && a.EntityId == handover.HandoverId.ToString(CultureInfo.InvariantCulture)))
            {
                using var after = Parse(decision.NewValues);
                using var before = Parse(decision.OldValues);
                if (after == null || before == null) { missing = true; continue; }
                var state = after.RootElement;
                var status = Text(state, "handover_status");
                if (status is not ("ACCEPTED" or "REJECTED") || Text(before.RootElement, "handover_status") != Text(p, "handover_status")
                    || decision.ActorUserId != handover.ToUserId || decision.OccurredAt < handover.CreatedAt
                    || Number(state, "asset_id") != handover.AssetId || Number(state, "handover_id") != handover.HandoverId
                    || Number(state, "from_department_id") != handover.FromDepartmentId || Number(state, "to_department_id") != handover.ToDepartmentId
                    || Number(state, "from_user_id") != handover.FromUserId || Number(state, "to_user_id") != handover.ToUserId
                    || status == "ACCEPTED" && Timestamp(state, "accepted_at") != decision.OccurredAt)
                { missing = true; continue; }
                rows.Add(new(decision.AuditId, handover.AssetId, "HANDOVER_" + status, handover.HandoverId,
                    source, destination, decision.OccurredAt, null, null, null, null,
                    handover.FromDepartmentId, handover.ToDepartmentId, handover.FromUserId, handover.ToUserId, status));
            }
        }
        return new(rows.OrderBy(r => r.OccurredAt).ThenBy(r => r.AuditId).ToArray(), missing);
    }

    private static bool Branches(JsonElement p, AuditLog audit, long[] allowed, out long source, out long destination)
    {
        source = Number(p, "from_branch_id") ?? 0;
        destination = Number(p, "to_branch_id") ?? 0;
        return source > 0 && destination > 0 && (audit.BranchId == source || audit.BranchId == destination)
            && allowed.Contains(source) && allowed.Contains(destination);
    }
    private static JsonDocument? Parse(string? value)
    {
        try { var doc = JsonDocument.Parse(value ?? "null"); if (doc.RootElement.ValueKind == JsonValueKind.Object) return doc; doc.Dispose(); }
        catch (JsonException) { }
        return null;
    }
    private static long? Number(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : null;
    private static string? Text(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static DateTime? Timestamp(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetDateTime(out var d) ? d : null;
}
