using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Inventory;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.MaterialRequests;

public sealed partial class MaterialRequestService(HotelOpsDbContext db, IInventoryIssuePosting posting, InventoryService inventory)
{
    private static ValidationException Invalid(string message) => new(message);
    private static decimal Qty(decimal q)
    {
        if (q < 0 || q > 999999999999999.999m || decimal.Round(q, 3) != q) throw Invalid("Quantity must be exact decimal(18,3) and nonnegative.");
        return q;
    }
    private static string Reason(string? s) => s?.Trim() is { Length: >= 10 } text ? text : throw Invalid("Reason must contain at least 10 characters.");
    private async Task Gate(string resource, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; SELECT @r;";
        var parameter = command.CreateParameter(); parameter.ParameterName = "@resource"; parameter.Value = resource; command.Parameters.Add(parameter);
        object? scalar;
        try { scalar = await command.ExecuteScalarAsync(ct); }
        catch (Microsoft.Data.SqlClient.SqlException) when (ct.IsCancellationRequested)
        {
            ct.ThrowIfCancellationRequested();
            throw;
        }
        var result = Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        if (result < 0) { ct.ThrowIfCancellationRequested(); throw new InvalidOperationException("Material Request lock unavailable."); }
    }
    private async Task Participant(long id, long branch, CancellationToken ct)
    {
        // Eligibility is enforced under retained User locks before MR resources.
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.UserId == id && x.PrimaryBranchId == branch, ct) && !await db.UserBranchAccesses.AnyAsync(x => x.UserId == id && x.BranchId == branch && x.RevokedAt == null, ct)) throw Invalid("Participant must be branch-related. This is not authorization.");
    }
    private async Task Approver(MaterialRequest r, long? id, long actor, CancellationToken ct)
    {
        if (id is null || id == r.RequesterId || id == actor) throw Invalid("An independent assigned approver is required.");
        await Participant(id.Value, r.BranchId, ct);
    }
    private static void RecheckDiscovery(MaterialRequest current, MaterialRequest discovered)
    {
        if (current.RequesterId != discovered.RequesterId || current.BranchId != discovered.BranchId
            || current.Status != discovered.Status || !current.RowVersion.SequenceEqual(discovered.RowVersion))
            throw Invalid("Request context changed during User lock acquisition. Reload.");
    }
    private static void Version(MaterialRequest r, string? expected)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(expected ?? ""); } catch (FormatException) { throw Invalid("Malformed ExpectedRowVersion."); }
        if (bytes.Length != 8 || !bytes.SequenceEqual(r.RowVersion)) throw Invalid("Stale or missing ExpectedRowVersion. Reload.");
    }
    private async Task<bool> NewWorkflow(MaterialRequest r, CancellationToken ct) =>
        await db.AuditLogs.AnyAsync(x => x.EntityType == "material_requests" && x.EntityId == r.RequestId.ToString() && x.NewValues != null && x.NewValues.Contains("REQUEST_CREATED"), ct)
        || await db.MaterialRequestApprovalCycles.AnyAsync(x => x.RequestId == r.RequestId, ct);
    private async Task<MaterialRequest?> Locked(long id, string? version, CancellationToken ct)
    {
        await Gate("HotelOps:Inventory:Catalog", ct);
        await Gate($"HotelOps:MaterialRequest:Request:{id}", ct);
        var trackedRequest = db.MaterialRequests.Local.Any(x => x.RequestId == id);
        var trackedLines = db.MaterialRequestLines.Local.Where(x => x.RequestId == id).ToArray();
        var r = await db.MaterialRequests.FromSqlInterpolated($"SELECT * FROM dbo.material_requests WITH (UPDLOCK,HOLDLOCK) WHERE request_id={id}")
            .Include(x => x.MaterialRequestLines).SingleOrDefaultAsync(ct);
        if (r is null) return null;
        if (trackedRequest) await db.Entry(r).ReloadAsync(ct);
        foreach (var line in trackedLines) await db.Entry(line).ReloadAsync(ct);
        Version(r, version);
        if (!await NewWorkflow(r, ct)) throw Invalid("Legacy request mutation cannot be reconciled safely; read only.");
        return r;
    }
    private static void State(MaterialRequest r, params string[] states)
    {
        if (!states.Contains(r.Status)) throw Invalid("Command is not allowed in the current state.");
    }
    private async Task<bool> EverIssued(MaterialRequest r, CancellationToken ct) =>
        await db.MaterialFulfillments.AnyAsync(x => x.RequestId == r.RequestId && x.StockDocumentId != null, ct)
        || r.MaterialRequestLines.Any(x => x.FulfilledQty != 0);
    private static object Snapshot(MaterialRequest r) => new
    {
        r.RequestNo, r.BranchId, r.DepartmentId, r.RequesterId, r.SourceWarehouseId, r.Purpose, NeededAt = Utc(r.NeededAt), r.PriorityId,
        r.Status, r.SubmittedAt, r.CompletedAt, r.UpdatedAt, r.CancellationReason, r.ReceivingRoomId, r.ReceivingAreaId,
        r.IncidentId, r.RemainderClosedAt, r.RemainderClosedBy, r.RemainderCloseReason,
        Lines = r.MaterialRequestLines.OrderBy(x => x.LineNo).Select(x => new { x.LineId, x.LineNo, x.ItemId, x.UomId, x.RequestedQty, x.ApprovedQty, x.FulfilledQty, x.Note, x.IsActive }).ToArray()
    };
    private void Audit(MaterialRequest r, long actor, string action, string evt, object? before, object? extra = null, string table = "material_requests", long? id = null, object? state = null)
    {
        var after = state ?? Snapshot(r);
        var oldJson = before is null ? null : JsonSerializer.Serialize(before);
        var newJson = JsonSerializer.Serialize(after);
        using var current = JsonDocument.Parse(newJson);
        using var prior = JsonDocument.Parse(oldJson ?? "{}");
        var changed = current.RootElement.EnumerateObject().Where(x => !prior.RootElement.TryGetProperty(x.Name, out var p) || p.GetRawText() != x.Value.GetRawText()).Select(x => x.Name).ToArray();
        db.AuditLogs.Add(new() { BranchId = r.BranchId, ActorUserId = actor, EntityType = table, EntityId = (id ?? r.RequestId).ToString(CultureInfo.InvariantCulture), Action = action,
            OldValues = oldJson, NewValues = JsonSerializer.Serialize(new { BusinessEvent = evt, State = after, Extra = extra }), ChangedFields = JsonSerializer.Serialize(changed), OccurredAt = DateTime.UtcNow });
    }
    private async Task<string> Number(bool fulfillment, CancellationToken ct)
    {
        while (true)
        {
            var date = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture); var prefix = (fulfillment ? "FUL-" : "MR-") + date + "-";
            await Gate($"HotelOps:MaterialRequest:Number:{prefix}", ct);
            if (date != DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)) continue;
            var numbers = fulfillment ? await db.MaterialFulfillments.Where(x => x.FulfillmentNo.StartsWith(prefix)).Select(x => x.FulfillmentNo).ToListAsync(ct)
                : await db.MaterialRequests.Where(x => x.RequestNo.StartsWith(prefix)).Select(x => x.RequestNo).ToListAsync(ct);
            BigInteger maximum = 0;
            foreach (var value in numbers) { var suffix = value[prefix.Length..]; if (suffix.Length > 0 && suffix.All(char.IsAsciiDigit) && BigInteger.TryParse(suffix, out var n)) maximum = BigInteger.Max(maximum, n); }
            var result = prefix + (maximum + 1).ToString("D3", CultureInfo.InvariantCulture);
            return result.Length <= 50 ? result : throw new InvalidOperationException("Number space exhausted.");
        }
    }
    private async Task<MaterialRequestApprovalCycle> Latest(long id, CancellationToken ct)
    {
        var trackedCycles = db.MaterialRequestApprovalCycles.Local.Where(x => x.RequestId == id).Select(x => x.ApprovalCycleId).ToArray();
        var cycle = await db.MaterialRequestApprovalCycles.Include(x => x.MaterialRequestApprovalLineSnapshots)
            .Where(x => x.RequestId == id).OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct) ?? throw Invalid("Approval cycle missing.");
        if (trackedCycles.Contains(cycle.ApprovalCycleId))
        {
            await db.Entry(cycle).ReloadAsync(ct);
            foreach (var line in cycle.MaterialRequestApprovalLineSnapshots.ToArray()) await db.Entry(line).ReloadAsync(ct);
        }
        return cycle;
    }
    private async Task Cycle(MaterialRequest r, long actor, long? approver,
        IReadOnlyDictionary<long, UserEligibilityLock.LockedUser?> lockedUsers, CancellationToken ct)
    {
        // BASE routing is the explicit selector (or latest selector on edit).
        // Never select another User or acquire a User lock after MR resources.
        await ValidateContent(r, true, false, ct); await Approver(r, approver, actor, ct);
        UserEligibilityLock.RequireEligible(approver!.Value, lockedUsers);
        var prior = await db.MaterialRequestApprovalCycles.Where(x => x.RequestId == r.RequestId).OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct);
        if (prior != null) await db.Entry(prior).ReloadAsync(ct);
        if (prior?.Status == "PENDING") { prior.Status = "SUPERSEDED"; prior.ResolvedAt = DateTime.UtcNow; }
        var cycle = new MaterialRequestApprovalCycle { Request = r, RevisionNo = (prior?.RevisionNo ?? 0) + 1, SubmittedBy = actor, AssignedApproverId = approver!.Value, Status = "PENDING", SubmittedAt = DateTime.UtcNow };
        foreach (var l in r.MaterialRequestLines.Where(x => x.IsActive))
        {
            l.ApprovedQty = null;
            cycle.MaterialRequestApprovalLineSnapshots.Add(new() { RequestLine = l, LineNo = l.LineNo, ItemId = l.ItemId, UomId = l.UomId, RequestedQty = l.RequestedQty, Note = l.Note });
        }
        db.MaterialRequestApprovalCycles.Add(cycle); r.Status = "PENDING_APPROVAL"; r.SubmittedAt = cycle.SubmittedAt;
    }
    private async Task ValidateContent(MaterialRequest r, bool submit, bool changedIncident, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Purpose)) throw Invalid("Purpose is required.");
        if (!await db.Branches.AnyAsync(x => x.BranchId == r.BranchId && x.IsActive, ct) || !await db.Departments.AnyAsync(x => x.DepartmentId == r.DepartmentId && x.BranchId == r.BranchId && x.IsActive, ct)) throw Invalid("Active branch and receiving department must match.");
        if (submit && (r.SourceWarehouseId is null || r.NeededAt is null)) throw Invalid("Submit requires SourceWarehouseId and NeededAt.");
        if (r.SourceWarehouseId is long w && !await db.Warehouses.AnyAsync(x => x.WarehouseId == w && x.BranchId == r.BranchId && x.IsActive, ct)) throw Invalid("Source warehouse must be active and in request branch.");
        if (r.ReceivingRoomId != null && r.ReceivingAreaId != null) throw Invalid("Choose at most one receiving Room/Area.");
        if (r.ReceivingRoomId is long room && !await db.Rooms.AnyAsync(x => x.RoomId == room && x.BranchId == r.BranchId && x.IsActive, ct)) throw Invalid("Receiving room is invalid.");
        if (r.ReceivingAreaId is long area && !await db.Areas.AnyAsync(x => x.AreaId == area && x.BranchId == r.BranchId && x.IsActive, ct)) throw Invalid("Receiving area is invalid.");
        if (r.PriorityId is long p && !await db.ReferenceValues.AnyAsync(x => x.ReferenceId == p && x.GroupCode == "PRIORITY" && x.IsActive, ct)) throw Invalid("Priority reference is invalid.");
        if (changedIncident && r.IncidentId is long incident && !await db.Incidents.AnyAsync(x => x.IncidentId == incident && x.BranchId == r.BranchId && (x.Status == "OPEN" || x.Status == "IN_PROGRESS" || x.Status == "RESOLVED"), ct)) throw Invalid("Incident must be open and in request branch when linking.");
        var lines = r.MaterialRequestLines.Where(x => x.IsActive).ToArray();
        if (lines.Length == 0) throw Invalid("At least one active line is required.");
        foreach (var l in lines)
        {
            if (Qty(l.RequestedQty) == 0) throw Invalid("Requested quantity must be positive.");
            var item = await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(x => x.ItemId == l.ItemId && x.IsActive, ct);
            if (item is null || !await db.ItemCategories.AnyAsync(x => x.ItemCategoryId == item.CategoryId && x.IsActive, ct) || !await db.UnitsOfMeasures.AnyAsync(x => x.UomId == item.BaseUomId && x.IsActive, ct) || !await db.UnitsOfMeasures.AnyAsync(x => x.UomId == l.UomId && x.IsActive, ct)) throw Invalid("Item/category/UOM must be active.");
            if (l.UomId != item.BaseUomId && !await db.UomConversions.AnyAsync(x => x.ItemId == l.ItemId && x.FromUomId == l.UomId && x.ToUomId == item.BaseUomId && x.IsActive && x.ConversionFactor > 0, ct)) throw Invalid("Active direct item-specific conversion is required.");
        }
    }
}
