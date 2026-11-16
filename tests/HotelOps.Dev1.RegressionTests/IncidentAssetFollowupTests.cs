using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Notifications;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentAssetFollowupTests
{
    [Theory]
    [InlineData("DAMAGED", true)]
    [InlineData("UNDER_REPAIR", true)]
    [InlineData("IN_USE", false)]
    [InlineData("", false)]
    public void Only_verified_closure_with_a_relevant_asset_condition_creates_followup(string condition, bool expected)
    {
        var at = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var audit = new AuditLog { AuditId = 1, EntityType = "maintenance_works", EntityId = "7", Action = "UPDATE",
            BranchId = 2, ActorUserId = 10, OccurredAt = at,
            OldValues = "{\"maintenance_work_id\":7,\"incident_id\":8,\"assignee_id\":9,\"status\":\"COMPLETED\"}",
            NewValues = JsonSerializer.Serialize(new { maintenance_work_id = 7, incident_id = 8, assignee_id = 9,
                closure_reporter_id = 11, status = "CLOSED", reviewed_by = 10, reviewed_at = at,
                verification_note = "Repair verified successfully", notification_kind = "MAINTENANCE_VERIFIED",
                notification_version = 1, closure_asset_id = 12, closure_asset_condition = condition }) };
        var result = IncidentAssetFollowupSource.Parse(audit);
        Assert.Equal(expected, result != null);
        if (result != null) { Assert.Equal(12, result.AssetId); Assert.Equal(condition, result.Condition); }
        audit.OldValues = audit.OldValues.Replace("COMPLETED", "IN_PROGRESS");
        Assert.Null(IncidentAssetFollowupSource.Parse(audit));
    }

    [Fact]
    public void Asset_read_scope_is_independent_current_and_ownership_bound()
    {
        var now = DateTime.UtcNow;
        var grant = new PermissionGrantPath("ASSET_VIEW", "BRANCH", 2, true, true, now.AddHours(-1), null, true, true);
        Assert.True(IncidentAssetFollowupSource.Allows([grant], 2, false, now));
        Assert.True(IncidentAssetFollowupSource.Allows([grant with { DataScope = "OWN" }], 2, true, now));
        Assert.False(IncidentAssetFollowupSource.Allows([grant with { DataScope = "OWN" }], 2, false, now));
        foreach(var path in new[] { grant with { PermissionCode="INCIDENT_VIEW" }, grant with { BranchId=3 },
            grant with { RoleActive=false }, grant with { AssignmentActive=false }, grant with { BranchAccessActive=false },
            grant with { ExpiresAt=now } }) Assert.False(IncidentAssetFollowupSource.Allows([path],2,true,now));
    }
}
