using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class InventoryDocument
{
    public long DocumentId { get; set; }

    public long BranchId { get; set; }

    public string DocumentNo { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public string Status { get; set; } = null!;

    public long? DepartmentId { get; set; }

    public long? SourceWarehouseId { get; set; }

    public long? DestinationWarehouseId { get; set; }

    public long? DestinationBranchId { get; set; }

    public long? SourceDocumentId { get; set; }

    public long? SourceWorkId { get; set; }

    public long? ReceivingRoomId { get; set; }

    public string? SupplierName { get; set; }

    public DateOnly? ExpectedOn { get; set; }

    public string? Reason { get; set; }

    public long CreatedBy { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public long? PostedBy { get; set; }

    public DateTime? PostedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;
















}
