using System.ComponentModel.DataAnnotations;
namespace HotelOps.Api.Compact.Contracts;
public sealed record ItemLifecycleRequest(long BranchId,long ItemId,string? RowVersion,
    [Required]string Classification,[Required]string ExpiryPolicy,[Required,StringLength(2000)]string Reason);
public sealed record BatchMetadataRequest([Required]string RowVersion,[Required,StringLength(100)]string LotNumber,
    DateTime ReceivedAt,DateOnly? ExpiresOn,[Required]string Status,[Required,StringLength(2000)]string Reason);
