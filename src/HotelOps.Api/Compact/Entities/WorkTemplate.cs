using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class WorkTemplate
{
    public long WorkTemplateId { get; set; }

    public long BranchId { get; set; }

    public string Code { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Instruction { get; set; }

    public long? DepartmentId { get; set; }

    public long? AssetId { get; set; }

    public long? RoomId { get; set; }

    public long? LocationId { get; set; }

    public long? RoomTypeId { get; set; }

    public long? ChecklistTemplateId { get; set; }

    public string? Frequency { get; set; }

    public int? FrequencyInterval { get; set; }

    public string? ScheduleJson { get; set; }

    public DateTime? NextDueAt { get; set; }

    public long CreatedBy { get; set; }

    public bool IsActive { get; set; }

    public byte[] RowVersion { get; set; } = null!;









}
