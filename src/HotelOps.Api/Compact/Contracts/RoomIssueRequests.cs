using System.ComponentModel.DataAnnotations;
namespace HotelOps.Api.Compact.Contracts;

public sealed record RoomIssueCreateRequest(long BranchId,long RoomId,long TypeId,long? AssetId,long? ItemId,
    [Required] string RoomRowVersion,Guid OperationKey,[StringLength(2000)] string? Note,
    decimal Quantity=1,string Impact="READINESS",long? CleaningWorkId=null);
public sealed record RoomIssueTypeRequest(string Kind,[Required,StringLength(120,MinimumLength=1)] string Name,
    bool IsActive,string? RowVersion=null,[StringLength(2000)] string? Description=null,
    RoomIssueImpactOption[]? Impacts=null,bool RequireNote=false);
public sealed record RoomIssueImpactOption(string Code,[Required,StringLength(120,MinimumLength=1)] string Name,bool IsActive);
public sealed record RoomIssueActionRequest([Required] string RowVersion,Guid OperationKey,
    [StringLength(2000)] string? Note=null,long? UserId=null,long? DocumentId=null,string? RoomRowVersion=null,
    long? DepartmentId=null,long? ItemId=null,long? UnitId=null,decimal? Quantity=null,long? WarehouseId=null);
