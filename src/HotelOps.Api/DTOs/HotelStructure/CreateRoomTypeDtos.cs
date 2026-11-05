using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HotelOps.Api.DTOs.HotelStructure;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateRoomTypeRequest(
    [Range(1, long.MaxValue)] long BranchId,
    [Required, StringLength(30)] string Code,
    [Required, StringLength(150)] string Name,
    [Range(1, int.MaxValue)] int StandardCleaningMinutes,
    [Range(1, long.MaxValue)] long DefaultChecklistId,
    [StringLength(4000)] string? Description = null,
    [Range(1, int.MaxValue)] int? StandardCapacity = null);

public sealed record CreatedRoomTypeDto(long Id, long BranchId, string Code, string Name, string? Description,
    int StandardCleaningMinutes, long DefaultChecklistId, int? StandardCapacity, bool IsActive);
