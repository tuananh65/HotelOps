using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class Room
{
    public long RoomId { get; set; }

    public long BranchId { get; set; }

    public long LocationId { get; set; }

    public long RoomTypeId { get; set; }

    public long? DepartmentId { get; set; }

    public string Code { get; set; } = null!;

    public string OperationalStatus { get; set; } = null!;

    public int TurnoverNo { get; set; }

    public long? CurrentCleaningWorkId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;















}
