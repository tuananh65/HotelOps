using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class CleaningAlertRow
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string State { get; set; } = "";
    public DateTime? DueAt { get; set; }
    public DateTime? AllowedFrom { get; set; }
}

internal static class CleaningAlertSource
{
    internal static IQueryable<CleaningAlertRow> Query(HotelOpsDbContext db,bool rework)
        => rework ? db.Database.SqlQuery<CleaningAlertRow>($"""
            SELECT w.rework_id AS Id,r.branch_id AS BranchId,w.workflow_status AS State,w.due_at AS DueAt,
              CONVERT(datetime2(7),NULL) AS AllowedFrom
            FROM dbo.cleaning_reworks w JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=w.cleaning_task_id
            JOIN dbo.rooms r ON r.room_id=t.room_id
            JOIN dbo.room_inspections i ON i.inspection_id=w.inspection_id AND i.cleaning_task_id=t.cleaning_task_id AND i.room_id=r.room_id
            WHERE (w.workflow_status IN('IN_PROGRESS','ON_HOLD','PENDING_REINSPECTION') AND w.status='IN_PROGRESS')
              OR (w.workflow_status='COMPLETED' AND w.status='COMPLETED')
              OR (w.workflow_status='CANCELLED' AND w.status='CANCELLED')
              OR (w.workflow_status IN('PENDING_ACCEPTANCE','ACCEPTED') AND w.status='PENDING')
            """) : db.Database.SqlQuery<CleaningAlertRow>($"""
            SELECT t.cleaning_task_id AS Id,r.branch_id AS BranchId,t.workflow_status AS State,t.due_at AS DueAt,t.scheduled_start_at AS AllowedFrom
            FROM dbo.cleaning_tasks t JOIN dbo.rooms r ON r.room_id=t.room_id
            WHERE (t.workflow_status IN('PENDING_ASSIGNMENT','ASSIGNED') AND t.status='PENDING')
              OR (t.workflow_status IN('IN_PROGRESS','ON_HOLD','PENDING_INSPECTION','REWORK_REQUIRED') AND t.status='IN_PROGRESS')
              OR (t.workflow_status='COMPLETED' AND t.status='COMPLETED')
              OR (t.workflow_status='CANCELLED' AND t.status='CANCELLED')
            """);

    internal static async Task<WorkAlertCondition> ConditionAsync(HotelOpsDbContext db,WorkAlertSourceRow episode,
        DateTime now,decimal threshold,CancellationToken ct)
    {
        var row=await Query(db,episode.SourceType=="REWORK").SingleOrDefaultAsync(r=>r.Id==episode.SourceId&&r.BranchId==episode.BranchId,ct);
        return row==null?WorkAlertCondition.Unknown:WorkAlertWindow.Evaluate(row.State is "COMPLETED" or "CANCELLED",
            row.DueAt,row.AllowedFrom,now,threshold);
    }

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db,long user,WorkAlertSourceRow episode,CancellationToken ct)
    {
        var rework=episode.SourceType=="REWORK";
        if(episode.SourceType is not ("CLEANING" or "REWORK"))return false;
        if(!await Query(db,rework).AnyAsync(t=>t.Id==episode.SourceId&&t.BranchId==episode.BranchId,ct))return false;
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if(!await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==user&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null,ct)
            ||!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a=>a.UserId==user&&a.BranchId==episode.BranchId
                &&a.GrantedAt<=now&&a.RevokedAt==null&&a.Branch.IsActive,ct))return false;
        var data=new EfPermissionDataSource(db);
        var paths=await data.ReadPathsAsync(user,"CLEANING_VIEW",now,ct);
        var wide=paths.Any(p=>PermissionGrantRules.IsEligible(p,now)&&(PermissionGrantRules.EffectiveScope(p)=="GLOBAL"
            ||p.BranchId==episode.BranchId&&PermissionGrantRules.EffectiveScope(p)=="BRANCH"));
        var role=await db.UserRoles.AsNoTracking().AnyAsync(r=>r.UserId==user&&r.IsActive&&r.Role.IsActive
            &&r.Role.RoleCode=="HK_SUPERVISOR"&&r.AssignedAt<=now&&(r.ExpiresAt==null||r.ExpiresAt>now)
            &&(r.BranchId==null||r.BranchId==episode.BranchId),ct);
        if(role&&wide)return true;
        if(rework)return (await ReworkRecipientReadAccess.EvaluateAsync(db,user,episode.BranchId,episode.SourceId,"CLEANING_VIEW",now,ct)).IsGranted;
        var assignments=db.CleaningAssignments.AsNoTracking().Where(a=>a.CleaningTaskId==episode.SourceId
            &&a.AssignmentStatus=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now);
        return await assignments.CountAsync(ct)==1&&await assignments.AnyAsync(a=>a.AssigneeUserId==user,ct)
            &&(wide||paths.Any(p=>PermissionGrantRules.IsEligible(p,now)&&p.BranchId==episode.BranchId&&PermissionGrantRules.EffectiveScope(p)=="OWN"));
    }
}
