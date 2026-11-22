using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class WorkItem
{
    public long WorkId { get; set; }

    public long BranchId { get; set; }

    public string WorkNo { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public long? DepartmentId { get; set; }

    public long? RequesterDepartmentId { get; set; }

    public long? RoomId { get; set; }

    public long? LocationId { get; set; }

    public long? AssetId { get; set; }

    public long? CategoryId { get; set; }

    public long? PriorityId { get; set; }

    public long? SeverityId { get; set; }

    public long? WorkTemplateId { get; set; }

    public long? ChecklistTemplateId { get; set; }

    public long? ParentWorkId { get; set; }

    public int? TurnoverNo { get; set; }

    public DateTime? OccurrenceAt { get; set; }

    public decimal ProgressPercent { get; set; }

    public DateTime? PlannedStartAt { get; set; }

    public DateTime? DueAt { get; set; }

    public DateTime? ResponseDeadlineAt { get; set; }

    public DateTime? ActualStartAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public long? VerifiedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public string? Result { get; set; }

    public string? CompletionNote { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public string CategoryKind { get; set; } = null!;

    public string PriorityKind { get; set; } = null!;

    public string SeverityKind { get; set; } = null!;
































}
