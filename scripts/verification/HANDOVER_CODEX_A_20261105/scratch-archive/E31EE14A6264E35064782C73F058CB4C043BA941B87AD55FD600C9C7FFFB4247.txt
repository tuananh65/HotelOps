using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class Asset
{
    public long AssetId { get; set; }

    public string AssetCode { get; set; } = null!;

    public string AssetName { get; set; } = null!;

    public long AssetTypeId { get; set; }

    public long BranchId { get; set; }

    public string? SerialNumber { get; set; }

    public string? Brand { get; set; }

    public string? Model { get; set; }

    public DateOnly? PurchaseDate { get; set; }

    public DateOnly? WarrantyExpiryDate { get; set; }

    public decimal? AcquisitionCost { get; set; }

    public string ConditionStatus { get; set; } = null!;

    public string LifecycleStatus { get; set; } = null!;

    public long? CurrentRoomId { get; set; }

    public long? CurrentAreaId { get; set; }

    public long? CurrentDepartmentId { get; set; }

    public long? CustodianUserId { get; set; }

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AssetConditionHistory> AssetConditionHistories { get; set; } = new List<AssetConditionHistory>();

    public virtual ICollection<AssetHandover> AssetHandovers { get; set; } = new List<AssetHandover>();

    public virtual ICollection<AssetLocationHistory> AssetLocationHistories { get; set; } = new List<AssetLocationHistory>();

    public virtual AssetType AssetType { get; set; } = null!;

    public virtual Branch Branch { get; set; } = null!;

    public virtual Area? CurrentArea { get; set; }

    public virtual Department? CurrentDepartment { get; set; }

    public virtual Room? CurrentRoom { get; set; }

    public virtual User? CustodianUser { get; set; }

    public virtual ICollection<Incident> Incidents { get; set; } = new List<Incident>();

    public virtual ICollection<PreventiveMaintenanceSchedule> PreventiveMaintenanceSchedules { get; set; } = new List<PreventiveMaintenanceSchedule>();
}
