namespace HotelOps.Api.DTOs.Incidents;

public sealed record IncidentOptionDto(long Id, string Name);

public sealed class IncidentOptionsDto
{
    public IReadOnlyList<IncidentOptionDto> Branches { get; init; } = [];
    public IReadOnlyList<IncidentOptionDto> Rooms { get; init; } = [];
    public IReadOnlyList<IncidentOptionDto> Areas { get; init; } = [];
    public IReadOnlyList<IncidentOptionDto> Assets { get; init; } = [];
    public IReadOnlyList<IncidentOptionDto> Severities { get; init; } = [];
    public IReadOnlyList<IncidentOptionDto> Priorities { get; init; } = [];
    public bool CanCreate { get; init; }
}
