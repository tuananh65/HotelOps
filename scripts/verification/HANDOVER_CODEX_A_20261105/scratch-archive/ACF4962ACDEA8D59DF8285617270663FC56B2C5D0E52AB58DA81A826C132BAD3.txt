using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class Branch
{
    public long BranchId { get; set; }

    public string BranchCode { get; set; } = null!;

    public string BranchName { get; set; } = null!;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Timezone { get; set; }

    public bool IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Area> Areas { get; set; } = new List<Area>();

    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    public virtual ICollection<Floor> Floors { get; set; } = new List<Floor>();

    public virtual ICollection<Incident> Incidents { get; set; } = new List<Incident>();

    public virtual ICollection<InternalRequest> InternalRequests { get; set; } = new List<InternalRequest>();

    public virtual ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();

    public virtual ICollection<OperationalTask> OperationalTasks { get; set; } = new List<OperationalTask>();

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();

    public virtual ICollection<ShiftTemplate> ShiftTemplates { get; set; } = new List<ShiftTemplate>();

    public virtual ICollection<SystemSetting> SystemSettings { get; set; } = new List<SystemSetting>();

    public virtual ICollection<UserBranchAccess> UserBranchAccesses { get; set; } = new List<UserBranchAccess>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();

    public virtual ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();

    public virtual ICollection<WorkShift> WorkShifts { get; set; } = new List<WorkShift>();
}
