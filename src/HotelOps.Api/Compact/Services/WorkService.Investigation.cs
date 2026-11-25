using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class WorkService
{
    public Task<object> Investigate(long id,IncidentInvestigationRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        var issue=await LoadRoomIssue(id,ct);
        Output.Require(!await IsReadOnlyWorkActor("INCIDENT",issue.BranchId,ct),"WORK_ROLE_READ_ONLY",403);
        var supervisor=await access.HasRole("HOUSEKEEPING_SUPERVISOR",issue.BranchId,ct);
        var technical=await access.HasRole("MAINTENANCE_SUPERVISOR",issue.BranchId,ct);
        Output.Require(supervisor||technical,"INCIDENT_INVESTIGATOR_REQUIRED",403);
        // Housekeeping verifies reports through its existing request-handling
        // authority; technical opinions use the maintenance management grant.
        await access.RequireRolePermission(supervisor?"HOUSEKEEPING_SUPERVISOR":"MAINTENANCE_SUPERVISOR",
            supervisor?"IR_PROCESS":"INCIDENT_MANAGE",issue.BranchId,ct);
        await WorkdayAuthority.Require(db,access,actor,clock,issue.BranchId,supervisor?"HOUSEKEEPING_SUPERVISOR":"MAINTENANCE_SUPERVISOR",ct);
        Output.Version(issue.RowVersion,request.RowVersion);Output.Key(request.OperationKey);
        Output.Require(!WorkRules.Terminal.Contains(issue.Status),"WORK_ALREADY_TERMINAL");
        Output.Require(new[]{"UNKNOWN","GUEST","EMPLOYEE","WEAR","TECHNICAL","OTHER"}.Contains(request.Cause),"INVALID_INCIDENT_CAUSE",400);
        Output.Require(!string.IsNullOrWhiteSpace(request.Note)&&request.Note.Trim().Length<=2000,"INVESTIGATION_NOTE_REQUIRED",400);
        if(request.RelatedUserId!=null){
            Output.Require(request.Cause=="EMPLOYEE","RELATED_PERSON_REQUIRES_EMPLOYEE_CAUSE",400);
            await access.Eligible(request.RelatedUserId.Value,issue.BranchId,ct);
        }
        Output.Require(!await db.WorkEvents.AnyAsync(e=>e.WorkId==id&&e.OperationKey==request.OperationKey&&e.ActorUserId==actor.Id,ct),"OPERATION_ALREADY_APPLIED");
        Event(issue,supervisor?"INCIDENT_CAUSE_VERIFIED":"INCIDENT_TECHNICAL_OPINION",request.OperationKey,request.Note.Trim(),issue.Status);
        db.WorkEvents.Local.Single(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey).SnapshotJson=JsonSerializer.Serialize(new{request.Cause,request.RelatedUserId});
        db.Entry(issue).Property(x=>x.Title).IsModified=true;
        await db.SaveChangesAsync(ct);return (object)Output.Record(issue);
    },ct);

    private async Task<object> Investigation(long id,CancellationToken ct)
    {
        var rows=await db.WorkEvents.AsNoTracking().Where(e=>e.WorkId==id&&(e.EventType=="INCIDENT_CAUSE_VERIFIED"||e.EventType=="INCIDENT_TECHNICAL_OPINION"))
            .OrderByDescending(e=>e.WorkEventId).Take(100).Select(e=>new{e.WorkEventId,e.EventType,e.ActorUserId,e.OccurredAt,e.Reason,e.SnapshotJson}).ToArrayAsync(ct);
        return rows.Select(e=>new{e.WorkEventId,e.EventType,e.ActorUserId,e.OccurredAt,e.Reason,details=JsonSerializer.Deserialize<JsonElement>(e.SnapshotJson??"{}")}).ToArray();
    }
}
