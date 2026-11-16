using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentRequestCreationTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(1L, "AAAAAAAAAAA=")]
    public void OptionalLinkUsesExplicitCanonicalVersion(long? id, string? version)
    {
        var input = new CreateIncidentRequest { InternalRequestId = id,
            RequestLink = version is null ? null : new(version, false) };
        Assert.Equal(id is null ? null : new byte[8], IncidentRequestCreation.Validate(input));
    }

    [Theory]
    [InlineData(null, "AAAAAAAAAAA=")]
    [InlineData(0L, "AAAAAAAAAAA=")]
    [InlineData(1L, null)]
    [InlineData(1L, "")]
    [InlineData(1L, "AAAAAAAAAAA= ")]
    [InlineData(1L, "AAAAAAAAAAB=")]
    public void IncompleteOrNoncanonicalLinkIsRejected(long? id, string? version)
        => Assert.Throws<ValidationException>(() => IncidentRequestCreation.Validate(new() {
            InternalRequestId = id, RequestLink = version is null ? null : new(version, true) }));

    [Fact]
    public void MandatoryCannotBeOmittedFromTransport()
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IncidentRequestLink>("{\"Version\":\"AAAAAAAAAAA=\"}"));

    [Fact]
    public async Task SourceLinkCannotBypassIncidentCommandAdmission()
    {
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=invalid;Database=not-used;Integrated Security=true").Options);
        await Assert.ThrowsAsync<HotelOps.Api.Services.Dev2Authorization.Dev2AccessDenied>(() =>
            new IncidentService(db).CreateIncidentAsync(new() { InternalRequestId = 1, RequestLink = new("AAAAAAAAAAA=", true) }, default));
    }
}
