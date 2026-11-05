using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Rooms;

// SQL-backed read contract. Status codes remain persisted codes, never derived lifecycle states.
public sealed class RoomDirectoryQuery : IValidatableObject
{
    public long BranchId { get; init; }
    public bool AllBranches { get; init; }
    public string Sort { get; init; } = "floor";
    public bool Descending { get; init; }

    [Range(1, long.MaxValue)]
    public long? FloorId { get; init; }

    [Range(1, long.MaxValue)]
    public long? AreaId { get; init; }

    [Range(1, long.MaxValue)]
    public long? RoomTypeId { get; init; }

    [StringLength(200)]
    public string? Search { get; init; }

    public string? OperationalStatusCode { get; init; }
    public bool? IsActive { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AllBranches ? BranchId != 0 : BranchId <= 0)
            yield return new ValidationResult("Supply a positive BranchId or AllBranches=true, exclusively.", [nameof(BranchId), nameof(AllBranches)]);
        if (Sort is not ("floor" or "roomNumber" or "roomType" or "status"))
            yield return new ValidationResult("Sort must be floor, roomNumber, roomType or status.", [nameof(Sort)]);
        // Exact stored codes from CK_rooms_operational_status, not display/lifecycle states.
        if (OperationalStatusCode is not null && OperationalStatusCode is not
            ("AVAILABLE" or "OCCUPIED" or "DIRTY" or "CLEANING" or "INSPECTION" or "READY" or "OUT_OF_SERVICE"))
            yield return new ValidationResult("Unknown stored room status code.", [nameof(OperationalStatusCode)]);
        if (Page > 0 && PageSize > 0 && (long)(Page - 1) * PageSize > int.MaxValue)
            yield return new ValidationResult("The requested page offset is too large.", [nameof(Page)]);
    }
}

public sealed record RoomDirectoryFloorDto(long Id, string Code, string Name, int? Number);
public sealed record RoomDirectoryAreaDto(long Id, string Code, string Name);
public sealed record RoomDirectoryTypeDto(long Id, string Code, string Name);
public sealed record RoomDirectoryItemDto(long Id, long BranchId, string RoomNumber,
    RoomDirectoryFloorDto Floor, RoomDirectoryAreaDto? Area, RoomDirectoryTypeDto RoomType,
    string OperationalStatusCode, bool IsActive);
public sealed record RoomDirectoryResponse(IReadOnlyList<RoomDirectoryItemDto> Items,
    int Page, int PageSize, long TotalCount);

public sealed record RoomStatusSourceDto(string? Type, long? Id, string Availability);
public sealed record RoomStatusEventDto(long Id, string? FromStatusCode, string ToStatusCode,
    DateTime ChangedAtUtc, string? Reason, RoomStatusSourceDto Source);
public sealed record RoomCurrentStatusEvidenceDto(string Availability, string? Reason,
    DateTime? RecordedAtUtc, RoomStatusSourceDto? Source);
public sealed record RoomDirectoryDetailDto(RoomDirectoryItemDto Room, string? Description,
    RoomCurrentStatusEvidenceDto CurrentStatusEvidence, string? HistoryUrl = null);
public sealed record RoomDirectoryHistoryDto(IReadOnlyList<RoomStatusEventDto> Items, int Page,
    int PageSize, long TotalCount, string Coverage = "RECORDED_STATUS_EVENTS_ONLY");
