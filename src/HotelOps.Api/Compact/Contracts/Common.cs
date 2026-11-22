using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace HotelOps.Api.Compact.Contracts;

public sealed record PageDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record ResourceDto(string Type, IReadOnlyDictionary<string, object?> Fields);
public record VersionRequest([Required] string RowVersion);
public record ActionRequest([Required] string RowVersion, Guid OperationKey, string? Reason = null,
    long? UserId = null, long? RelatedId = null, decimal? Progress = null, DateTime? DueAt = null,
    IReadOnlyList<ApprovedLineRequest>? ApprovedLines = null, long? PriorityId = null, long? SeverityId = null, long? CategoryId = null);
public sealed record ApprovedLineRequest(long LineId, decimal Quantity);
public sealed record CreateWorkRequest(long BranchId, [Required] string Kind,
    [Required, StringLength(250,MinimumLength=1)] string Title, string? Description,
    long? DepartmentId, long? RoomId, long? LocationId, long? AssetId, long? ParentWorkId,
    long? ChecklistTemplateId, long? PriorityId, long? SeverityId, long? CategoryId, DateTime? DueAt, long? RequesterDepartmentId = null);
public sealed record ChecklistResultRequest(string RowVersion, [Required] string Outcome, string? Note);
public sealed record FindingRequest(string RowVersion, [Required] string Description,
    long? WorkChecklistItemId, long? SeverityId, long? ReworkWorkId);
public sealed record LinkRequest(string RowVersion, long TargetWorkId, [Required] string Relation);
public sealed record RoomActionRequest(string RowVersion, Guid OperationKey, string? Reason,
    long? TargetRoomId = null, string? TargetRowVersion = null, long? SourceWorkId = null);
public sealed record CatalogWriteRequest(JsonElement Fields, string? RowVersion = null);
public sealed record LoginRequest([Required,StringLength(100)] string Username,
    [Required,StringLength(1024)] string Password);
public sealed record PasswordRequest([Required] string CurrentPassword,
    [Required,StringLength(128,MinimumLength=12)] string NewPassword);
public sealed record CreateUserRequest([Required,StringLength(30)] string EmployeeCode,
    [Required,StringLength(100)] string Username,
    [Required,StringLength(200)] string FullName,
    [Required,StringLength(128,MinimumLength=12)] string Password, string? Email);
public sealed record MembershipRequest(long BranchId, string Kind, long? DepartmentId, bool IsPrimary = false);
public sealed record UserRoleRequest(long RoleId, long? BranchId, DateTime? ExpiresAt);
public sealed record GrantRequest(long PermissionId, string DataScope);
public sealed record CreateDocumentRequest(long BranchId, [Required] string Kind,
    long? DepartmentId, long? SourceWarehouseId, long? DestinationWarehouseId, long? SourceDocumentId,
    long? SourceWorkId, string? SupplierName, string? Reason, DateOnly? ExpectedOn, Guid? OperationKey = null);
public sealed record DocumentLineRequest(string RowVersion, long ItemId, long UnitId,
    decimal Quantity, decimal? UnitCost, long? SourceLineId, decimal? CountedQty = null);
public sealed record DeriveDocumentRequest(string RowVersion, Guid OperationKey, string Kind,
    long? WarehouseId, string? Reason, IReadOnlyList<DerivedLineRequest> Lines);
public sealed record DerivedLineRequest(long LineId, decimal Quantity);
public sealed record MaintenanceFromIncidentRequest(string RowVersion,Guid OperationKey,[Required,StringLength(250)]string Title,DateTime? DueAt,string? Reason);
public sealed record AssetActionRequest(string RowVersion, Guid OperationKey,
    [Required] string Reason, long? RoomId, long? LocationId, long? UserId, string? Condition);
public sealed record TemplateRequest(long BranchId, [Required,StringLength(60)] string Code,
    [Required] string Kind,[Required,StringLength(200)] string Title, long? DepartmentId,
    long? RoomId,long? AssetId,long? ChecklistTemplateId,string? Frequency,int? FrequencyInterval,DateTime? NextDueAt);
public sealed record ChecklistCreateRequest(long BranchId,string Code,string Name,string Kind,
    IReadOnlyList<ChecklistDefinitionItem> Items);
public sealed record ChecklistDefinitionItem(string Description,bool IsRequired,decimal Weight);
public sealed record UpdateTemplateRequest(string RowVersion,TemplateRequest Plan);
