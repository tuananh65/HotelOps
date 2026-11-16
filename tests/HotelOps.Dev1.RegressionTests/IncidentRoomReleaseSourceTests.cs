using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Security.Authorization;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentRoomReleaseSourceTests
{
    [Fact]
    public void Room_grant_requires_current_scope_and_cannot_be_replaced_by_incident_access()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var path = new PermissionGrantPath("ROOM_VIEW", "BRANCH", 9, true, true, now.AddDays(-1), null, true, true);
        Assert.True(IncidentRoomReleaseSource.AllowsRoomGrant([path], 9, now));
        foreach (var denied in new[] { path with { PermissionCode = "INCIDENT_VIEW" }, path with { BranchId = 10 },
            path with { AssignmentActive = false }, path with { RoleActive = false }, path with { BranchActive = false },
            path with { BranchAccessActive = false }, path with { ExpiresAt = now }, path with { AssignedAt = now.AddMinutes(1) },
            path with { DataScope = "OWN" } })
            Assert.False(IncidentRoomReleaseSource.AllowsRoomGrant([denied], 9, now));
    }

    private static AuditLog Event(long[] ids, string readiness = "BLOCKED", string kind = IncidentRoomReleaseSource.Kind)
        => new() { AuditId = 7, EntityType = "rooms", EntityId = "8", Action = "UPDATE", BranchId = 9,
            ActorUserId = 10, OccurredAt = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc),
            OldValues = JsonSerializer.Serialize(new { maintenance_work_id = 11, released_block_ids = Array.Empty<long>() }),
            NewValues = JsonSerializer.Serialize(new { maintenance_work_id = 11, incident_id = 12,
                notification_kind = kind, notification_version = 1, released_block_ids = ids, effective_readiness = readiness }) };

    [Theory]
    [InlineData("BLOCKED")]
    [InlineData("READY")]
    [InlineData("PENDING_CLEANING")]
    public void Release_is_a_room_event_not_a_ready_assertion(string readiness)
    {
        var source = Assert.IsType<IncidentRoomReleaseSource.Source>(IncidentRoomReleaseSource.Parse(Event([14, 13], readiness)));
        Assert.Equal("13,14", source.BlockIds);
        Assert.Equal(readiness, source.Readiness);
        Assert.Equal(8, source.RoomId);
        Assert.Equal(9, source.BranchId);
        Assert.Equal(11, source.WorkId);
        Assert.Equal(12, source.IncidentId);
    }

    [Fact]
    public void Missing_release_duplicate_blocks_and_foreign_markers_are_rejected()
    {
        Assert.Null(IncidentRoomReleaseSource.Parse(Event([])));
        Assert.Null(IncidentRoomReleaseSource.Parse(Event([13, 13])));
        Assert.Null(IncidentRoomReleaseSource.Parse(Event([0])));
        Assert.Null(IncidentRoomReleaseSource.Parse(Event([13], "UNRECOGNIZED")));
        Assert.Null(IncidentRoomReleaseSource.Parse(Event([13], kind: "MAINTENANCE_VERIFIED")));
        var audit = Event([13]); audit.EntityType = "incidents";
        Assert.Null(IncidentRoomReleaseSource.Parse(audit));
        audit = Event([13]); audit.OldValues = "{}";
        Assert.Null(IncidentRoomReleaseSource.Parse(audit));
    }
}
