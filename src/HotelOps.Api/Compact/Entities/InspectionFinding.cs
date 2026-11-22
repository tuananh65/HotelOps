using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class InspectionFinding
{
    public long FindingId { get; set; }

    public long BranchId { get; set; }

    public long InspectionWorkId { get; set; }

    public long? WorkChecklistItemId { get; set; }

    public long? ReworkWorkId { get; set; }

    public long? SeverityId { get; set; }

    public string Description { get; set; } = null!;

    public string Status { get; set; } = null!;

    public long? AssignedTo { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? ResolutionNote { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public string SeverityKind { get; set; } = null!;







}
