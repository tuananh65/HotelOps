using System.Text.Json;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Maintenance;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MaintenanceAssignmentProofTests
{
    private static readonly DateTime At = new(2026, 11, 16, 8, 0, 0);
    private static Incident Incident() => new() { IncidentId = 7, BranchId = 3, PriorityId = 4, DiscoveredAt = At.AddHours(-1) };
    private static IncidentEvent Event() => new() { IncidentId = 7, EventType = "TRIAGED", ActorId = 9, OccurredAt = At };
    private static IncidentSlaDto Sla() => new(At.AddHours(1), At.AddHours(4), null);
    private static AuditLog Audit() => new() { AuditId = 11, EntityType = "incidents", EntityId = "7", Action = "UPDATE",
        BranchId = 3, ActorUserId = 9, OccurredAt = At,
        NewValues = JsonSerializer.Serialize(new { priority_id = 4, response_deadline_at = At.AddHours(1), resolution_deadline_at = At.AddHours(4) }) };
    [Fact]
    public void MatchingBusinessEventAndSlaAuditProveTriage()
        => Assert.True(MaintenanceAssignmentProof.MatchesTriage(Audit(), Incident(), Sla(), Event()));
    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("invalid")]
    [InlineData("{\"priority_id\":4,\"severity_id\":2}")]
    public void LegacyPriorityOnlyOrMalformedAuditDoesNotProveFullTriage(string json)
    {
        var audit = Audit(); audit.NewValues = json;
        Assert.False(MaintenanceAssignmentProof.MatchesTriage(audit, Incident(), Sla(), Event()));
    }
    [Fact]
    public void ChangedPriorityDeadlineOrEventIdentityInvalidatesProof()
    {
        var incident = Incident(); incident.PriorityId = 5;
        Assert.False(MaintenanceAssignmentProof.MatchesTriage(Audit(), incident, Sla(), Event()));
        Assert.False(MaintenanceAssignmentProof.MatchesTriage(Audit(), Incident(), Sla() with { ResponseDeadlineAt = At.AddHours(2) }, Event()));
        var triage = Event(); triage.ActorId = 8;
        Assert.False(MaintenanceAssignmentProof.MatchesTriage(Audit(), Incident(), Sla(), triage));
        triage = Event(); triage.OccurredAt = At.AddSeconds(1);
        Assert.False(MaintenanceAssignmentProof.MatchesTriage(Audit(), Incident(), Sla(), triage));
        Assert.False(MaintenanceAssignmentProof.MatchesTriage(Audit(), Incident(), Sla() with { UnavailableReason = "INCIDENT_SLA_INVALID" }, Event()));
    }
}
