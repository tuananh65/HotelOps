using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HotelOps.Api.DTOs.HotelStructure;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateFloorRequest([Required] string Code, [Required] string Name,
    [Required] string Reason, string? Description = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateAreaRequest([Required] string Code, [Required] string Name,
    [Required] string Reason, long? FloorId = null, string? Description = null);

public sealed record EditableStructureDto(string Kind, long Id, long BranchId, string Code,
    string Name, string? Description, long? FloorId, int? FloorNumber, bool IsActive);
