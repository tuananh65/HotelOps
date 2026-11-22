using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class AssetEvent
{
    public long AssetEventId { get; set; }

    public long BranchId { get; set; }

    public long AssetId { get; set; }

    public string EventType { get; set; } = null!;

    public long? SourceWorkId { get; set; }

    public long? FromBranchId { get; set; }

    public long? FromLocationId { get; set; }

    public long? ToLocationId { get; set; }

    public long? FromRoomId { get; set; }

    public long? ToRoomId { get; set; }

    public long? FromUserId { get; set; }

    public long? ToUserId { get; set; }

    public string? OldCondition { get; set; }

    public string? NewCondition { get; set; }

    public string? HandoverStatus { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public long ActorUserId { get; set; }

    public Guid OperationKey { get; set; }

    public string? Note { get; set; }

    public DateTime OccurredAt { get; set; }











}
