using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class Attachment
{
    public long AttachmentId { get; set; }

    public long BranchId { get; set; }

    public long? WorkId { get; set; }

    public long? FindingId { get; set; }

    public long? DocumentId { get; set; }

    public long? AssetId { get; set; }

    public string StorageKey { get; set; } = null!;

    public string OriginalName { get; set; } = null!;

    public string MimeType { get; set; } = null!;

    public long ByteLength { get; set; }

    public byte[] Sha256 { get; set; } = null!;

    public long UploadedBy { get; set; }

    public DateTime? InvalidatedAt { get; set; }

    public string? InvalidationReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;






}
