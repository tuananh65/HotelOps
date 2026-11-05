namespace HotelOps.Api.DTOs.HotelStructure;

public sealed record ChecklistCriterionRequest(string Text, string? Group, bool? Mandatory, string? PassCondition = null, string? Description = null);
public sealed record CreateChecklistRequest(string Code, string Name, IReadOnlyList<long> RoomTypeIds, IReadOnlyList<ChecklistCriterionRequest> Criteria, string? Description = null);
public sealed record UpdateChecklistRequest(IReadOnlyList<ChecklistCriterionRequest> Criteria, string Reason);
public sealed record ChecklistTransitionRequest(string Reason, bool Confirmation = true);
public sealed record ChecklistCriterionDto(long Id, string Text, string? Group, bool Mandatory, string? PassCondition, string? Description, int Order);
public sealed record ChecklistVersionDto(long Id, int Number, string Status, DateTime? EffectiveAt, long? PublishedBy, bool UsedByInspection, IReadOnlyList<ChecklistCriterionDto> Criteria);
public sealed record ChecklistDetailsDto(long Id, long BranchId, string Code, string Name, string? Description, bool Active,
    IReadOnlyList<long> RoomTypeIds, IReadOnlyList<ChecklistVersionDto> Versions);
