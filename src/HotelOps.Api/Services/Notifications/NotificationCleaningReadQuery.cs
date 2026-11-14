using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Notifications;

// Existing persisted assignment and CLEANING_VIEW grant predicate only.
// This is not proof of the missing FS020 operation/current-cycle/source-revision graph.
public static class NotificationCleaningReadQuery
{
    public static IQueryable<CleaningAssignment> Apply(IQueryable<CleaningAssignment> assignments,
        IQueryable<Branch> branches, IQueryable<UserRole> roles, IQueryable<RolePermission> permissions,
        IQueryable<UserBranchAccess> access, long recipientUserId, DateTime now)
    {
        return assignments.Where(a => a.AssignmentId > 0 && a.CleaningTaskId > 0 &&
            a.AssigneeUserId == recipientUserId && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now &&
            a.CleaningTask.Room.IsActive && branches.Any(b => b.BranchId == a.CleaningTask.Room.BranchId && b.IsActive) &&
            // An ambiguous current assignment must not choose an arbitrary recipient.
            assignments.Count(other => other.CleaningTaskId == a.CleaningTaskId && other.AssignmentStatus == "ACTIVE" &&
                other.EndedAt == null && other.AssignedAt <= now) == 1 &&
            roles.Any(r => r.UserId == recipientUserId && r.IsActive && r.Role.IsActive && r.AssignedAt <= now &&
                (r.ExpiresAt == null || r.ExpiresAt > now) && r.User.AccountStatus == "ACTIVE" &&
                r.User.EmploymentStatus == "ACTIVE" && r.User.DeletedAt == null &&
                permissions.Any(p => p.RoleId == r.RoleId && p.Permission.PermissionCode == "CLEANING_VIEW" &&
                    ((p.DataScope == "ALL" && r.BranchId == null) ||
                     (r.BranchId == a.CleaningTask.Room.BranchId &&
                      access.Any(b => b.UserId == recipientUserId && b.BranchId == a.CleaningTask.Room.BranchId &&
                          b.GrantedAt <= now && b.RevokedAt == null) &&
                      (p.DataScope == "ALL" || p.DataScope == "BRANCH" || p.DataScope == "OWN" && a.AssigneeUserId == recipientUserId))))));
    }
}
