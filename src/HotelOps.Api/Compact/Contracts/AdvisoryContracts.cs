using System.ComponentModel.DataAnnotations;
namespace HotelOps.Api.Compact.Contracts;

public sealed record AdvisoryRequest([Range(1,long.MaxValue)]long BranchId,
    [Required,RegularExpression("^(DEMAND|TRANSFER)$")]string Kind,
    [Range(1,90)]int Days=14);
public sealed record DemandEstimate(long BranchId,long WarehouseId,long ItemId,string Item,string Unit,
    int Days,decimal UsedInLast30Days,int ObservedDays,decimal Available,decimal Minimum,
    decimal ExpectedDemand,decimal ExpiringBeforeHorizon,decimal SuggestedReplenishment,string Limitation,
    decimal IssuedInLast30Days,decimal ReturnedInLast30Days,decimal RecentDailyUse,decimal PreviousDailyUse,
    int RoomsNeedingCleaning,int GuestCapacity,decimal RoomDemandUpperBound);
public sealed record TransferSuggestion(long SourceBranchId,long SourceWarehouseId,long DestinationBranchId,
    long DestinationWarehouseId,long ItemId,string Item,decimal Quantity,string Reason);
public sealed record AdvisoryResult(string Method,string ExternalStatus,string? Provider,string? Model,
    DateTime GeneratedAt,object Evidence,string Explanation,bool RequiresHumanDecision=true);
