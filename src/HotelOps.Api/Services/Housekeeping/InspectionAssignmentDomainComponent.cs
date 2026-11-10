using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.HotelStructure;
public sealed partial class RoomTypeCreationService
{
    // Testable domain component, not a notification-complete production writer.
    internal async Task<RoomTypeBatchResult> AssignInspectionComponentInCallerAsync(SessionReference ticket,long sid,long branch,long task,
        AssignInspectionRequest input,long supervisor,SupervisorBindingUsers retained,InspectionAssignmentExpectation expected,CancellationToken ct)
    {
        Fe05CallerTransaction.Require(db);
        if(await db.Database.SqlQuery<int>($"SELECT CASE WHEN DB_NAME() LIKE 'HotelOps[_]Canonical[_]%[_]Fresh' AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE05.AssignmentDomain' AND CONVERT(nvarchar(128),value)='FAST017_COMPONENT_NOT_ACCEPTED') THEN 1 ELSE 0 END AS Value").SingleAsync(ct)!=1)
            throw new InvalidOperationException("UC041_OWNED_COMPONENT_SCHEMA_REQUIRED");
        return await AssignInspectionDomainInCallerAsync(ticket,sid,branch,task,input,supervisor,retained,expected,ct);
    }
    private async Task<RoomTypeBatchResult> AssignInspectionDomainInCallerAsync(SessionReference ticket,long sid,long branch,long task,
        AssignInspectionRequest input,long supervisor,SupervisorBindingUsers retained,InspectionAssignmentExpectation? expected,CancellationToken ct)
    {
        Fe05CallerTransaction.Require(db);retained.Require(db,ticket.UserId,input.InspectorId,supervisor);
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted)return new(RoomTypeBatchStatus.SessionInvalid);
        var valid=await sessions.ValidateSessionAsync(ticket,ct);
        if(!valid.IsValid||valid.Session?.SessionId!=sid||valid.Session.IsRestricted)return new(RoomTypeBatchStatus.SessionInvalid);
        if(!(await permissions.EvaluateBranchAsync("INSPECTION_PERFORM",branch,ct)).IsGranted)return new(RoomTypeBatchStatus.Forbidden);
        if(input.InspectorId<=0||supervisor<=0||task<=0)throw new ValidationException("Explicit inspector and responsible supervisor required.");
        var reason=WorkflowReason(input.Reason);WorkflowDue(input.DueAt,clock.GetUtcNow().UtcDateTime);
        if(!await EligibleInspector(input.InspectorId,branch,ct)||!await EligibleInspector(supervisor,branch,ct))return new(RoomTypeBatchStatus.InvalidReference);
        var read=await ReadInspectionAssignmentSource(branch,task,ct);
        if(read.Status!=RoomTypeBatchStatus.Success)return read;
        var source=(AssignmentSource)read.Value!;
        if(expected!=null&&(expected.TaskRevision!=WorkflowTag(source.TaskRevision)||expected.CycleRevision!=WorkflowTag(source.CycleRevision)||expected.ChecklistVersionId!=source.VersionId))return new(RoomTypeBatchStatus.Stale);
        if(await InspectorCleaningConflict(task,input.InspectorId,ct))return new(RoomTypeBatchStatus.InvalidReference);
        if(await db.RoomInspections.AsNoTracking().AnyAsync(i=>i.CleaningTaskId==task&&i.Status!="COMPLETED"&&i.Status!="CANCELLED",ct))return new(RoomTypeBatchStatus.Duplicate);
        var inspection=new RoomInspection{InspectionNo="INSP-"+Guid.NewGuid().ToString("N"),RoomId=source.RoomId,CleaningTaskId=task,
            InspectorUserId=input.InspectorId,VersionId=source.VersionId,Status="PENDING",CreatedAt=clock.GetUtcNow().UtcDateTime};
        db.Add(inspection);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='ASSIGNED',due_at={input.DueAt} WHERE inspection_id={inspection.InspectionId}",ct);
        var binding=await ReadSupervisorAssignmentSourceAsync(branch,inspection.InspectionId,ct);
        var tag=await BindSupervisorInAssignmentAsync(ticket,sid,branch,inspection.InspectionId,supervisor,binding.RootRevision,binding.CycleRevision,reason,retained,ct);
        await BatchAuditAsync(actor.UserId,branch,"room_inspections",inspection.InspectionId,null,new{reason,inspectorId=input.InspectorId,version=source.VersionId,responsibleSupervisorId=supervisor},["assignedInspector","version","dueAt"],ct);
        // Recheck actor/eligibility before handing control back to the transaction owner.
        if(input.DueAt<=clock.GetUtcNow().UtcDateTime||!(await sessions.ValidateSessionAsync(ticket,ct)).IsValid||!(await permissions.EvaluateBranchAsync("INSPECTION_PERFORM",branch,ct)).IsGranted
            ||!await EligibleInspector(input.InspectorId,branch,ct)||!await EligibleInspector(supervisor,branch,ct))throw new InvalidOperationException("UC041_COMMIT_AUTHORIZATION_CHANGED");
        return new(RoomTypeBatchStatus.Success,new InspectionAssignmentComponentResult(inspection.InspectionId,task,source.RoomId,input.InspectorId,supervisor,source.VersionId,tag,
            WorkflowTag(source.TaskRevision),WorkflowTag(source.CycleRevision),false,false),tag);
    }
    private async Task<bool> InspectorCleaningConflict(long task,long inspector,CancellationToken ct)=>await db.CleaningAssignments.AsNoTracking().AnyAsync(a=>a.CleaningTaskId==task&&a.AssigneeUserId==inspector&&a.AssignmentStatus!="CANCELLED",ct)
        ||await db.CleaningProgressLogs.AsNoTracking().AnyAsync(p=>p.CleaningTaskId==task&&p.LoggedBy==inspector,ct)
        ||await db.CleaningReworks.AsNoTracking().AnyAsync(w=>w.CleaningTaskId==task&&w.AssigneeUserId==inspector,ct);
    private async Task<RoomTypeBatchResult> ReadInspectionAssignmentSource(long branch,long task,CancellationToken ct)
    {
        Fe05CallerTransaction.Require(db);
        if(await db.Database.SqlQuery<string>($"SELECT APPLOCK_MODE('public','HotelOps:RoomTypes:v1','Transaction') AS Value").SingleAsync(ct)!="Exclusive")throw new InvalidOperationException("UC041_EXISTING_CATALOG_LOCK_REQUIRED");
        if(!await HousekeepingSchema.InstalledAsync(db,ct))return new(RoomTypeBatchStatus.SchemaRequired);
        var rows=await db.Database.SqlQuery<AssignmentSource>($"""
            SELECT t.cleaning_task_id AS TaskId,t.room_id AS RoomId,r.room_type_id AS TypeId,t.row_version AS TaskRevision,c.row_version AS CycleRevision,
              t.workflow_status AS State,CAST(0 AS bigint) AS VersionId
            FROM dbo.cleaning_tasks t WITH(UPDLOCK,HOLDLOCK) JOIN dbo.rooms r ON r.room_id=t.room_id
            JOIN dbo.floors f ON f.floor_id=r.floor_id LEFT JOIN dbo.areas a ON a.area_id=r.area_id
            JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=t.room_id AND c.cleaning_task_id=t.cleaning_task_id
            WHERE t.cleaning_task_id={task} AND r.branch_id={branch} AND r.is_active=1 AND f.is_active=1 AND (r.area_id IS NULL OR a.is_active=1)
            """).ToArrayAsync(ct);
        if(rows.Length!=1)return new(RoomTypeBatchStatus.NotFound);
        var source=rows[0];
        // Exact Rework→reinspection assignment/version/reassignment policy is not accepted.
        if(source.State!="PENDING_INSPECTION")return new(RoomTypeBatchStatus.Inactive);
        var checklist=await db.BranchRoomTypes.AsNoTracking().Where(t=>t.RoomTypeId==source.TypeId&&t.IsActive&&(t.BranchId==branch||t.BranchId==null)).Select(t=>t.DefaultChecklistId).SingleOrDefaultAsync(ct);
        if(checklist==null||!await ChecklistEligible(checklist.Value,branch,source.TypeId,ct))return new(RoomTypeBatchStatus.InvalidReference);
        var now=clock.GetUtcNow().UtcDateTime;
        source.VersionId=await db.InspectionChecklistVersions.AsNoTracking().Where(v=>v.ChecklistId==checklist&&v.Status=="PUBLISHED"&&(v.EffectiveAt==null||v.EffectiveAt<=now)&&v.InspectionChecklistItems.Any(c=>c.IsActive)).OrderByDescending(v=>v.VersionNo).Select(v=>v.VersionId).FirstOrDefaultAsync(ct);
        return source.VersionId==0?new(RoomTypeBatchStatus.InvalidReference):new(RoomTypeBatchStatus.Success,source);
    }
    internal Task<RoomTypeBatchResult> ReadInspectionAssignmentOptionsAsync(SessionReference ticket,long sid,long branch,long task,CancellationToken ct)
        =>BatchAsync(ticket,sid,branch,async(_,token)=>{
            var read=await ReadInspectionAssignmentSource(branch,task,token);if(read.Status!=RoomTypeBatchStatus.Success)return read;
            var source=(AssignmentSource)read.Value!;
            var people=await db.Users.AsNoTracking().Where(u=>u.UserBranchAccessUsers.Any(a=>a.BranchId==branch&&a.RevokedAt==null)).OrderBy(u=>u.UserId).Select(u=>new InspectionAssignmentPerson(u.UserId,u.FullName)).ToArrayAsync(token);
            var eligible=new List<InspectionAssignmentPerson>();var supervisors=new List<InspectionAssignmentPerson>();
            foreach(var p in people)if(await EligibleInspector(p.UserId,branch,token)){supervisors.Add(p);if(!await InspectorCleaningConflict(task,p.UserId,token))eligible.Add(p);}
            return new(RoomTypeBatchStatus.Success,new InspectionAssignmentOptions(task,source.RoomId,WorkflowTag(source.TaskRevision),WorkflowTag(source.CycleRevision),source.VersionId,eligible,supervisors,false),WorkflowTag(source.TaskRevision));
        },ct,requireRoomTypeSchema:false,permissionCode:"INSPECTION_PERFORM");
    internal Task<RoomTypeBatchResult> ReadInspectionAssignmentQueueAsync(SessionReference ticket,long sid,long branch,CancellationToken ct)
        =>BatchAsync(ticket,sid,branch,async(_,token)=>{
            // Existing committed UC041 collection contract uses INSPECTION_VIEW; assigned-self uses PERFORM.
            // This compound candidate requires their intersection, never a new broad read grant.
            if(!(await permissions.EvaluateBranchAsync("INSPECTION_VIEW",branch,token)).IsGranted)return new(RoomTypeBatchStatus.Forbidden);
            var pending=await db.Database.SqlQuery<AssignmentQueueRow>($"""
                SELECT t.cleaning_task_id AS TaskId,r.room_id AS RoomId FROM dbo.cleaning_tasks t
                JOIN dbo.rooms r ON r.room_id=t.room_id JOIN dbo.floors f ON f.floor_id=r.floor_id LEFT JOIN dbo.areas a ON a.area_id=r.area_id
                JOIN dbo.room_cleaning_cycle_state c ON c.room_id=r.room_id AND c.cleaning_task_id=t.cleaning_task_id
                WHERE r.branch_id={branch} AND r.is_active=1 AND f.is_active=1 AND (r.area_id IS NULL OR a.is_active=1) AND t.workflow_status='PENDING_INSPECTION'
                  AND NOT EXISTS(SELECT 1 FROM dbo.room_inspections i WHERE i.cleaning_task_id=t.cleaning_task_id AND i.status NOT IN('COMPLETED','CANCELLED'))
                ORDER BY t.cleaning_task_id
                """).ToArrayAsync(token);
            var assigned=await db.Database.SqlQuery<AssignedQueueRow>($"""
                SELECT i.inspection_id AS InspectionId,i.room_id AS RoomId,i.version_id AS VersionId,i.row_version AS Revision
                FROM dbo.room_inspections i JOIN dbo.rooms r ON r.room_id=i.room_id
                JOIN dbo.room_cleaning_cycle_state c ON c.room_id=i.room_id AND c.cleaning_task_id=i.cleaning_task_id
                WHERE r.branch_id={branch} AND r.is_active=1 AND i.inspector_user_id={ticket.UserId}
                  AND i.workflow_status IN('ASSIGNED','INSPECTING','PENDING_CONCLUSION') ORDER BY i.inspection_id
                """).ToArrayAsync(token);
            return new(RoomTypeBatchStatus.Success,new InspectionAssignmentQueue(pending.Select(p=>new InspectionAssignmentTask(p.TaskId,p.RoomId)).ToArray(),assigned.Select(i=>new InspectionAssignedItem(i.InspectionId,i.RoomId,i.VersionId,WorkflowTag(i.Revision))).ToArray()));
        },ct,requireRoomTypeSchema:false,permissionCode:"INSPECTION_PERFORM");
    internal async Task<RoomTypeBatchResult> PreflightInspectionAssignmentAsync(SessionReference ticket,long sid,long branch,long task,string tag,InspectionAssignmentCandidateRequest input,CancellationToken ct)
    {
        var read=await ReadInspectionAssignmentOptionsAsync(ticket,sid,branch,task,ct);if(read.Status!=RoomTypeBatchStatus.Success)return read;
        var s=(InspectionAssignmentOptions)read.Value!;
        if(!input.Confirmed||!s.Inspectors.Any(p=>p.UserId==input.InspectorId)||!s.ResponsibleSupervisors.Any(p=>p.UserId==input.ResponsibleSupervisorId))return new(RoomTypeBatchStatus.InvalidReference);
        WorkflowReason(input.Reason);WorkflowDue(input.DueAt,clock.GetUtcNow().UtcDateTime);
        if(tag!=s.TaskRevision||input.CycleRevision!=s.CycleRevision||input.ChecklistVersionId!=s.ChecklistVersionId)return new(RoomTypeBatchStatus.Stale);
        return new(RoomTypeBatchStatus.Unavailable);
    }
    private sealed class AssignmentQueueRow{public long TaskId{get;set;}public long RoomId{get;set;}}
    private sealed class AssignedQueueRow{public long InspectionId{get;set;}public long RoomId{get;set;}public long VersionId{get;set;}public byte[] Revision{get;set;}=[];}
    private sealed class AssignmentSource{public long TaskId{get;set;}public long RoomId{get;set;}public long TypeId{get;set;}public byte[] TaskRevision{get;set;}=[];public byte[] CycleRevision{get;set;}=[];public string State{get;set;}="";public long VersionId{get;set;}}
}
