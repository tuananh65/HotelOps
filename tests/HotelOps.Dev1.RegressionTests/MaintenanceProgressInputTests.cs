using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Maintenance;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MaintenanceProgressInputTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    [InlineData(101, false)]
    public void Percentage_has_explicit_bounds_and_legacy_notes_remain_valid(int? percent, bool valid)
    {
        var input = new CreateMaintenanceWorkLogRequest { ActorUserId = 1, WorkNote = "Repair update", ProgressPercent = percent };
        Assert.Equal(valid, Validator.TryValidateObject(input, new ValidationContext(input), [], true));
    }
}
