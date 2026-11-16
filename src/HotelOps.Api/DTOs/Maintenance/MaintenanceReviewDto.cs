namespace HotelOps.Api.DTOs.Maintenance;

public sealed record MaintenanceReviewDto(long SubmissionAuditId, DateTime SubmittedAtUtc,
    string Diagnosis, string ActionTaken, IReadOnlyList<long> ResultAttachmentIds);
