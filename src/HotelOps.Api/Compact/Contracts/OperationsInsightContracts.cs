namespace HotelOps.Api.Compact.Contracts;

// Links identify existing records; dashboard queues never introduce workflow state.
public sealed record ActionCenterItem(string EntityKind,long Id,string Kind,string Title,string Status,
    DateTime OccurredAt,DateTime? DueAt,bool CanAct);
public sealed record ActionCenterGroup(string Code,int Count,IReadOnlyList<ActionCenterItem> Items);
public sealed record OperationsActionCenter(DateTime UpdatedAt,IReadOnlyList<ActionCenterGroup> Groups,
    int ActiveAssets,int RetiredAssets,int LowStockItems,int TransfersInTransit);
