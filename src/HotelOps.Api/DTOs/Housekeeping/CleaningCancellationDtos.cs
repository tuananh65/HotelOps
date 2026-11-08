namespace HotelOps.Api.DTOs.Housekeeping;

public sealed record CancelCleaningRequest(bool Confirmed, string Reason);
public sealed record CleaningCancellationOptions(long Id, long BranchId, string SourceETag, string Capability, string? Blocker);
public sealed record CleaningCancellationResult(long Id, long BranchId, long RoomId, long? AssignmentId,
    long? AssigneeId, Guid OperationId, Guid? PublicationId, long ReadinessEventId, DateTime OccurredAtUtc,
    string SourceETag, string State = "CANCELLED", string Readiness = "PENDING_CLEANING",
    string Contract = "PROVISIONAL_NOT_ACCEPTED");
