using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class ChecklistTemplate
{
    public long ChecklistTemplateId { get; set; }

    public long BranchId { get; set; }

    public string Code { get; set; } = null!;

    public int VersionNo { get; set; }

    public string Name { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public DateTime? PublishedAt { get; set; }

    public long CreatedBy { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;





}
