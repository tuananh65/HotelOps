using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Services.Incidents;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentTriageDeadlineTests
{
    private static readonly DateTime Discovered = new(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);
    private static IncidentTriageStore.Target Target() => new() { ResponseMinutes = 37, ResolutionMinutes = 721, Version = "synthetic" };
    [Fact]
    public void ConfiguredTargetsUseDiscoveryWithoutHardcodedPriorityDefaults()
    {
        var result = IncidentTriageStore.Calculate(Discovered, Target(), new());
        Assert.Equal(Discovered.AddMinutes(37), result.Response);
        Assert.Equal(Discovered.AddMinutes(721), result.Resolution);
        Assert.Null(result.OverrideReason);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("          ")]
    [InlineData("short")]
    public void OverrideRequiresNonWhitespaceReason(string? reason)
        => Assert.Throws<ValidationException>(() => IncidentTriageStore.Calculate(Discovered, Target(), new()
            { ResponseDeadlineAt = Discovered.AddHours(2), DeadlineChangeReason = reason }));
    [Fact]
    public void OverridePreservesUnchangedDeadlineAndReason()
    {
        var result = IncidentTriageStore.Calculate(Discovered, Target(), new()
            { ResolutionDeadlineAt = Discovered.AddHours(24), DeadlineChangeReason = "  Approved vendor schedule  " });
        Assert.Equal(Discovered.AddMinutes(37), result.Response);
        Assert.Equal(Discovered.AddHours(24), result.Resolution);
        Assert.Equal("Approved vendor schedule", result.OverrideReason);
    }
    [Fact]
    public void UnchangedExplicitDefaultDoesNotRequireOverrideReason()
        => Assert.Null(IncidentTriageStore.Calculate(Discovered, Target(), new()
            { ResponseDeadlineAt = Discovered.AddMinutes(37) }).OverrideReason);
    [Fact]
    public void InvalidTargetsDateOverflowAndNonUtcInputAreRejected()
    {
        Assert.Throws<ValidationException>(() => IncidentTriageStore.Calculate(Discovered, new() { ResponseMinutes = 0, ResolutionMinutes = 1 }, new()));
        Assert.Throws<ValidationException>(() => IncidentTriageStore.Calculate(DateTime.MaxValue, Target(), new()));
        Assert.Throws<ValidationException>(() => IncidentTriageStore.Calculate(Discovered, Target(), new()
            { ResponseDeadlineAt = Discovered.AddSeconds(-1), DeadlineChangeReason = "Reviewed override" }));
        Assert.Throws<ValidationException>(() => IncidentTriageStore.Calculate(Discovered, Target(), new()
            { ResolutionDeadlineAt = DateTime.SpecifyKind(Discovered.AddHours(2), DateTimeKind.Unspecified), DeadlineChangeReason = "Reviewed override" }));
    }
}
