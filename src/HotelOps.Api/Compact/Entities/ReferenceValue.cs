using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class ReferenceValue
{
    public long ReferenceId { get; set; }

    public string Kind { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public long? ParentId { get; set; }

    public long? BaseUnitId { get; set; }

    public decimal? ConversionFactor { get; set; }

    public string? SettingsJson { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public byte[] RowVersion { get; set; } = null!;












}
