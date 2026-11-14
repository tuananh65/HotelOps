using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using HotelOps.Api.Services.Dependencies;
using HotelOps.Api.Entities;

internal static class GrantImpactChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var now = new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);
        RevokeGrant Grant(long id, string scope = "OWN", long? branch = 3, string permission = "MAINTENANCE_EXECUTE") =>
            new(id, new(permission, scope, branch, true, true, now.AddDays(-1), null, true, true));
        var before = new[] { Grant(10), Grant(11) };
        foreach (var state in new[] { "PENDING", "IN_PROGRESS", "ON_HOLD", "COMPLETED" })
        {
            check(RevokeRoleGrantImpact.Maintenance(state, true, before, [], 3, now) == RevokeCapabilityImpact.Lost, state + " executor capability loss blocks revoke");
            check(RevokeRoleGrantImpact.Maintenance(state, true, before, RevokeRoleGrantImpact.Without(before, 10), 3, now) == RevokeCapabilityImpact.Preserved, state + " surviving assignment preserves capability");
        }
        foreach (var state in new[] { "CLOSED", "CANCELLED" })
            check(RevokeRoleGrantImpact.Maintenance(state, true, before, [], 3, now) == RevokeCapabilityImpact.Preserved, state + " is terminal history");
        check(RevokeRoleGrantImpact.Maintenance("COMPLETED", false, before, [], 3, now) == RevokeCapabilityImpact.Preserved, "historical reporter/reviewer is not current assignee");
        check(RevokeRoleGrantImpact.Maintenance("NEW_UNKNOWN", true, before, before, 3, now) == RevokeCapabilityImpact.Unknown, "unknown workflow cannot be cleared by surviving grant");
        check(RevokeRoleGrantImpact.Maintenance("PENDING", true, before, before, null, now) == RevokeCapabilityImpact.Unknown, "ambiguous resource branch fails closed");
        check(RevokeRoleGrantImpact.Evaluate(before, [Grant(11, branch: 4)], "MAINTENANCE_EXECUTE", 3, true, now) == RevokeCapabilityImpact.Lost, "other branch grant cannot preserve capability");
        check(RevokeRoleGrantImpact.Evaluate(before, [Grant(11, "ALL", null)], "MAINTENANCE_EXECUTE", 3, true, now) == RevokeCapabilityImpact.Preserved, "eligible global grant preserves capability");
        check(RevokeRoleGrantImpact.Evaluate(before, [Grant(11) with { Path = Grant(11).Path with { ExpiresAt = now } }], "MAINTENANCE_EXECUTE", 3, true, now) == RevokeCapabilityImpact.Lost, "expired survivor does not preserve capability");
        check(RevokeRoleGrantImpact.Evaluate(before, [Grant(11, "UNSUPPORTED")], "MAINTENANCE_EXECUTE", 3, true, now) == RevokeCapabilityImpact.Unknown, "unknown scope cannot authorize");
        check(RevokeRoleGrantImpact.Evaluate([], [], "MAINTENANCE_EXECUTE", 3, true, now) == RevokeCapabilityImpact.Unknown, "inconsistent current assignment without capability fails closed");
        var requester = new[] { Grant(10, permission: "MR_CREATE"), Grant(11, permission: "MR_CREATE") };
        check(RevokeRoleGrantImpact.Evaluate(requester, RevokeRoleGrantImpact.Without(requester, 10), "MR_CREATE", 3, true, now)
            == RevokeCapabilityImpact.Preserved, "current requester retains capability through another grant");
        check(RevokeRoleGrantImpact.Evaluate(requester, [], "MR_CREATE", 3, true, now)
            == RevokeCapabilityImpact.Lost, "current requester loses required capability");
        check(RevokeRoleGrantImpact.Evaluate(before, [Grant(11) with { Path = Grant(11).Path with { BranchAccessActive = false } }], "MAINTENANCE_EXECUTE", 3, true, now)
            == RevokeCapabilityImpact.Lost, "revoked membership cannot preserve executor capability");
        check(RevokeRoleGrantImpact.Evaluate([Grant(10, permission: "MR_APPROVE")], [Grant(11, permission: "MR_APPROVE")], "MR_APPROVE", 3, false, now) == RevokeCapabilityImpact.Unknown, "OWN cannot grant branch approval");
        var request = new MaterialRequest { RequestId = 7, BranchId = 3, Status = "PENDING_APPROVAL" };
        var cycles = new[] {
            new MaterialRequestApprovalCycle { RequestId = 7, Request = request, AssignedApproverId = 8, RevisionNo = 1, Status = "PENDING" },
            new MaterialRequestApprovalCycle { RequestId = 7, Request = request, AssignedApproverId = 9, RevisionNo = 2, Status = "PENDING" }
        }.AsQueryable();
        check(!Dev2DependencyQueries.PendingApprovals(cycles, 8, 3).Any(), "superseded pending approver never resurrected");
        check(Dev2DependencyQueries.PendingApprovals(cycles, 9, 3).Count() == 1, "latest current pending approver retained");
        request.Status = "CANCELLED";
        check(!Dev2DependencyQueries.PendingApprovals(cycles, 9, 3).Any(), "terminal parent cannot retain approval obligation");
        check(RevokeRoleGrantImpact.Maintenance("PENDING", true, before, [], 3, now) == RevokeCapabilityImpact.Lost, "writer-first snapshot requires denial on lost capability (offline expectation)");
        check(UserRoleRevocationService.PendingResponsibilityWriters.Count > 0 && UserRoleRevocationService.PendingReleaseGates.Count > 0,
            "revoke-first writer coordination remains gated; no SQL race claimed");
    }
}
