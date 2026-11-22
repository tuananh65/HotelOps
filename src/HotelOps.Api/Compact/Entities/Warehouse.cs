using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class Warehouse
{
    public long WarehouseId { get; set; }

    public long BranchId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public long? LocationId { get; set; }

    public long? DepartmentId { get; set; }

    public bool IsActive { get; set; }

    public byte[] RowVersion { get; set; } = null!;







}
