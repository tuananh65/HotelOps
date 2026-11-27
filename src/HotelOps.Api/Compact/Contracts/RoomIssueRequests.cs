using System.ComponentModel.DataAnnotations;
namespace HotelOps.Api.Compact.Contracts;

public sealed record RoomIssueCreateRequest(long BranchId,long RoomId,long TypeId,long? AssetId,long? ItemId,
    [Required] string RoomRowVersion,Guid OperationKey,[StringLength(2000)] string? Note,
    decimal Quantity=1,string Impact="READINESS",long? CleaningWorkId=null,long? DetectedByUserId=null);
public sealed record RoomIssueTypeRequest(string Kind,[Required,StringLength(120,MinimumLength=1)] string Name,
    bool IsActive,string? RowVersion=null,[StringLength(2000)] string? Description=null,
    RoomIssueImpactOption[]? Impacts=null,bool RequireNote=false,long? ReferenceId=null,bool CreateNew=false);
public sealed record RoomIssueImpactOption(string Code,[Required,StringLength(120,MinimumLength=1)] string Name,bool IsActive);
public sealed record LegacyRoomIssueRecoveryRequest([Required] string RowVersion,[Required] string RoomRowVersion,
    Guid OperationKey,long AssetId,[Required,StringLength(2000)] string Note);
public sealed record RoomIssueActionRequest([Required] string RowVersion,Guid OperationKey,
    [StringLength(2000)] string? Note=null,long? UserId=null,long? DocumentId=null,string? RoomRowVersion=null,
    long? DepartmentId=null,long? ItemId=null,long? UnitId=null,decimal? Quantity=null,long? WarehouseId=null,bool ExternalRepair=false,long? ChecklistTemplateId=null,decimal ReturnedQuantity=0,long? ReplacementAssetId=null);

public sealed record RoomIssueBatchLine(long? ItemId,long? AssetId,decimal Quantity=1);
public sealed record RoomIssueBatchRequest(long BranchId,long RoomId,long TypeId,
    [Required] string RoomRowVersion,Guid OperationKey,[Required] RoomIssueBatchLine[] Lines,
    [StringLength(2000)] string? Note=null,string Impact="READINESS",long? CleaningWorkId=null,long? DetectedByUserId=null);
