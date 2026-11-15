using System.Text.Json.Serialization;

namespace HotelOps.Api.DTOs.OperationalTasks;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VerifyOperationalTaskRequest(long SubmissionId, string Decision, string Note);
public sealed record TaskVerificationDecisionDto(long SubmissionId, string Decision, string Note,
    long RequestedBy, long SubmittedBy, IReadOnlyList<long> PerformedBy, long VerifiedBy,
    DateTimeOffset DecidedAt, long ActualEffortSeconds);
public sealed record TaskVerificationReviewDto(long TaskId, long BranchId, long SubmissionId,
    string State, long ActualEffortSeconds, bool CanDecide, string? DenialCode,
    IReadOnlyList<TaskVerificationDecisionDto> Decisions);
