using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class AssetHandover
{
    public long HandoverId { get; set; }

    public string HandoverNo { get; set; } = null!;

    public long AssetId { get; set; }

    public long? FromUserId { get; set; }

    public long? ToUserId { get; set; }

    public long? FromDepartmentId { get; set; }

    public long? ToDepartmentId { get; set; }

    public string? ConditionNote { get; set; }

    public string HandoverStatus { get; set; } = null!;

    public DateTime? HandedOverAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Department? FromDepartment { get; set; }

    public virtual User? FromUser { get; set; }

    public virtual Department? ToDepartment { get; set; }

    public virtual User? ToUser { get; set; }
}
