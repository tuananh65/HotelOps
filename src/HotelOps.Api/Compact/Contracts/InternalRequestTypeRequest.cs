using System.ComponentModel.DataAnnotations;
namespace HotelOps.Api.Compact.Contracts;

public sealed record InternalRequestTypeRequest(
    [Required,StringLength(120,MinimumLength=1)] string Name,
    [StringLength(2000)] string? Description,
    long? DefaultDepartmentId,bool IsActive,string? RowVersion=null,
    long[]? SenderRoleIds=null,long[]? RecipientDepartmentIds=null,long[]? RecipientUserIds=null);
