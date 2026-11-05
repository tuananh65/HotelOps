namespace HotelOps.Api.DTOs.Housekeeping;

public sealed record EvidenceUpload(string Name, string MimeType, string Base64);
public sealed record InspectionCriterionInput(long ItemId, string Outcome, string? Note = null, IReadOnlyList<EvidenceUpload>? Photos = null);
public sealed record SaveInspectionRequest(IReadOnlyList<InspectionCriterionInput> Results, string Reason);
public sealed record InspectionActionRequest(string Reason);
public sealed record RecordInspectionDefectRequest(long ItemId, long SeverityId, string Description, string Location, IReadOnlyList<EvidenceUpload>? Photos = null);
public sealed record AssignCleaningRequest(long AssigneeId, DateTime DueAt, string Reason, bool AdoptUnassignedLegacy = false);
public sealed record AssignInspectionRequest(long InspectorId, DateTime DueAt, string Reason);
public sealed record RequestReworkRequest(long AssigneeId, DateTime DueAt, IReadOnlyList<long> DefectIds, string Reason);
public sealed record InspectionExecutionDto(long Id, long BranchId, long RoomId, long CleaningTaskId, long InspectorId, long VersionId,
    string State, DateTime? DueAt, IReadOnlyList<HotelOps.Api.DTOs.HotelStructure.HistoryCriterionDto> Criteria,
    IReadOnlyList<HotelOps.Api.DTOs.HotelStructure.HistoryDefectDto> Defects, IReadOnlyList<HotelOps.Api.DTOs.HotelStructure.HistoryEvidenceDto> Evidence);
public sealed record HousekeepingWriteDto(long Id, string State, string Notification = "NOT_INTEGRATED");
