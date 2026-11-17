using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal static class TaskAlertSource
{
    internal static async Task<WorkAlertCondition> ConditionAsync(HotelOpsDbContext db, long id, DateTime now, decimal threshold, CancellationToken ct)
    {
        if (!await ProgressSchema.InstalledAsync(db,ct)) return WorkAlertCondition.Unknown;
        var task = await db.OperationalTasks.AsNoTracking().SingleOrDefaultAsync(t => t.TaskId == id,ct);
        if (task == null) return WorkAlertCondition.Unknown;
        var state = await TaskWorkflowRead.Query(db,now).Where(t => t.TaskId == id).Select(t => t.State).SingleOrDefaultAsync(ct);
        if (state == null) return WorkAlertCondition.Unknown;
        // Approved source-specific origin; never substitute creation time.
        return WorkAlertWindow.Evaluate(state is "CLOSED" or "CANCELLED",task.DueAt,task.PlannedStartAt,now,threshold);
    }

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db,long user,WorkAlertSourceRow episode,CancellationToken ct)
    {
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var data=new EfPermissionDataSource(db);
        if(!await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==user&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null,ct)
            ||!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a=>a.UserId==user&&a.BranchId==episode.BranchId
                &&a.Branch.IsActive&&a.GrantedAt<=now&&a.RevokedAt==null,ct))return false;
        var task=await db.OperationalTasks.AsNoTracking().Where(t=>t.TaskId==episode.SourceId&&t.BranchId==episode.BranchId
            &&t.Department.BranchId==episode.BranchId&&(t.RoomId==null||t.Room!.BranchId==episode.BranchId)
            &&(t.AreaId==null||t.Area!.BranchId==episode.BranchId)).Select(t=>new{t.DepartmentId}).SingleOrDefaultAsync(ct);
        if(task==null)return false;
        var paths=await data.ReadPathsAsync(user,"TASK_VIEW",now,ct);
        var wide=paths.Any(p=>PermissionGrantRules.IsEligible(p,now)&&(PermissionGrantRules.EffectiveScope(p)=="GLOBAL"
            ||p.BranchId==episode.BranchId&&PermissionGrantRules.EffectiveScope(p)=="BRANCH"));
        var assignments=db.OperationalTaskAssignments.AsNoTracking().Where(a=>a.TaskId==episode.SourceId
            &&a.AssignmentStatus=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now);
        if(await assignments.CountAsync(ct)==1&&await assignments.AnyAsync(a=>a.AssigneeId==user,ct))
            return wide||await TaskVerificationStore.ExecutorReadAsync(db,user,episode.BranchId,episode.SourceId,now,ct);
        if(!wide||!await db.UserRoles.AsNoTracking().AnyAsync(r=>r.UserId==user&&r.IsActive&&r.Role.IsActive
            &&r.Role.RoleCode=="DEPT_MANAGER"&&r.AssignedAt<=now&&(r.ExpiresAt==null||r.ExpiresAt>now)
            &&(r.BranchId==null||r.BranchId==episode.BranchId),ct))return false;
        if(!await new InternalRequests.InternalRequestProcessAuthority(db).InstalledAsync(ct))return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.department_management_access m
            JOIN dbo.departments d ON d.department_id=m.department_id AND d.branch_id=m.branch_id
            WHERE m.user_id={user} AND m.branch_id={episode.BranchId} AND m.department_id={task.DepartmentId}
              AND m.granted_at<={now} AND m.revoked_at IS NULL AND d.is_active=1
            """).AnyAsync(ct);
    }
}
