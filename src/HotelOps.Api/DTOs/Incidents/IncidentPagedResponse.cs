namespace HotelOps.Api.DTOs.Incidents;

public sealed class IncidentPagedResponse
{
    public IReadOnlyList<IncidentListDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
