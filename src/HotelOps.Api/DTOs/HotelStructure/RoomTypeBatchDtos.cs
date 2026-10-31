using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HotelOps.Api.DTOs.HotelStructure;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateRoomTypeRequest(
    [Required, StringLength(150)] string Name,
    [Range(1, int.MaxValue)] int StandardCleaningMinutes,
    [Range(1, long.MaxValue)] long DefaultChecklistId,
    [StringLength(4000)] string? Description = null,
    [StringLength(30)] string? Code = null);

public sealed record RoomTypeDetailsDto(long Id, long? BranchId, string Code, string Name, string? Description,
    int? StandardCleaningMinutes, long? DefaultChecklistId, int? StandardCapacity, bool IsActive);
public sealed record RoomTypeListItem(RoomTypeDetailsDto Details, int ActiveRooms, bool CanUpdate);
public sealed record RoomTypePage(IReadOnlyList<RoomTypeListItem> Items, int Total, int Page, int PageSize,
    long? BranchId, bool? IsActive, string? Search, string Sort, bool Descending);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateRoomRequest(
    [Range(1, long.MaxValue)] long BranchId,
    [Range(1, long.MaxValue)] long FloorId,
    [Range(1, long.MaxValue)] long RoomTypeId,
    [Required, StringLength(30)] string RoomNumber,
    [Range(1, long.MaxValue)] long? AreaId = null,
    [StringLength(4000)] string? Notes = null);

public sealed record CreatedRoomDto(long Id, long BranchId, long FloorId, long? AreaId, long RoomTypeId,
    string RoomNumber, string? Notes, string OperationalStatus, bool IsActive);
