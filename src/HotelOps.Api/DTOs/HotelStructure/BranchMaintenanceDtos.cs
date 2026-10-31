using System.Text.Json.Serialization;

namespace HotelOps.Api.DTOs.HotelStructure;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateBranchRequest(string Code, string Name, string Address, string? Phone, string? Email);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateBranchRequest(string Name, string Address, string? Phone, string? Email, string Reason);
public sealed record EditableBranchDto(long Id, string Code, string Name, string? Address, string? Phone,
    string? Email, string? Timezone, bool IsActive);
