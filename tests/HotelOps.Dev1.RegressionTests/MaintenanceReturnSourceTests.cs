using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MaintenanceReturnSourceTests
{
    private static readonly DateTime At = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    private static Dictionary<string, object?> Values() => new()
    {
        ["maintenance_work_id"] = 7L, ["incident_id"] = 8L, ["assignee_id"] = 9L,
        ["status"] = "IN_PROGRESS", ["resolved_at"] = null, ["return_reason"] = "Replace the damaged seal",
        ["notification_kind"] = MaintenanceReturnSource.Kind, ["notification_version"] = 1
    };
    private static AuditLog Audit(Dictionary<string, object?> values) => new()
    {
        AuditId = 1, EntityType = "maintenance_works", EntityId = "7", Action = "UPDATE",
        BranchId = 2, ActorUserId = 10, OccurredAt = At,
        OldValues = "{\"maintenance_work_id\":7,\"incident_id\":8,\"assignee_id\":9,\"status\":\"COMPLETED\"}",
        NewValues = JsonSerializer.Serialize(values)
    };

    [Fact]
    public void Return_intent_binds_transition_source_and_original_assignee()
    {
        Assert.Equal(new MaintenanceReturnSource.Source(1, 7, 8, 2, 10, 9, At, "Replace the damaged seal"),
            MaintenanceReturnSource.Parse(Audit(Values())));
        foreach (var change in new Dictionary<string, object?>
        {
            ["maintenance_work_id"] = 11L, ["incident_id"] = 11L, ["assignee_id"] = 11L,
            ["status"] = "ON_HOLD", ["resolved_at"] = At, ["return_reason"] = " ",
            ["notification_kind"] = "MAINTENANCE_SUBMITTED", ["notification_version"] = 2
        })
        {
            var values = Values(); values[change.Key] = change.Value;
            Assert.Null(MaintenanceReturnSource.Parse(Audit(values)));
        }
    }

    [Fact]
    public void Self_return_missing_actor_branch_and_wrong_transition_are_rejected()
    {
        var audit = Audit(Values()); audit.ActorUserId = 9;
        Assert.Null(MaintenanceReturnSource.Parse(audit));
        audit = Audit(Values()); audit.ActorUserId = null;
        Assert.Null(MaintenanceReturnSource.Parse(audit));
        audit = Audit(Values()); audit.BranchId = 0;
        Assert.Null(MaintenanceReturnSource.Parse(audit));
        audit = Audit(Values()); audit.AuditId = 0;
        Assert.Null(MaintenanceReturnSource.Parse(audit));
        audit = Audit(Values()); audit.OldValues = audit.OldValues!.Replace("COMPLETED", "CLOSED");
        Assert.Null(MaintenanceReturnSource.Parse(audit));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("invalid json")]
    public void Missing_or_malformed_audit_cannot_be_used_as_notification_authority(string json)
    {
        var audit = Audit(Values()); audit.NewValues = json;
        Assert.Null(MaintenanceReturnSource.Parse(audit));
    }
}
