using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Services.Incidents;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentCreationInputTests
{
    private static CreateIncidentRequest Input(long? room, long? area, long? asset, string title = "Incident", string description = "Details")
        => new() { BranchId = 1, ReportedBy = 2, RoomId = room, AreaId = area, AssetId = asset,
            IncidentTitle = title, Description = description, DiscoveredAt = DateTime.UtcNow };

    [Theory]
    [InlineData(1L, null, null)]
    [InlineData(null, 2L, null)]
    [InlineData(null, null, 3L)]
    [InlineData(1L, 2L, 3L)]
    public void AnySupportedLocationSatisfiesPresenceOnly(long? room, long? area, long? asset)
        => IncidentCreationInput.Validate(Input(room, area, asset));

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(0L, null, null)]
    [InlineData(null, -1L, null)]
    [InlineData(null, null, 0L)]
    [InlineData(1L, -1L, null)]
    [InlineData(null, 1L, -1L)]
    public void MissingOrInvalidLocationIsRejectedEvenWithAnotherValidId(long? room, long? area, long? asset)
        => Assert.Throws<ValidationException>(() => IncidentCreationInput.Validate(Input(room, area, asset)));

    [Theory]
    [InlineData(" ", "Details")]
    [InlineData("Incident", " \t ")]
    public void MandatoryTextCannotBeWhitespace(string title, string description)
        => Assert.Throws<ValidationException>(() => IncidentCreationInput.Validate(Input(1, null, null, title, description)));

    [Fact]
    public void TitleRespectsActualPersistenceLimit()
        => Assert.Throws<ValidationException>(() => IncidentCreationInput.Validate(Input(1, null, null, new string('x', 201))));
}
