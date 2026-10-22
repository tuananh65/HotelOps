namespace HotelOps.Api.DTOs.Maintenance;

public sealed record MaintenanceMaterialUsageWrite(long StockDocumentLineId, long UomId, decimal UsedQty);
public sealed record MaintenanceMaterialUsageDto(long UsageId, long MaintenanceWorkId, long MaintenanceWorkLogId,
    long StockDocumentLineId, long DocumentId, string DocumentNo, long IncidentId, long RequestId,
    long RequestLineId, long FulfillmentId, long FulfillmentLineId, long WarehouseId, long ItemId,
    long BaseUomId, long UomId, decimal UsedQty, decimal ConversionFactor, decimal UsedBaseQty);
public sealed record MaintenanceUsageUom(long UomId, decimal ConversionFactor);
public sealed record MaintenanceMaterialAvailabilityLine(long StockDocumentLineId, int LineNo, long DocumentId,
    string DocumentNo, DateTime PostedAt, long IncidentId, long RequestId, long RequestLineId, long FulfillmentId,
    long FulfillmentLineId, long WarehouseId, long ItemId, long BaseUomId,
    IReadOnlyList<MaintenanceUsageUom> AllowedUoms, decimal IssuedBaseQty, decimal ConsumedBaseQty,
    decimal ReturnedBaseQty, decimal RemainingBaseQty);
public sealed record MaintenanceMaterialAvailability(int Page, int PageSize, int Total, IReadOnlyList<MaintenanceMaterialAvailabilityLine> Items);
