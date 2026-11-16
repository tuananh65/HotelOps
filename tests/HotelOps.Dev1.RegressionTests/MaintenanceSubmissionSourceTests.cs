using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MaintenanceSubmissionSourceTests
{
    private static readonly DateTime At = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    private static Dictionary<string, object?> Values() => new()
    {
        ["status"] = "COMPLETED", ["notification_kind"] = MaintenanceSubmissionSource.Kind,
        ["notification_version"] = 1, ["maintenance_work_id"] = 7L, ["incident_id"] = 8L,
        ["assignee_id"] = 9L, ["resolved_at"] = At, ["submitted_diagnosis"] = "Worn seal",
        ["submitted_action_taken"] = "Replaced seal"
    };
    private static AuditLog Audit(Dictionary<string, object?> values) => new()
    {
        AuditId = 1, EntityType = "maintenance_works", EntityId = "7", Action = "UPDATE",
        BranchId = 2, ActorUserId = 9, OccurredAt = At,
        OldValues = "{\"status\":\"IN_PROGRESS\"}", NewValues = JsonSerializer.Serialize(values)
    };

    [Fact]
    public void Intent_binds_source_actor_branch_time_and_exact_transition()
    {
        Assert.Equal(new MaintenanceSubmissionSource.Source(1, 7, 8, 2, 9, At), MaintenanceSubmissionSource.Parse(Audit(Values())));
        foreach (var change in new Dictionary<string, object?>
        {
            ["status"] = "IN_PROGRESS", ["notification_kind"] = "TASK_SUBMITTED", ["notification_version"] = 2,
            ["maintenance_work_id"] = 10L, ["incident_id"] = 0L, ["assignee_id"] = 10L,
            ["resolved_at"] = At.AddSeconds(1), ["submitted_diagnosis"] = " ", ["submitted_action_taken"] = null
        })
        {
            var values = Values(); values[change.Key] = change.Value;
            Assert.Null(MaintenanceSubmissionSource.Parse(Audit(values)));
        }
        var audit = Audit(Values()); audit.OldValues = "{\"status\":\"CLOSED\"}";
        Assert.Null(MaintenanceSubmissionSource.Parse(audit));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("invalid json")]
    public void Malformed_or_legacy_audit_is_not_an_intent(string json)
    {
        var audit = Audit(Values()); audit.NewValues = json;
        Assert.Null(MaintenanceSubmissionSource.Parse(audit));
    }
}
