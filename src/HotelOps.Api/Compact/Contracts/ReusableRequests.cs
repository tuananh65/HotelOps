using System.ComponentModel.DataAnnotations;
namespace HotelOps.Api.Compact.Contracts;
public sealed record ReusableMoveRequest([Required]string RowVersion,Guid OperationKey,decimal Quantity,[Required]string Status,
    long? WarehouseId,long? UserId,long? RoomId,[Required,StringLength(2000)]string Reason,long? WorkId=null,bool Replace=false);
public sealed record ReusableWriteOffRequest([Required]string RowVersion,Guid OperationKey,decimal Quantity,[Required,StringLength(2000)]string Reason);
public sealed record DecideWriteOffRequest([Required]string RowVersion,Guid OperationKey,bool Approve,[StringLength(2000)]string? Reason);
