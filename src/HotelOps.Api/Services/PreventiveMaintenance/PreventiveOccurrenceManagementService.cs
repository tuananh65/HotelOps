using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.PreventiveMaintenance;

public sealed class PreventiveOccurrenceManagementService(HotelOpsDbContext db,PreventiveOccurrenceReadService reader)
{
    internal static async Task<bool> AllowedAsync(HotelOpsDbContext db,long actor,long branch,string permission,CancellationToken ct)
    {
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var paths=await (from role in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on role.RoleId equals grant.RoleId
            where role.UserId==actor && role.BranchId==branch && role.Role.RoleCode=="MAINTENANCE_LEAD"
                && grant.Permission.PermissionCode==permission && role.User.AccountStatus=="ACTIVE"
                && role.User.EmploymentStatus=="ACTIVE" && role.User.DeletedAt==null
            select new PermissionGrantPath(grant.Permission.PermissionCode,grant.DataScope,role.BranchId,
                role.IsActive,role.Role.IsActive,role.AssignedAt,role.ExpiresAt,role.Branch!=null&&role.Branch.IsActive,
                db.UserBranchAccesses.Any(a=>a.UserId==actor&&a.BranchId==branch&&a.RevokedAt==null&&a.GrantedAt<=now)))
            .ToArrayAsync(ct);
        if(!paths.Any(p=>PermissionGrantRules.IsEligible(p,now)&&PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "GLOBAL"))return false;
        var read=await MaintenanceReadAccess.ReadAsync(db,actor,branch,ct);
        return read.Lead;
    }
    private async Task RequireManagementAsync(long actor,long branch,string permission,CancellationToken ct)
    {
        if(!await AllowedAsync(db,actor,branch,permission,ct))throw new InternalRequestReadException(403,"SCOPED_"+permission+"_REQUIRED");
    }
    public async Task<PreventiveOccurrenceView> ChangeAsync(SessionReference ticket,long sid,long branch,long id,
        string version,long? assignee,string? reason,bool cancel,CancellationToken ct)
    {
        reason=reason?.Trim();
        if(cancel ? reason==null||reason.Length is <10 or >4000 : assignee is null or <=0)
            throw new InternalRequestReadException(400,cancel?"CANCELLATION_REASON_REQUIRED":"ASSIGNEE_REQUIRED");
        if(db.Database.CurrentTransaction!=null||db.ChangeTracker.HasChanges())throw new InvalidOperationException("Clean occurrence context required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync(assignee is {} user?[ticket.UserId,user]:[ticket.UserId],ct);
        // Reader verifies current session, role, branch, read scope and snapshot integrity before any write.
        await reader.ReadAsync(ticket,sid,branch,id,0,ct);
        var permission=cancel?"PM_MANAGE":"MAINTENANCE_ASSIGN";
        await RequireManagementAsync(ticket.UserId,branch,permission,ct);
        await db.Database.SqlQuery<long>($"SELECT run_id AS Value FROM dbo.preventive_occurrence_details WITH(UPDLOCK,HOLDLOCK) WHERE run_id={id} AND branch_id={branch}").ToArrayAsync(ct);
        var before=(await reader.ReadAsync(ticket,sid,branch,id,0,ct)).Single();
        if(before.Version!=version)throw new InternalRequestReadException(412,"OCCURRENCE_CHANGED");
        if(before.Status is not ("PENDING_ASSIGNMENT" or "ASSIGNED"))throw new InternalRequestReadException(409,"UNSTARTED_OCCURRENCE_REQUIRED");
        var run=await db.PreventiveMaintenanceRuns.FromSqlInterpolated($"SELECT * FROM dbo.preventive_maintenance_runs WITH(UPDLOCK,HOLDLOCK) WHERE run_id={id}")
            .AsNoTracking().SingleAsync(ct);
        if(run.PerformedAt!=null||run.PerformedBy!=null||run.Result!=null||run.MaintenanceWorkId!=null)
            throw new InternalRequestReadException(409,"OCCURRENCE_EXECUTION_ALREADY_LINKED");
        if(!cancel)
        {
            if(before.AssigneeId==assignee)throw new InternalRequestReadException(409,"TECHNICIAN_ALREADY_ASSIGNED");
            if(!await MaintenanceExecutionAccess.GrantsAsync(db,assignee!.Value,branch,ct))
                throw new InternalRequestReadException(400,"TECHNICIAN_EXECUTION_AUTHORITY_REQUIRED");
            var read=await MaintenanceReadAccess.ReadAsync(db,assignee.Value,branch,ct);
            if(!read.Lead&&!read.Staff)throw new InternalRequestReadException(400,"TECHNICIAN_READ_AUTHORITY_REQUIRED");
        }
        var targetAssignee=cancel?before.AssigneeId:assignee;
        var status=cancel?"CANCELLED":"ASSIGNED";
        var affected=await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.preventive_occurrence_details SET workflow_status={status},assignee_id={targetAssignee}
            WHERE run_id={id} AND branch_id={branch} AND CONVERT(varchar(16),row_version,2)={version[1..^1]}
              AND workflow_status IN('PENDING_ASSIGNMENT','ASSIGNED')
            """,ct);
        if(affected!=1)throw new InternalRequestReadException(412,"OCCURRENCE_CHANGED");
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        db.AuditLogs.Add(new AuditLog{BranchId=branch,ActorUserId=ticket.UserId,EntityType="preventive_maintenance_runs",
            EntityId=id.ToString(CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
            OldValues=JsonSerializer.Serialize(new{assigneeId=before.AssigneeId,status=before.Status,version=before.Version}),
            NewValues=JsonSerializer.Serialize(new{eventType=cancel?"PREVENTIVE_OCCURRENCE_CANCELLED":"PREVENTIVE_OCCURRENCE_ASSIGNED",
                runId=id,assigneeId=targetAssignee,status,reason,notificationPending=!cancel})});
        await db.SaveChangesAsync(ct);
        await RequireManagementAsync(ticket.UserId,branch,permission,ct);
        var after=(await reader.ReadAsync(ticket,sid,branch,id,0,ct)).Single();
        await tx.CommitAsync(ct);return after;
    }
}
