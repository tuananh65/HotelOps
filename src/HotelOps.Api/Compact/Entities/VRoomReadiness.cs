using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class VRoomReadiness
{
    public long RoomId { get; set; }

    public long BranchId { get; set; }

    public string OperationalStatus { get; set; } = null!;

    public int TurnoverNo { get; set; }

    public bool? IsOccupied { get; set; }

    public string Readiness { get; set; } = null!;
}
