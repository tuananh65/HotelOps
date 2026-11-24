namespace HotelOps.Api.Compact.Contracts;

public sealed record SaveSupplyConfigurationRequest(long BranchId,long? ItemId,long? AssetId,string SupplyGroup,long? WarehouseId,long? UnitId,decimal InitialQuantity,decimal MinimumStock,decimal PerGuestQuantity,bool IsActive,string? RowVersion);
public sealed record SupplyConfigurationDto(long ConfigurationId,long BranchId,long? ItemId,long? AssetId,string Code,string Name,string SupplyGroup,long? WarehouseId,long? UnitId,string? Unit,decimal InitialQuantity,decimal MinimumStock,decimal PerGuestQuantity,bool IsActive,int Revision,long UpdatedBy,DateTime UpdatedAt,string RowVersion);
public sealed record StaffSupplyBalanceDto(long BalanceId,long ItemId,string Code,string Name,long UnitId,long WarehouseId,decimal InitialTarget,decimal InitialIssued,decimal AdditionalIssued,decimal Reserved,decimal Used,decimal Returned,decimal Remaining,decimal Available,string RowVersion);
public sealed record StaffSupplyAllocationDto(long AllocationId,long BranchId,long UserId,long AssignmentId,string Status,string? ShortageReason,DateTime CreatedAt,DateTime? ClosedAt,string RowVersion,IReadOnlyList<StaffSupplyBalanceDto> Items);
public sealed record SupplyLedgerDto(long LedgerId,long ItemId,string Kind,decimal Quantity,long? WorkId,long? ReservationId,long? DocumentLineId,long ActorUserId,DateTime OccurredAt);
public sealed record SupplyIssueQuantity(long ItemId,decimal Quantity);
