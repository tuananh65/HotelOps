using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class ChecklistItem
{
    public long ChecklistItemId { get; set; }

    public long ChecklistTemplateId { get; set; }

    public int LineNo { get; set; }

    public string Description { get; set; } = null!;

    public bool IsRequired { get; set; }

    public decimal Weight { get; set; }


}
