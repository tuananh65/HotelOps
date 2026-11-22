using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class Department
{
    public long DepartmentId { get; set; }

    public long BranchId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public long? ManagerUserId { get; set; }

    public bool IsActive { get; set; }

    public byte[] RowVersion { get; set; } = null!;










}
