using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
using Xunit;
namespace HotelOps.Dev1.RegressionTests;

// No SQL writes: deliberately absent persistence collaborators must never be reached by invalid input.
public sealed class CleaningLifecycleContractTests
{
    private static RoomTypeCreationService Service() => new(null!, null!, null!, null!, TimeProvider.System);

    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("W/\"0000000000000001\"")]
    [InlineData("\"000000000000000a\"")]
    [InlineData("\"AAAAAAAAAAA=\"")]
    [InlineData("\"0000000000000001\",\"0000000000000002\"")]
    public async Task Assignment_rejects_non_authoritative_rowversion_before_persistence(string tag)
        => await Assert.ThrowsAsync<ValidationException>(() => Service().AssignCleaningAsync(null!, 0, 1, 1, tag,
            new(1, DateTime.UtcNow.AddHours(1), "assignment reason"), default));

    [Theory]
    [InlineData(0, 1, "valid", true)]
    [InlineData(1, 0, "valid", true)]
    [InlineData(1, 1, "", true)]
    [InlineData(1, 1, "bad\nreason", true)]
    [InlineData(1, 1, "valid", false)]
    public async Task Assignment_rejects_invalid_identity_reason_or_deadline_before_persistence(long task, long assignee, string reason, bool future)
        => await Assert.ThrowsAsync<ValidationException>(() => Service().AssignCleaningAsync(null!, 0, 1, task, "\"0000000000000001\"",
            new(assignee, DateTime.UtcNow.AddHours(future ? 1 : -1), reason), default));

    [Fact]
    public async Task Planning_rejects_non_UTC_or_oversized_windows_before_persistence()
    {
        var from = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
        await Assert.ThrowsAsync<ValidationException>(() => Service().HousekeepingPlanningAsync(null!, 0, 1, 1, false,
            new(From: DateTime.SpecifyKind(from, DateTimeKind.Unspecified), To: from.AddDays(1)), default));
        await Assert.ThrowsAsync<ValidationException>(() => Service().HousekeepingPlanningAsync(null!, 0, 1, 1, false,
            new(From: from, To: from.AddDays(3)), default));
    }

    [Fact]
    public void Existing_assignment_contract_does_not_claim_notification_delivery()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        using var request = JsonDocument.Parse(JsonSerializer.Serialize(new AssignCleaningRequest(7,
            new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc), "reason"), options));
        Assert.Equal(new[] { "adoptUnassignedLegacy", "assigneeId", "dueAt", "reason" },
            request.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.False(request.RootElement.GetProperty("adoptUnassignedLegacy").GetBoolean());
        Assert.Equal("NOT_INTEGRATED", new HousekeepingWriteDto(1, "ASSIGNED").Notification);
    }
}
