using System.Text.Json.Serialization;
namespace HotelOps.Api.DTOs.Housekeeping;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCleaningTaskRequest(long RoomId, long CleaningTypeId, long PriorityId, DateTime DueAt, string? Instruction = null);
public sealed record CleaningReferenceDto(long Id, string Code, string Name);
public sealed record CleaningItemPreview(long Id, string Label, bool IsRequired, int SortOrder);
public sealed record CleaningCreateOptions(long RoomId, string RoomNumber, long BranchId,
    IReadOnlyList<CleaningReferenceDto> CleaningTypes, IReadOnlyList<CleaningReferenceDto> Priorities, int CleaningItemCount, IReadOnlyList<CleaningItemPreview>? CleaningItems = null, long? ExistingTaskId = null);
public sealed record CreatedCleaningTask(long Id, long BranchId, long RoomId, string TaskNumber, string State,
    string Readiness, bool HasEffectiveBlock, bool ItemsInitialized = false);
