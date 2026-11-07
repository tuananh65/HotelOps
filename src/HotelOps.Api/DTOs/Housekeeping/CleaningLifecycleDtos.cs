using System.Text.Json.Serialization;
namespace HotelOps.Api.DTOs.Housekeeping;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StartCleaningRequest(bool ResolveBlocker = false, string? Note = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReassignCleaningRequest(long AssigneeId, DateTime DueAt, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportCleaningBlockerRequest(long ReasonId, string Note);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CompleteCleaningRequest(string Note);
public sealed record CleaningLifecycleFailure(string Code);
public sealed record CleaningExecutionDto(long Id, long BranchId, string TaskNumber, string? State,
    long? AssignmentId, long? AssigneeId, DateTime? ActualStartAtUtc, string SourceETag,
    string StartCapability, string ReassignCapability,
    string ReportBlockerCapability = "UNAVAILABLE", string CompleteCapability = "UNAVAILABLE",
    string ResumeCapability = "UNAVAILABLE");
public sealed record StartedCleaningDto(long Id, long BranchId, long AssignmentId, string State,
    long IntervalId, DateTime ActualStartAtUtc, string Readiness, bool HasEffectiveBlock);
public sealed record ReassignedCleaningDto(long Id, long BranchId, long PreviousAssignmentId, long AssignmentId,
    long PreviousAssigneeId, long AssigneeId, Guid OperationId, string State, string Notification,
    string SourceETag, string Contract = "PROVISIONAL_NOT_ACCEPTED");
