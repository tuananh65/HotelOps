using System.ComponentModel.DataAnnotations;
namespace HotelOps.Api.Compact.Contracts;

public sealed record ReplacementRequest([Required]string RowVersion,Guid OperationKey,
    [Required,MaxLength(2000)]string Reason,[MaxLength(2000)]string? Plan=null,long? ReplacementAssetId=null);

public sealed record ReplacementSnapshot(long OriginalAssetId,string Reason,string Plan,long? ReplacementAssetId=null,long? RoomId=null,long? LocationId=null);
