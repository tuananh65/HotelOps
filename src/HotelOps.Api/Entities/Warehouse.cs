using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class Warehouse
{
    public long WarehouseId { get; set; }

    public long BranchId { get; set; }

    public long? DepartmentId { get; set; }

    public string WarehouseCode { get; set; } = null!;

    public string WarehouseName { get; set; } = null!;

    public string? LocationDescription { get; set; }

    public long? ManagerUserId { get; set; }

    public bool IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Branch Branch { get; set; } = null!;

    public virtual Department? Department { get; set; }

    public virtual User? ManagerUser { get; set; }

    public virtual ICollection<MaterialFulfillment> MaterialFulfillments { get; set; } = new List<MaterialFulfillment>();

    public virtual ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();

    public virtual ICollection<StockDocument> StockDocumentDestinationWarehouses { get; set; } = new List<StockDocument>();

    public virtual ICollection<StockDocument> StockDocumentSourceWarehouses { get; set; } = new List<StockDocument>();

    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    public virtual ICollection<StockThreshold> StockThresholds { get; set; } = new List<StockThreshold>();

    public virtual ICollection<Stocktake> Stocktakes { get; set; } = new List<Stocktake>();
}
