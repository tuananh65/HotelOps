using System.Text.Json.Serialization;
namespace HotelOps.Api.DTOs.Housekeeping;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReworkRemediation(long DefectId, string Note, IReadOnlyList<EvidenceUpload>? Photos = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReworkActionRequest(string Note, IReadOnlyList<ReworkRemediation>? Remediations = null);
public sealed record ReworkDefectDto(long Id, string Description, string? Remediation,
    IReadOnlyList<long> InspectionAttachmentIds, IReadOnlyList<long> RemediationAttachmentIds);
public sealed record ReworkExecutionDto(long Id, long BranchId, long CleaningTaskId, long InspectionId, long RoomId,
    long AssigneeId, string State, DateTime? DueAtUtc, string SourceETag, string InspectionETag,
    IReadOnlyList<ReworkDefectDto> Defects, string Capability = "PROVISIONAL_NOT_ACCEPTED",
    string SourceAuthority = "SYNTHETIC_TEST_ONLY");
public sealed record ReworkCommandResult(long Id, long BranchId, long CleaningTaskId, long InspectionId, long AssigneeId,
    string Action, string State, Guid OperationId, string SourceETag, DateTime OccurredAtUtc,
    long? IntervalId, Guid? PublicationId, string Readiness, string Notification,
    string Contract = "PROVISIONAL_NOT_ACCEPTED");
