using HotelOps.Api.Services.Incidents;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentSlaReadTests
{
    private static readonly DateTime Discovered = new(2026, 11, 16, 8, 0, 0);
    [Fact]
    public void MissingDeadlinesAreNotInferred()
    {
        var result = IncidentSlaRead.Normalize(Discovered, null, null);
        Assert.Null(result.ResponseDeadlineAt);
        Assert.Null(result.ResolutionDeadlineAt);
        Assert.Equal("INCIDENT_SLA_NOT_RECORDED", result.UnavailableReason);
    }
    [Fact]
    public void RecordedValuesRemainExactAndUtc()
    {
        var result = IncidentSlaRead.Normalize(Discovered, Discovered.AddHours(1), Discovered.AddHours(4));
        Assert.Equal(Discovered.AddHours(1), result.ResponseDeadlineAt);
        Assert.Equal(Discovered.AddHours(4), result.ResolutionDeadlineAt);
        Assert.Equal(DateTimeKind.Utc, result.ResponseDeadlineAt!.Value.Kind);
        Assert.Null(result.UnavailableReason);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvalidHistoricalDeadlineDoesNotBecomeTrusted(bool response)
    {
        var result = IncidentSlaRead.Normalize(Discovered,
            response ? Discovered.AddTicks(-1) : Discovered, response ? Discovered : Discovered.AddTicks(-1));
        Assert.Null(result.ResponseDeadlineAt);
        Assert.Null(result.ResolutionDeadlineAt);
        Assert.Equal("INCIDENT_SLA_INVALID", result.UnavailableReason);
    }
    [Fact]
    public void PartialHistoryPreservesKnownValueAndExplainsMissingValue()
    {
        var result = IncidentSlaRead.Normalize(Discovered, Discovered.AddMinutes(30), null);
        Assert.Equal(Discovered.AddMinutes(30), result.ResponseDeadlineAt);
        Assert.Null(result.ResolutionDeadlineAt);
        Assert.Equal("INCIDENT_SLA_NOT_RECORDED", result.UnavailableReason);
    }
}
