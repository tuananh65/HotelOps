using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class InventoryDocumentLine
{
    public long LineId { get; set; }

    public long BranchId { get; set; }

    public long DocumentId { get; set; }

    public int LineNo { get; set; }

    public long ItemId { get; set; }

    public long UnitId { get; set; }

    public decimal ConversionFactor { get; set; }

    public decimal? RequestedQty { get; set; }

    public decimal? ApprovedQty { get; set; }

    public decimal ActualQty { get; set; }

    public decimal? BaseQty { get; set; }

    public decimal? CountedQty { get; set; }

    public decimal? ExpectedQty { get; set; }

    public decimal DamagedQty { get; set; }

    public decimal MissingQty { get; set; }

    public decimal? UnitCost { get; set; }

    public long? SourceLineId { get; set; }

    public string? Note { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public string UnitKind { get; set; } = null!;








}
