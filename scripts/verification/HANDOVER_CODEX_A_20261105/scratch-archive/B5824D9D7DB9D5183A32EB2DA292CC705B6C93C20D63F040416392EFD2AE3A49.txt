using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class Department
{
    public long DepartmentId { get; set; }

    public long BranchId { get; set; }

    public string DepartmentCode { get; set; } = null!;

    public string DepartmentName { get; set; } = null!;

    public long? ManagerUserId { get; set; }

    public string? ScopeDescription { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<AssetHandover> AssetHandoverFromDepartments { get; set; } = new List<AssetHandover>();

    public virtual ICollection<AssetHandover> AssetHandoverToDepartments { get; set; } = new List<AssetHandover>();

    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<InternalRequest> InternalRequestReceivingDepartments { get; set; } = new List<InternalRequest>();

    public virtual ICollection<InternalRequest> InternalRequestRequesterDepartments { get; set; } = new List<InternalRequest>();

    public virtual ICollection<InternalRequestType> InternalRequestTypes { get; set; } = new List<InternalRequestType>();

    public virtual ICollection<MaintenanceWork> MaintenanceWorks { get; set; } = new List<MaintenanceWork>();

    public virtual User? ManagerUser { get; set; }

    public virtual ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();

    public virtual ICollection<OperationalTask> OperationalTasks { get; set; } = new List<OperationalTask>();

    public virtual ICollection<PreventiveMaintenanceSchedule> PreventiveMaintenanceSchedules { get; set; } = new List<PreventiveMaintenanceSchedule>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();

    public virtual ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();

    public virtual ICollection<WorkCategory> WorkCategories { get; set; } = new List<WorkCategory>();

    public virtual ICollection<WorkShift> WorkShifts { get; set; } = new List<WorkShift>();
}
