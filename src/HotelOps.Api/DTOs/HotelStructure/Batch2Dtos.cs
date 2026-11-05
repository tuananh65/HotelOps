using System.Text.Json.Serialization;
namespace HotelOps.Api.DTOs.HotelStructure;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeactivateRoomTypeRequest(bool Confirmation, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateRoomInformationRequest(long FloorId, long RoomTypeId, string RoomNumber,
    string Reason, long? AreaId = null, string? Notes = null);
public sealed record ActiveRoomReferenceDto(long Id, string RoomNumber);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateDepartmentRequest(string Code, string Name, string? Description = null, long? ManagerUserId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateDepartmentRequest(string Name, string? Description = null, long? ManagerUserId = null);
public sealed record DepartmentInformationDto(long Id, long BranchId, string Code, string Name,
    string? Description, long? ManagerUserId, bool IsActive);
