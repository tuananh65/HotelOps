using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ReceptionAccessTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
    private static PermissionGrantPath Grant(string permission, string scope = "OWN") =>
        new(permission, scope, 7, true, true, Now.AddDays(-1), null, true, true);

    [Fact]
    public void LinkedCleaningReadWithoutMonitorIsLimitedToCurrentOwnAssignment()
    {
        CleaningTask Row(long id, long assignee, DateTime assignedAt, DateTime? endedAt = null)
        {
            var row = new CleaningTask { CleaningTaskId = id };
            row.CleaningAssignments.Add(new() { AssigneeUserId = assignee, AssignedAt = assignedAt,
                EndedAt = endedAt, AssignmentStatus = "ACTIVE" });
            return row;
        }
        var rows = new[] { Row(1, 10, Now), Row(2, 99, Now), Row(3, 10, Now.AddMinutes(1)),
            Row(4, 10, Now.AddHours(-1), Now) }.AsQueryable();
        Assert.Equal(new long[] { 1 }, InternalRequestCleaningLinks.ApplyVisibility(rows, 10, Now, false, true).Select(t => t.CleaningTaskId));
        Assert.Empty(InternalRequestCleaningLinks.ApplyVisibility(rows, 10, Now, false, false));
        Assert.Equal(4, InternalRequestCleaningLinks.ApplyVisibility(rows, 10, Now, true, true).Count());
    }

    [Fact]
    public void ReceptionReportsOnlyWithinAnEligibleGrantedBranch()
    {
        Assert.True(IncidentCreationAccess.IsCreatorRole("RECEPTION"));
        var grant = Grant("IR_CREATE");
        Assert.True(InternalRequestIntakeAccess.Eligible("RECEPTION", grant, 7, Now));
        foreach (var invalid in new[] { grant with { AssignmentActive = false }, grant with { RoleActive = false },
            grant with { BranchAccessActive = false }, grant with { BranchId = 8 }, grant with { ExpiresAt = Now },
            grant with { AssignedAt = Now.AddTicks(1) }, grant with { PermissionCode = "IR_VIEW" },
            grant with { BranchId = null, DataScope = "GLOBAL" } })
            Assert.False(InternalRequestIntakeAccess.Eligible("RECEPTION", invalid, 7, Now));
    }

    [Fact]
    public void ReceptionCannotReadOtherReportersEvenWithHistoricalMaintenanceAssignment()
    {
        Incident Row(long id, long reporter, long branch = 7) => new() {
            IncidentId = id, ReportedBy = reporter, BranchId = branch, Branch = new() { IsActive = true } };
        var assigned = Row(2, 99);
        assigned.MaintenanceWorks.Add(new() { AssigneeId = 10 });
        var rows = new[] { Row(1, 10), assigned, Row(3, 10, 8) }.AsQueryable();
        var grant = Grant("INCIDENT_VIEW", "BRANCH");
        var scope = IncidentReadAccess.ResolveSource(10, 7, [new("RECEPTION", grant)], Now);
        Assert.Equal(new long[] { 1 }, scope.Apply(rows).Select(r => r.IncidentId));
        Assert.False(IncidentReadAccess.Resolve(10, 7, [new("RECEPTION", grant)], Now).CanRead);
        Assert.Empty(IncidentReadAccess.ResolveSource(10, 7, [new("RECEPTION", grant with { BranchAccessActive = false })], Now).Apply(rows));
    }

    [Fact]
    public void ReceptionInternalRequestViewIsRequesterOnly()
    {
        InternalRequest Row(long id, long requester, long branch = 7) => new() {
            InternalRequestId = id, RequesterId = requester, BranchId = branch, Branch = new() { IsActive = true } };
        var assigned = Row(2, 99);
        assigned.InternalRequestAssignments.Add(new() { AssigneeId = 10, Status = "ACTIVE", AssignedAt = Now });
        var rows = new[] { Row(1, 10), assigned, Row(3, 10, 8) }.AsQueryable();
        var grant = Grant("IR_VIEW");
        var scope = InternalRequestReadAccess.Resolve(10, [new("RECEPTION", grant)], Now);
        Assert.NotNull(scope.HandlerBranches);
        Assert.Empty(scope.HandlerBranches);
        Assert.Equal(new long[] { 1 }, scope.Apply(rows, Now).Select(r => r.InternalRequestId));
        Assert.Empty(scope.Branches);
        Assert.False(scope.Global);
        Assert.Empty(InternalRequestReadAccess.Resolve(10, [new("RECEPTION", grant with { ExpiresAt = Now })], Now).Apply(rows, Now));
    }
}
