using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class RoomBlock
{
    public long BlockId { get; set; }

    public long BranchId { get; set; }

    public long RoomId { get; set; }

    public long? SourceWorkId { get; set; }

    public string Reason { get; set; } = null!;

    public long BlockedBy { get; set; }

    public DateTime BlockedAt { get; set; }

    public DateTime? ExpectedReleaseAt { get; set; }

    public long? ReleasedBy { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;





}
