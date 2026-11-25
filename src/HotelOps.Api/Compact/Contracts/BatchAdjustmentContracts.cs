using System.ComponentModel.DataAnnotations;
namespace HotelOps.Api.Compact.Contracts;
public sealed record BatchAdjustmentLine(long LineId,long BatchId,decimal Quantity);
public sealed record BatchAdjustmentRequest([Required]string RowVersion,Guid OperationKey,
    [Required,MinLength(1),MaxLength(200)]BatchAdjustmentLine[] Lines,[Required,StringLength(2000)]string Reason);
