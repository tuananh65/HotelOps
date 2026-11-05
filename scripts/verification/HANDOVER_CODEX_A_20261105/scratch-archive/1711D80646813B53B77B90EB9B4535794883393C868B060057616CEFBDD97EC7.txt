using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class SystemSetting
{
    public long SettingId { get; set; }

    public long? BranchId { get; set; }

    public string SettingGroup { get; set; } = null!;

    public string SettingKey { get; set; } = null!;

    public string? SettingValue { get; set; }

    public string DataType { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsSensitive { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual User? UpdatedByNavigation { get; set; }
}
