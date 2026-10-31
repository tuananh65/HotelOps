namespace HotelOps.Api.Entities;

// Opt-in Database First projections; generated legacy entities remain unchanged.
public sealed class ChecklistScopeV1
{
    public long ChecklistId { get; set; }
    public long? BranchId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class ChecklistRoomTypeV1
{
    public long ChecklistId { get; set; }
    public long RoomTypeId { get; set; }
}
public sealed class ChecklistCriterionV1
{
    public long ItemId { get; set; }
    public string? Group { get; set; }
}
