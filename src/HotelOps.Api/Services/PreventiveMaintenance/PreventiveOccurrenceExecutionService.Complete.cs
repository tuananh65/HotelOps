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

public sealed record PreventiveCheckResult(string Key,bool? Passed,string? Note);
public sealed record PreventiveCompletionInput(PreventiveCheckResult[] Results,string? Note);

public sealed partial class PreventiveOccurrenceExecutionService
{
    internal static Task<bool> CompletionInstalledAsync(HotelOpsDbContext db,CancellationToken ct)=>db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveCompletion.V1'
          AND CONVERT(nvarchar(10),value)='1') AND OBJECT_ID('dbo.preventive_occurrence_completions','U') IS NOT NULL
          AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_pm_completion_immutable') AND is_disabled=0)
        THEN 1 ELSE 0 END AS Value
        """).AnyAsync(x=>x==1,ct);

    public async Task<PreventiveOccurrenceView> CompleteAsync(SessionReference ticket,long sid,long branch,long id,string version,
        PreventiveCompletionInput input,CancellationToken ct)
    {
        if(input.Results==null||input.Results.Length>100||input.Note?.Length>4000
            ||input.Results.Any(r=>r==null||string.IsNullOrWhiteSpace(r.Key)||r.Key.Length>64||r.Passed==null||r.Note?.Length>2000)
            ||input.Results.Select(r=>r.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=input.Results.Length)
            throw new InternalRequestReadException(400,"INVALID_CHECK_RESULTS");
        if(db.Database.CurrentTransaction!=null||db.ChangeTracker.HasChanges())throw new InvalidOperationException("Clean occurrence context required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            var time=new SqlWorkTimeTransactionParticipant(db);
            await time.LockUsersAsync([ticket.UserId],ct);
            await reader.ReadAsync(ticket,sid,branch,id,0,ct);
            if(!await CompletionInstalledAsync(db,ct))throw new InternalRequestReadException(503,"PREVENTIVE_COMPLETION_STORAGE_UNAVAILABLE");
            if(!await MaintenanceExecutionAccess.GrantsAsync(db,ticket.UserId,branch,ct))throw new InternalRequestReadException(403,"MAINTENANCE_EXECUTE_REQUIRED");
            await db.Database.SqlQuery<long>($"SELECT run_id AS Value FROM dbo.preventive_occurrence_details WITH(UPDLOCK,HOLDLOCK) WHERE run_id={id} AND branch_id={branch}").ToArrayAsync(ct);
            var before=(await reader.ReadAsync(ticket,sid,branch,id,0,ct)).Single();
            if(before.Version!=version)throw new InternalRequestReadException(412,"OCCURRENCE_CHANGED");
            if(before.AssigneeId!=ticket.UserId)throw new InternalRequestReadException(403,"CURRENT_ASSIGNEE_REQUIRED");
            if(before.Status!="IN_PROGRESS")throw new InternalRequestReadException(409,"RUNNING_OCCURRENCE_REQUIRED");
            if(input.Results.Any(r=>!before.CheckItems.Any(c=>c.Key==r.Key))
                ||before.CheckItems.Where(c=>c.Required).Any(c=>!input.Results.Any(r=>r.Key==c.Key)))
                throw new InternalRequestReadException(400,"MANDATORY_CHECK_RESULTS_REQUIRED");
            var run=await db.PreventiveMaintenanceRuns.FromSqlInterpolated($"SELECT * FROM dbo.preventive_maintenance_runs WITH(UPDLOCK,HOLDLOCK) WHERE run_id={id}")
                .SingleAsync(ct);
            if(run.PerformedAt!=null||run.PerformedBy!=null||run.Result!=null||run.MaintenanceWorkId!=null||run.NextDueAt==null||run.NextDueAt<=run.PlannedAt)
                throw new InternalRequestReadException(409,"OCCURRENCE_EXECUTION_CONFLICT");
            var schedule=await db.PreventiveMaintenanceSchedules.FromSqlInterpolated($"SELECT * FROM dbo.preventive_maintenance_schedules WITH(UPDLOCK,HOLDLOCK) WHERE schedule_id={run.ScheduleId}")
                .SingleAsync(ct);
            var open=await db.WorkIntervals.AsNoTracking().Where(i=>i.UserId==ticket.UserId&&i.ClosedAt==null).Take(2).ToArrayAsync(ct);
            if(open.Length!=1||open[0].WorkItemKind!="OCCURRENCE"||open[0].WorkItemId!=id||open[0].BranchId!=branch)
                throw new InternalRequestReadException(409,"OCCURRENCE_INTERVAL_REQUIRED");
            var closed=await time.CloseIntervalAsync(new(ticket.UserId,ticket.UserId,branch,"OCCURRENCE",id),open[0].IntervalId,open[0].RowVersion,ct);
            var now=closed.ClosedAtUtc??throw new InternalRequestReadException(409,"OCCURRENCE_INTERVAL_REQUIRED");
            run.PerformedAt=now;run.PerformedBy=ticket.UserId;run.Note=input.Note?.Trim();
            // This is a checklist result summary, not a second workflow state.
            run.Result=input.Results.Any(r=>r.Passed==false)?"FAIL":input.Results.Length==0?null:"PASS";
            var oldNext=schedule.NextDueAt;
            if(schedule.NextDueAt<run.NextDueAt.Value)schedule.NextDueAt=run.NextDueAt.Value;
            if(schedule.LastPerformedAt==null||schedule.LastPerformedAt<now)schedule.LastPerformedAt=now;
            var affected=await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.preventive_occurrence_details SET workflow_status='COMPLETED'
                WHERE run_id={id} AND assignee_id={ticket.UserId} AND workflow_status='IN_PROGRESS'
                  AND CONVERT(varchar(16),row_version,2)={version[1..^1]}
                """,ct);
            if(affected!=1)throw new InternalRequestReadException(412,"OCCURRENCE_CHANGED");
            var results=JsonSerializer.Serialize(input.Results);
            var audit=new AuditLog{BranchId=branch,ActorUserId=ticket.UserId,EntityType="preventive_maintenance_runs",
                EntityId=id.ToString(CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
                OldValues=JsonSerializer.Serialize(new{status=before.Status,version=before.Version,scheduleNextDueAt=oldNext}),
                NewValues=JsonSerializer.Serialize(new{eventType="PREVENTIVE_OCCURRENCE_COMPLETED",runId=id,status="COMPLETED",
                    results=input.Results,note=run.Note,intervalId=closed.IntervalId,completedAt=now,scheduleNextDueAt=schedule.NextDueAt})};
            db.AuditLogs.Add(audit);await db.SaveChangesAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.preventive_occurrence_completions(run_id,interval_id,audit_id,actor_id,completed_at,results_json,note,next_due_at)
                VALUES({id},{closed.IntervalId},{audit.AuditId},{ticket.UserId},{now},{results},{run.Note},{run.NextDueAt.Value})
                """,ct);
            if(!await MaintenanceExecutionAccess.GrantsAsync(db,ticket.UserId,branch,ct))throw new InternalRequestReadException(403,"MAINTENANCE_EXECUTE_REQUIRED");
            var after=(await reader.ReadAsync(ticket,sid,branch,id,0,ct)).Single();
            await tx.CommitAsync(ct);return after;
        }
        catch(WorkTimeContractException e)
        {
            throw new InternalRequestReadException(e.Code=="TIME_STALE"?412:e.Code is "TIME_SCOPE_DENIED" or "TIME_ACTOR_INVALID"?403:409,e.Code);
        }
    }
}
