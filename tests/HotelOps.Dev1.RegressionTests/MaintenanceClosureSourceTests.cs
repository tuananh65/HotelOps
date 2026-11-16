using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MaintenanceClosureSourceTests
{
    private static readonly DateTime At = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    private static Dictionary<string, object?> Values() => new()
    {
        ["maintenance_work_id"] = 7L, ["incident_id"] = 8L, ["assignee_id"] = 9L,
        ["closure_reporter_id"] = 11L, ["status"] = "CLOSED", ["reviewed_by"] = 10L,
        ["reviewed_at"] = At, ["verification_note"] = "Repair verified successfully",
        ["notification_kind"] = MaintenanceClosureSource.Kind, ["notification_version"] = 1
    };
    private static AuditLog Audit(Dictionary<string, object?> values) => new()
    {
        AuditId = 1, EntityType = "maintenance_works", EntityId = "7", Action = "UPDATE",
        BranchId = 2, ActorUserId = 10, OccurredAt = At,
        OldValues = "{\"maintenance_work_id\":7,\"incident_id\":8,\"assignee_id\":9,\"status\":\"COMPLETED\"}",
        NewValues = JsonSerializer.Serialize(values)
    };

    [Fact]
    public void Closure_intent_binds_reviewer_source_time_and_recipients()
    {
        Assert.Equal(new MaintenanceClosureSource.Source(1,7,8,2,10,11,9,At,"Repair verified successfully"),
            MaintenanceClosureSource.Parse(Audit(Values())));
        foreach (var change in new Dictionary<string, object?>
        {
            ["maintenance_work_id"] = 12L, ["incident_id"] = 12L, ["assignee_id"] = 12L,
            ["closure_reporter_id"] = 0L, ["status"] = "IN_PROGRESS", ["reviewed_by"] = 9L,
            ["reviewed_at"] = At.AddSeconds(1), ["verification_note"] = " ",
            ["notification_kind"] = "MAINTENANCE_RETURNED", ["notification_version"] = 2
        })
        {
            var values = Values(); values[change.Key] = change.Value;
            Assert.Null(MaintenanceClosureSource.Parse(Audit(values)));
        }
    }

    [Fact]
    public void Self_verification_and_non_submission_transition_cannot_issue_intent()
    {
        var audit = Audit(Values()); audit.ActorUserId = 9;
        Assert.Null(MaintenanceClosureSource.Parse(audit));
        audit = Audit(Values()); audit.OldValues = audit.OldValues!.Replace("COMPLETED", "ON_HOLD");
        Assert.Null(MaintenanceClosureSource.Parse(audit));
        var sameRecipient = Values(); sameRecipient["closure_reporter_id"] = 9L;
        Assert.NotNull(MaintenanceClosureSource.Parse(Audit(sameRecipient))); // Delivery deduplicates recipient IDs.
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("invalid json")]
    public void Missing_or_malformed_audit_is_not_a_closure_intent(string json)
    {
        var audit = Audit(Values()); audit.NewValues = json;
        Assert.Null(MaintenanceClosureSource.Parse(audit));
    }
}
