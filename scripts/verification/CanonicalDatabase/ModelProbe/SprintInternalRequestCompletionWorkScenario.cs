using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

internal static class SprintInternalRequestCompletionWorkScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,long actor,long request,List<object> checks,CancellationToken ct)
    {
        void Check(bool value,string name){if(!value)throw new Exception(name);checks.Add(new{completionWorkCheck=name});}
        var denied=false;
        try{await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,request,ct);}catch(InvalidOperationException){denied=true;}
        Check(denied,"inspection cannot run outside closure transaction");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var source=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        var task=new OperationalTask{TaskCode="D3COMP"+Guid.NewGuid().ToString("N")[..12],Title="Synthetic closure prerequisite",
            BranchId=source.BranchId,DepartmentId=source.ReceivingDepartmentId!.Value,SourceType="INTERNAL_REQUEST",SourceId=request,
            Status="IN_PROGRESS",CreatedBy=actor,CreatedAt=DateTime.UtcNow};
        db.OperationalTasks.Add(task);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var blockers=await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,request,ct);
        Check(blockers.Any(b=>b.Kind=="TASK"&&b.Id==task.TaskId&&b.Code=="LEGACY_COMPLETION_REQUIREMENT_UNCLASSIFIED"),"legacy source task cannot disappear from closure prerequisites");
        var audit=new AuditLog{ActorUserId=actor,BranchId=source.BranchId,EntityType="internal_requests",EntityId=request.ToString(),Action="UPDATE",OccurredAt=DateTime.UtcNow};
        db.AuditLogs.Add(audit);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_task_links(internal_request_id,task_id,is_mandatory,audit_id) VALUES({request},{task.TaskId},1,{audit.AuditId})",ct);
        blockers=await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,request,ct);
        Check(blockers.Count(b=>b.Kind=="TASK"&&b.Id==task.TaskId)==1&&blockers.Any(b=>b.Id==task.TaskId&&b.Code=="MANDATORY_WORK_NOT_CLOSED"),"explicit link reconciles legacy provenance but missing workflow still blocks");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({task.TaskId},'IN_PROGRESS')",ct);
        Check((await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,request,ct)).Any(b=>b.Kind=="TASK"&&b.Id==task.TaskId),"in progress mandatory work blocks independent of target read visibility");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET status='COMPLETED',completed_at=SYSUTCDATETIME() WHERE task_id={task.TaskId}",ct);
        Check((await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,request,ct)).Any(b=>b.Kind=="TASK"&&b.Id==task.TaskId),"physical completed alone cannot substitute for canonical closed state");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_request_task_links SET is_mandatory=0 WHERE internal_request_id={request} AND task_id={task.TaskId}",ct);
        Check(!(await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,request,ct)).Any(b=>b.Kind=="TASK"&&b.Id==task.TaskId),"explicit optional work does not block closure");
        var incident=new Incident{IncidentNo="D3COMP"+Guid.NewGuid().ToString("N")[..12],BranchId=source.BranchId,
            IncidentTitle="Synthetic closure incident",Description="Disposable prerequisite fixture",Status="OPEN",ReportedBy=actor,ReportedAt=DateTime.UtcNow,DiscoveredAt=DateTime.UtcNow};
        db.Incidents.Add(incident);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RelatedIncidentId,incident.IncidentId),ct);
        Check((await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,request,ct)).Any(b=>b.Kind=="INCIDENT"&&b.Code=="LEGACY_COMPLETION_REQUIREMENT_UNCLASSIFIED"),"legacy related incident requires explicit completion classification");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_incident_links(internal_request_id,incident_id,is_mandatory,audit_id) VALUES({request},{incident.IncidentId},1,{audit.AuditId})",ct);
        await db.Incidents.Where(i=>i.IncidentId==incident.IncidentId).ExecuteUpdateAsync(s=>s.SetProperty(i=>i.Status,"CLOSED"),ct);
        Check((await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,request,ct)).Any(b=>b.Kind=="INCIDENT"&&b.Code=="MANDATORY_WORK_NOT_CLOSED"),"incident CLOSED without closure timestamp fails closed");
        await db.Incidents.Where(i=>i.IncidentId==incident.IncidentId).ExecuteUpdateAsync(s=>s.SetProperty(i=>i.ClosedAt,DateTime.UtcNow),ct);
        Check(!(await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,request,ct)).Any(b=>b.Kind=="INCIDENT"),"closed classified incident clears known prerequisite");
        var assignment=new InternalRequestAssignment{InternalRequestId=request,AssigneeId=actor,AssignedBy=actor,AssignedAt=DateTime.UtcNow,Status="ACTIVE"};
        var entry=new InternalRequestEvent{InternalRequestId=request,EventType="HANDOVER_SUBMIT",ActorId=actor,OccurredAt=DateTime.UtcNow};
        db.AddRange(assignment,entry);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_handovers(handover_id,request_id,branch_id,department_id,sender_assignment_id,receiver_id,summary,state,created_at) VALUES({entry.EventId},{request},{source.BranchId},{source.ReceivingDepartmentId},{assignment.AssignmentId},{actor},'Synthetic prerequisite','PENDING_ACKNOWLEDGMENT',SYSUTCDATETIME())",ct);
        Check((await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,request,ct)).Any(b=>b.Kind=="HANDOVER"&&b.Id==entry.EventId&&b.Code=="HANDOVER_PENDING"),"pending handover blocks independently of closed linked work");
        var floor=new Floor{BranchId=source.BranchId,FloorCode="COMP",FloorName="Synthetic closure floor",IsActive=true};
        var type=new RoomType{TypeCode="D3COMP"+Guid.NewGuid().ToString("N")[..8],TypeName="Synthetic closure type",IsActive=true,StandardCapacity=1};
        var reference=new ReferenceValue{GroupCode="CLEANING_TYPE",ValueCode="D3COMP"+Guid.NewGuid().ToString("N")[..8],DisplayName="Synthetic closure cleaning",IsActive=true};
        db.AddRange(floor,type,reference);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var room=new Room{BranchId=source.BranchId,FloorId=floor.FloorId,RoomTypeId=type.RoomTypeId,RoomNumber="COMP",OperationalStatus="DIRTY",IsActive=true};
        db.Rooms.Add(room);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var cleaning=new CleaningTask{RoomId=room.RoomId,CleaningTypeId=reference.ReferenceId,TaskNo="D3COMP"+Guid.NewGuid().ToString("N")[..12],Status="PENDING",CreatedBy=actor,CreatedAt=DateTime.UtcNow};
        db.CleaningTasks.Add(cleaning);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='PENDING_ASSIGNMENT' WHERE cleaning_task_id={cleaning.CleaningTaskId}",ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_cleaning_links(internal_request_id,cleaning_task_id,is_mandatory,audit_id) VALUES({request},{cleaning.CleaningTaskId},1,{audit.AuditId})",ct);
        Check((await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,request,ct)).Any(b=>b.Kind=="CLEANING"&&b.Id==cleaning.CleaningTaskId),"mandatory pending cleaning blocks");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET status='COMPLETED',workflow_status='COMPLETED',actual_end_at=SYSUTCDATETIME() WHERE cleaning_task_id={cleaning.CleaningTaskId}",ct);
        Check(!(await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,request,ct)).Any(b=>b.Kind=="CLEANING"&&b.Id==cleaning.CleaningTaskId),"consistent completed cleaning clears known prerequisite");
        await tx.RollbackAsync(ct);db.ChangeTracker.Clear();
        Check(!await db.OperationalTasks.AnyAsync(t=>t.TaskId==task.TaskId,ct)&&!await db.AuditLogs.AnyAsync(a=>a.AuditId==audit.AuditId,ct),"synthetic prerequisite fixture rolled back with audit and links");
        checks.Add(new{completionWorkLimit="Known task/cleaning/incident/handover prerequisites only; no closure endpoint or complete UC120 verdict yet"});
    }
}
