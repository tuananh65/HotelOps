using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class Attachment
{
    public long AttachmentId { get; set; }

    public string EntityType { get; set; } = null!;

    public long EntityId { get; set; }

    public string FileName { get; set; } = null!;

    public string? OriginalFileName { get; set; }

    public string? MimeType { get; set; }

    public long? FileSizeBytes { get; set; }

    public string StorageKey { get; set; } = null!;

    public string? FileUrl { get; set; }

    public string? Description { get; set; }

    public long UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual User UploadedByNavigation { get; set; } = null!;
}
