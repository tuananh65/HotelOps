using System.ComponentModel.DataAnnotations;
namespace HotelOps.Api.Compact.Contracts;

public sealed record RoomSupplyAssessmentLine(long ReservationId,long ItemId,string Name,decimal RequiredQuantity,
    decimal? UsableQuantity,decimal MissingQuantity,DateTime? AssessedAt,string RowVersion,
    decimal? CurrentQuantity=null,decimal? UnusableQuantity=null,decimal? ExpiredQuantity=null,decimal? ReplenishedQuantity=null,string? Note=null);
public sealed record AssessRoomSupplyLine([Range(1,long.MaxValue)]long ReservationId,
    [Range(typeof(decimal),"0","999999999")]decimal UsableQuantity,[Required]string RowVersion,
    decimal? CurrentQuantity=null,decimal? UnusableQuantity=null,decimal? ExpiredQuantity=null,decimal? ReplenishedQuantity=null,[StringLength(2000)]string? Note=null);
public sealed record AssessRoomSuppliesRequest(Guid OperationKey,[Required,MinLength(1),MaxLength(100)]AssessRoomSupplyLine[] Lines);
