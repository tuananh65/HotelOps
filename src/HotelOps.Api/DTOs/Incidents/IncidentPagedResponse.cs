namespace HotelOps.Api.DTOs.Incidents;

public sealed class IncidentPagedResponse
{
    public IReadOnlyList<IncidentListDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public IncidentQueryRequest? AppliedFilters { get; init; }
    public string SortOrder { get; init; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; init; }
    public string? SimilarReviewToken { get; set; }
}
