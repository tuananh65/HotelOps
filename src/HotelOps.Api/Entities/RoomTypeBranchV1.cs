namespace HotelOps.Api.Entities;

// Controlled Database First read model for the explicitly deployed UC017 extension.
// The original RoomType remains the six-column legacy-compatible table projection.
public sealed class RoomTypeBranchV1
{
    public long RoomTypeId { get; set; }
    public string TypeCode { get; set; } = null!;
    public string TypeName { get; set; } = null!;
    public string? Description { get; set; }
    public int? StandardCapacity { get; set; }
    public bool IsActive { get; set; }
    public long? BranchId { get; set; }
    public int? StandardCleaningMinutes { get; set; }
    public long? DefaultChecklistId { get; set; }
    public Branch? Branch { get; set; }
    public InspectionChecklist? DefaultChecklist { get; set; }
}
