using System.Text.Json.Serialization;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.DTOs.Housekeeping;

namespace HotelOps.Api.DTOs.OperationalTasks;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitOperationalTaskRequest(IReadOnlyList<ProgressItemInput> Items,
    string? ResultNote, string? HandoverNote, IReadOnlyList<EvidenceUpload>? Photos = null);
public sealed record TaskSubmissionDto(long Id, long TaskId, long BranchId, long SubmittedBy,
    DateTimeOffset SubmittedAt, string? ResultNote, string? HandoverNote,
    IReadOnlyList<ProgressItemDto> Criteria, IReadOnlyList<HistoryEvidenceDto> Evidence);
public sealed record TaskSubmissionFailure(string MessageCode, IReadOnlyList<string>? MissingCriteria = null);
