using HotelOps.Api.Data;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

internal static class RevokeHandoverResponsibilities
{
    internal static async Task<UserResponsibilityAssessment> ReadAsync(HotelOpsDbContext db, long target,
        IReadOnlyList<RevokeGrant> before, IReadOnlyList<RevokeGrant> after,
        IReadOnlyList<RevokeActorAssignment> beforeActors, IReadOnlyList<RevokeActorAssignment> afterActors,
        DateTime now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Owning role transaction required.");
        var blockers = new SortedSet<string>(StringComparer.Ordinal);
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        void Record(RevokeCapabilityImpact impact, string fact)
        {
            if (impact == RevokeCapabilityImpact.Lost) blockers.Add(fact);
            if (impact == RevokeCapabilityImpact.Unknown) unresolved.Add(fact + "_AUTHORITY_UNRESOLVED");
        }
        var ir = await InternalRequestHandoverResponsibilities.ReadAsync(db, target, ct);
        unresolved.UnionWith(ir.Unresolved);
        if (ir.Unresolved.Count == 0)
        {
            var rows = await InternalRequestHandoverStore.Rows(db).Where(h => h.State == "PENDING_ACKNOWLEDGMENT"
                && (h.ReceiverId == target || db.InternalRequestAssignments.Any(a => a.AssignmentId == h.SenderAssignmentId && a.AssigneeId == target)))
                .ToArrayAsync(ct);
            var membershipInstalled = await InternalRequestEmployeeMembership.InstalledAsync(db, ct);
            foreach (var row in rows)
            {
                // Receipt ownership is explicit for both participants; it does not grant
                // access to the full request. Decisions still require eligible membership.
                var member = membershipInstalled && await InternalRequestEmployeeMembership.Members(db,
                    row.BranchId, row.DepartmentId, now).AnyAsync(u => u.UserId == target, ct);
                Record(RevokeRoleGrantImpact.InternalRequestHandler("IN_PROGRESS", before, after,
                    row.BranchId, now, member, false), $"IR_HANDOVER:{row.Id}");
            }
        }
        if (!await ShiftHandoverStore.InstalledAsync(db, ct)) unresolved.Add("SHIFT_HANDOVER_STORAGE_REQUIRED");
        else
        {
            var rows = await ShiftHandoverStore.Rows(db).Where(h => h.State == "PENDING_ACKNOWLEDGMENT"
                && (h.SenderId == target || h.ReceiverId == target)).ToArrayAsync(ct);
            string[] roles = ["HK_SUPERVISOR", "HK_STAFF", "MAINTENANCE_LEAD", "MAINTENANCE_STAFF", "STOREKEEPER", "DEPT_MANAGER"];
            foreach (var row in rows)
            {
                var fact = $"SHIFT_HANDOVER:{row.Id}";
                var read = RevokeRoleGrantImpact.Evaluate(before, after, "SHIFT_HANDOVER_VIEW", row.BranchId, true, now);
                var action = RevokeRoleGrantImpact.Evaluate(before, after,
                    row.ReceiverId == target ? "SHIFT_HANDOVER_DECIDE" : "SHIFT_HANDOVER_SUBMIT", row.BranchId, true, now);
                Record(read, fact + "_READ");
                Record(action, fact + "_ACTION");
                var wasActor = RevokeActorAssignment.Has(beforeActors, row.BranchId, now, true, roles);
                var remainsActor = RevokeActorAssignment.Has(afterActors, row.BranchId, now, true, roles);
                Record(!wasActor ? RevokeCapabilityImpact.Unknown : remainsActor
                    ? RevokeCapabilityImpact.Preserved : RevokeCapabilityImpact.Lost, fact + "_ACTOR");
                if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == target && a.BranchId == row.BranchId
                    && a.RevokedAt == null && a.GrantedAt <= now && a.Branch.IsActive, ct))
                    unresolved.Add(fact + "_SCOPE_UNAVAILABLE");
                if (row.ReceiverId == target && (!await WorkdayMembershipStore.InstalledAsync(db, ct)
                    || !await WorkdayMembershipStore.Employees(db, row.BranchId, row.DepartmentId, now).AnyAsync(u => u.UserId == target, ct)
                    || !await db.ShiftAssignments.AsNoTracking().AnyAsync(a => a.ShiftAssignmentId == row.ReceiverAssignmentId
                        && a.UserId == target && a.AssignmentStatus == "ASSIGNED" && a.AssignedAt <= now
                        && a.Shift.BranchId == row.BranchId && a.Shift.DepartmentId == row.DepartmentId && a.Shift.Status != "CANCELLED", ct)))
                    unresolved.Add(fact + "_RECEIVER_UNAVAILABLE");
            }
        }
        return new(blockers.ToArray(), unresolved.ToArray());
    }
}
