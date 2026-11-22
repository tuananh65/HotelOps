using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class WorkChecklistItem
{
    public long WorkChecklistItemId { get; set; }

    public long BranchId { get; set; }

    public long WorkId { get; set; }

    public long ChecklistTemplateId { get; set; }

    public long ChecklistItemId { get; set; }

    public string DescriptionSnapshot { get; set; } = null!;

    public bool RequiredSnapshot { get; set; }

    public decimal WeightSnapshot { get; set; }

    public string? Outcome { get; set; }

    public string? Note { get; set; }

    public long? CheckedBy { get; set; }

    public DateTime? CheckedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;






}
