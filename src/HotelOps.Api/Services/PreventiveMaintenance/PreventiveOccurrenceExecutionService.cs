using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.PreventiveMaintenance;

public sealed class PreventiveOccurrenceExecutionService(HotelOpsDbContext db,PreventiveOccurrenceReadService reader)
{
    public async Task<PreventiveOccurrenceView> StartAsync(SessionReference ticket,long sid,long branch,long id,string version,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction!=null||db.ChangeTracker.HasChanges())throw new InvalidOperationException("Clean occurrence context required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            var time=new SqlWorkTimeTransactionParticipant(db);
            await time.LockUsersAsync([ticket.UserId],ct);
            await reader.ReadAsync(ticket,sid,branch,id,0,ct);
            if(!await MaintenanceExecutionAccess.GrantsAsync(db,ticket.UserId,branch,ct))
                throw new InternalRequestReadException(403,"MAINTENANCE_EXECUTE_REQUIRED");
            await db.Database.SqlQuery<long>($"SELECT run_id AS Value FROM dbo.preventive_occurrence_details WITH(UPDLOCK,HOLDLOCK) WHERE run_id={id} AND branch_id={branch}").ToArrayAsync(ct);
            var before=(await reader.ReadAsync(ticket,sid,branch,id,0,ct)).Single();
            if(before.Version!=version)throw new InternalRequestReadException(412,"OCCURRENCE_CHANGED");
            if(before.AssigneeId!=ticket.UserId)throw new InternalRequestReadException(403,"CURRENT_ASSIGNEE_REQUIRED");
            if(before.Status!="ASSIGNED")throw new InternalRequestReadException(409,"ASSIGNED_OCCURRENCE_REQUIRED");
            var run=await db.PreventiveMaintenanceRuns.FromSqlInterpolated($"SELECT * FROM dbo.preventive_maintenance_runs WITH(UPDLOCK,HOLDLOCK) WHERE run_id={id}")
                .AsNoTracking().SingleAsync(ct);
            if(run.PerformedAt!=null||run.PerformedBy!=null||run.Result!=null||run.MaintenanceWorkId!=null
                ||await db.WorkIntervals.AnyAsync(i=>i.WorkItemKind=="OCCURRENCE"&&i.WorkItemId==id,ct))
                throw new InternalRequestReadException(409,"OCCURRENCE_EXECUTION_CONFLICT");
            var affected=await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.preventive_occurrence_details SET workflow_status='IN_PROGRESS'
                WHERE run_id={id} AND branch_id={branch} AND assignee_id={ticket.UserId} AND workflow_status='ASSIGNED'
                  AND CONVERT(varchar(16),row_version,2)={version[1..^1]}
                """,ct);
            if(affected!=1)throw new InternalRequestReadException(412,"OCCURRENCE_CHANGED");
            var interval=await time.OpenIntervalAsync(new(ticket.UserId,ticket.UserId,branch,"OCCURRENCE",id),ct);
            db.AuditLogs.Add(new AuditLog{BranchId=branch,ActorUserId=ticket.UserId,EntityType="preventive_maintenance_runs",
                EntityId=id.ToString(CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=interval.OpenedAtUtc,
                OldValues=JsonSerializer.Serialize(new{status=before.Status,version=before.Version}),
                NewValues=JsonSerializer.Serialize(new{eventType="PREVENTIVE_OCCURRENCE_STARTED",runId=id,status="IN_PROGRESS",
                    intervalId=interval.IntervalId,workdayId=interval.WorkdayId,startedAt=interval.OpenedAtUtc})});
            await db.SaveChangesAsync(ct);
            if(!await MaintenanceExecutionAccess.GrantsAsync(db,ticket.UserId,branch,ct))
                throw new InternalRequestReadException(403,"MAINTENANCE_EXECUTE_REQUIRED");
            var after=(await reader.ReadAsync(ticket,sid,branch,id,0,ct)).Single();
            await tx.CommitAsync(ct);return after;
        }
        catch(WorkTimeContractException e)
        {
            throw new InternalRequestReadException(e.Code=="TIME_STALE"?412:e.Code is "TIME_SCOPE_DENIED" or "TIME_ACTOR_INVALID"?403:409,e.Code);
        }
    }
}
