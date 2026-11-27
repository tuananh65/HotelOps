namespace HotelOps.Api.Compact.Contracts;

public sealed record RoomSupplyAssessmentLine(long ItemId,string ItemName,decimal StandardQuantity,decimal? UsableQuantity,
    decimal? RequiredQuantity,decimal AvailableQuantity,decimal? ShortageQuantity,long UnitId,string UnitCode,bool? IntegerOnly);
public sealed record RoomSupplyAssessmentDto(long WorkId,long RoomId,bool Confirmed,bool ReadOnly,long? ExecutionId,
    string RowVersion,long? AllocationId,IReadOnlyList<RoomSupplyAssessmentLine> Lines,bool Historical=false);
