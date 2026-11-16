using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.Notifications;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MaintenanceDeclineTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("          ")]
    [InlineData("too short")]
    [InlineData("valid reason\u0000")]
    public void InvalidReasonCannotBecomeReceipt(string? reason)
        => Assert.Throws<ValidationException>(() => MaintenanceDeclineService.ValidateReason(reason));

    [Fact]
    public void ReasonMatchesStorageAndTrims()
    {
        Assert.Equal("Cannot take this work", MaintenanceDeclineService.ValidateReason("  Cannot take this work  "));
        Assert.Equal(2000, MaintenanceDeclineService.ValidateReason(new string('x', 2000)).Length);
        Assert.Throws<ValidationException>(() => MaintenanceDeclineService.ValidateReason(new string('x', 2001)));
    }

    private static AuditLog Event(long previous = 42, string status = "PENDING", long? assignee = null) => new()
    {
        AuditId = 8, BranchId = 3, ActorUserId = 42, EntityType = "maintenance_works", EntityId = "7", Action = "UPDATE",
        OccurredAt = new(2026, 11, 16, 10, 0, 0),
        OldValues = JsonSerializer.Serialize(new { maintenance_work_id = 7, incident_id = 9, status = "PENDING", assignee_id = 42 }),
        NewValues = JsonSerializer.Serialize(new { maintenance_work_id = 7, incident_id = 9, status, assignee_id = assignee,
            previous_assignee_id = previous, reason = "Cannot take this assignment", notification_kind = "MAINTENANCE_DECLINED", notification_version = 1 })
    };

    [Fact]
    public void NotificationRequiresExactCommittedDeclineShape()
    {
        var source = MaintenanceDeclineSource.Parse(Event());
        Assert.NotNull(source); Assert.Equal(42, source.ActorId); Assert.Equal(7, source.WorkId);
        Assert.Null(MaintenanceDeclineSource.Parse(Event(previous: 43)));
        Assert.Null(MaintenanceDeclineSource.Parse(Event(status: "IN_PROGRESS")));
        Assert.Null(MaintenanceDeclineSource.Parse(Event(assignee: 42)));
        var broken = Event(); broken.NewValues = "{}";
        Assert.Null(MaintenanceDeclineSource.Parse(broken));
        broken = Event(); broken.AuditId = 0;
        Assert.Null(MaintenanceDeclineSource.Parse(broken));
    }
}
