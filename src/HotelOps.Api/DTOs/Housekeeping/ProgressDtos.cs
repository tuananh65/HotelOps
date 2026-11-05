using System.Text.Json.Serialization;
using HotelOps.Api.DTOs.HotelStructure;

namespace HotelOps.Api.DTOs.Housekeeping;

public sealed record ProgressDefinition(string Label, bool Required = true);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InitializeProgressRequest(IReadOnlyList<ProgressDefinition>? Items, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProgressItemInput(long Id, bool? Done = null, string? Outcome = null, string? Note = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveProgressRequest(int Percent, IReadOnlyList<ProgressItemInput> Items, string? Note,
    IReadOnlyList<EvidenceUpload>? Photos = null);
public sealed record ProgressItemDto(long Id, string Label, bool Required, int Order, bool? Done, string? Outcome, string? Note);
public sealed record WorkProgressDto(long Id, long BranchId, string Kind, string State, int Percent,
    IReadOnlyList<ProgressItemDto> Items, IReadOnlyList<HistoryProgressDto> Timeline, IReadOnlyList<HistoryEvidenceDto> Evidence);
