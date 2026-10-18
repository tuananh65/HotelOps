using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService
{
    private static Dictionary<string, object?> Snapshot(Asset a) => new()
    {
        ["asset_id"] = a.AssetId, ["asset_code"] = a.AssetCode, ["asset_name"] = a.AssetName,
        ["asset_type_id"] = a.AssetTypeId, ["branch_id"] = a.BranchId,
        ["serial_number"] = a.SerialNumber, ["brand"] = a.Brand, ["model"] = a.Model,
        ["purchase_date"] = a.PurchaseDate, ["warranty_expiry_date"] = a.WarrantyExpiryDate,
        ["acquisition_cost"] = a.AcquisitionCost, ["description"] = a.Description,
        ["condition_status"] = a.ConditionStatus, ["lifecycle_status"] = a.LifecycleStatus,
        ["current_room_id"] = a.CurrentRoomId, ["current_area_id"] = a.CurrentAreaId,
        ["current_department_id"] = a.CurrentDepartmentId, ["custodian_user_id"] = a.CustodianUserId,
        ["created_at"] = a.CreatedAt, ["updated_at"] = a.UpdatedAt
    };

    private void Audit(string entityType, long id, Asset asset, long actor, DateTime now,
        Dictionary<string, object?>? before, Dictionary<string, object?> after,
        string? reason = null, string? confirmationNote = null)
    {
        var changed = before is null ? null :
            after.Keys.Where(key => key != "updated_at" && !Equals(before[key], after[key])).ToArray();
        if (changed is { Length: 0 })
            return;
        // Context belongs to the action payload, not to Asset columns/ChangedFields.
        if (reason is not null) after["reason"] = reason;
        if (confirmationNote is not null) after["confirmation_note"] = confirmationNote;
        _dbContext.AuditLogs.Add(new AuditLog
        {
            BranchId = asset.BranchId, ActorUserId = actor, EntityType = entityType,
            EntityId = id.ToString(CultureInfo.InvariantCulture),
            Action = before is null ? "CREATE" : "UPDATE",
            OldValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = JsonSerializer.Serialize(after),
            ChangedFields = changed is null ? null : JsonSerializer.Serialize(changed),
            OccurredAt = now
        });
    }
}
