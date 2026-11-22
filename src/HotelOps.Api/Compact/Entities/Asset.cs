using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class Asset
{
    public long AssetId { get; set; }

    public long BranchId { get; set; }

    public string AssetCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public long? AssetTypeId { get; set; }

    public long? LocationId { get; set; }

    public long? RoomId { get; set; }

    public long? DepartmentId { get; set; }

    public long? CustodianUserId { get; set; }

    public string? SerialNumber { get; set; }

    public string ConditionCode { get; set; } = null!;

    public DateOnly? PurchasedOn { get; set; }

    public DateOnly? WarrantyUntil { get; set; }

    public DateTime? RetiredAt { get; set; }

    public string? RetirementReason { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public string AssetTypeKind { get; set; } = null!;










}
