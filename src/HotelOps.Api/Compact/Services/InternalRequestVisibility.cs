using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

internal static class InternalRequestVisibility
{
    // BRANCH read permission does not make an employee a request coordinator.
    public static IQueryable<WorkItem> Apply(IQueryable<WorkItem> query, HotelOpsCompactDbContext db, long userId, DateTime now)
    {
        return query.Where(w => w.Kind != "INTERNAL_REQUEST" || w.CreatedBy == userId
            || db.WorkAssignments.Any(a => a.WorkId == w.WorkId && a.AssigneeUserId == userId
                && (a.AssignmentStatus == "ACTIVE" || a.AssignmentStatus == "COMPLETED" || (w.Status == "CANCELLED" && a.AssignmentStatus == "CANCELLED")))
            || db.WorkEvents.Any(e => e.WorkId == w.WorkId && e.EventType == "HANDOVER_OFFERED" && e.RecipientUserId == userId
                && !db.WorkEvents.Any(d => d.RelatedEventId == e.WorkEventId && (d.EventType == "HANDOVER_ACCEPTED" || d.EventType == "HANDOVER_REJECTED")))
            || db.Memberships.Any(m => m.UserId == userId && m.BranchId == w.BranchId
                && m.Kind == "DEPARTMENT_AUTHORITY" && m.DepartmentId == w.DepartmentId
                && m.ValidFrom <= now && (m.ValidTo == null || m.ValidTo > now))
            || (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.RoleId
                where ur.UserId == userId && ur.AssignedAt <= now && ur.RevokedAt == null
                    && (ur.ExpiresAt == null || ur.ExpiresAt > now) && role.IsActive
                    && (ur.BranchId == w.BranchId || ur.BranchId == null)
                    && (role.Code == "OPERATION_MANAGER" || role.Code == "OWNER")
                select ur.UserRoleId).Any());
    }
}

