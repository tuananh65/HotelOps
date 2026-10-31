using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HotelOps.Api.DTOs.HotelStructure;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateFloorRequest([Required] string Code, [Required] string Name,
    int? FloorNumber = null, string? Description = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAreaRequest([Required] string Code, [Required] string Name,
    long? FloorId = null, string? Description = null);

public sealed record CreatedFloorDto(long Id, long BranchId, string Code, string Name,
    int? FloorNumber, string? Description, bool IsActive);
public sealed record CreatedAreaDto(long Id, long BranchId, string Code, string Name,
    long? FloorId, string? Description, bool IsActive);
