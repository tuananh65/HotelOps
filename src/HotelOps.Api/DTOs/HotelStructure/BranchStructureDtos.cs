using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.HotelStructure;

public sealed class BranchStructureQuery
{
    [StringLength(10)] public string NodeType { get; init; } = "branch";
    [Range(1, long.MaxValue)] public long? NodeId { get; init; }
    [StringLength(11)] public string? Collection { get; init; }
    public bool? IsActive { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;

    public (string Node, string? Collection) Validate()
    {
        Validator.ValidateObject(this, new ValidationContext(this), true);
        if ((long)(Page - 1) * PageSize > int.MaxValue) throw new ValidationException("Page offset exceeds the supported Int32 range.");
        var node = NodeType?.Trim().ToLowerInvariant();
        if (node is not ("branch" or "floor" or "area" or "room" or "department")
            || (node == "branch" ? NodeId != null : NodeId == null))
            throw new ValidationException("Select branch without nodeId, or floor/area/room/department with a positive nodeId.");
        var collection = string.IsNullOrWhiteSpace(Collection) ? node switch
        { "branch" => "floors", "floor" => "areas", "area" => "rooms", _ => null }
            : Collection.Trim().ToLowerInvariant();
        if (collection != null && !CollectionsFor(node).Contains(collection))
            throw new ValidationException("Collection is not a child collection of the selected node.");
        return (node, collection);
    }

    public static string[] CollectionsFor(string node) => node switch
    { "branch" => ["floors", "areas", "departments"], "floor" => ["areas", "rooms"], "area" => ["rooms"], _ => [] };
}

public sealed record StructureBranchDto(long Id, string Code, string Name, bool IsActive);
public sealed record StructureFloorDto(long Id, long BranchId, string Code, string Name, int? FloorNumber, string? Description, bool IsActive);
public sealed record StructureAreaDto(long Id, long BranchId, long? FloorId, string Code, string Name, string? Description, bool IsActive);
public sealed record StructureRoomDto(long Id, long BranchId, long FloorId, long? AreaId, string RoomNumber, bool IsActive);
public sealed record StructureDepartmentDto(long Id, long BranchId, string Code, string Name, bool IsActive);
public sealed record StructurePageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);
public sealed record BranchStructureDto(StructureBranchDto Branch, string Visibility, string NodeType, string? Collection,
    IReadOnlyList<string> AvailableCollections, bool? IsActive,
    StructureFloorDto? SelectedFloor = null, StructureAreaDto? SelectedArea = null,
    StructureRoomDto? SelectedRoom = null, StructureDepartmentDto? SelectedDepartment = null,
    StructurePageDto<StructureFloorDto>? Floors = null, StructurePageDto<StructureAreaDto>? Areas = null,
    StructurePageDto<StructureRoomDto>? Rooms = null, StructurePageDto<StructureDepartmentDto>? Departments = null);
