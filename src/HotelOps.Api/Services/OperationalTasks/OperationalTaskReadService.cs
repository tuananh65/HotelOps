using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> ReadOperationalTasksAsync(SessionReference ticket, long sid, long branch,
        OperationalTaskQuery query, long? taskId, CancellationToken ct)
    {
        if (branch <= 0 || taskId <= 0 || query.DepartmentId <= 0 || query.Page < 1 || query.PageSize is < 1 or > 100
            || (long)(query.Page-1)*query.PageSize > int.MaxValue || query.Search?.Length > 150 || query.Status?.Length > 40
            || query.Search?.Any(char.IsControl) == true || query.Status?.Any(char.IsControl) == true
            || query.WorkflowStatus != null && !TaskWorkflowRead.States.Contains(query.WorkflowStatus))
            throw new ValidationException("Invalid task filters.");
        bool? wideScope = null;
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var workflowReady = await ProgressSchema.InstalledAsync(db, token);
            if (query.WorkflowStatus != null && (!workflowReady
                || query.WorkflowStatus is "ASSIGNED" or "ACCEPTED" && !await TaskEntryInstalled(token)
                || query.WorkflowStatus == "PENDING_ASSIGNMENT" && !await TaskDeclineStore.InstalledAsync(db, token)
                || query.WorkflowStatus == "PENDING_VERIFICATION" && !await TaskSubmissionStore.InstalledAsync(db, token)
                || query.WorkflowStatus == "CLOSED" && !await TaskVerificationStore.InstalledAsync(db, token)))
                return new(RoomTypeBatchStatus.SchemaRequired);
            var tasks = db.OperationalTasks.AsNoTracking().Where(t => t.BranchId == branch && t.Department.BranchId == branch
                && (t.RoomId == null || t.Room!.BranchId == branch) && (t.AreaId == null || t.Area!.BranchId == branch));
            if (wideScope != true)
            {
                if(await TaskVerificationStore.InstalledAsync(db,token))
                {
                    var closedOwn=TaskVerificationStore.ClosedTaskIds(db,ticket.UserId);
                    tasks=tasks.Where(t=>t.OperationalTaskAssignments.Count(a=>a.AssignmentStatus=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now)==1
                        &&t.OperationalTaskAssignments.Any(a=>a.AssigneeId==ticket.UserId&&a.AssignmentStatus=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now)
                        ||closedOwn.Contains(t.TaskId));
                }
                else tasks = tasks.Where(t => t.OperationalTaskAssignments.Count(a => a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now) == 1
                    && t.OperationalTaskAssignments.Any(a => a.AssigneeId == ticket.UserId && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now));
            }
            if (taskId is { } id)
            {
                var entity = await tasks.SingleOrDefaultAsync(t => t.TaskId == id, token);
                if (entity == null) return new(RoomTypeBatchStatus.NotFound);
                var history = db.OperationalTaskProgresses.AsNoTracking().Where(p => p.TaskId == id);
                var total = await history.CountAsync(token);
                var progress = await history.OrderByDescending(p => p.ReportedAt).ThenByDescending(p => p.ProgressId)
                    .Skip((query.Page-1)*query.PageSize).Take(query.PageSize)
                    .Select(p => new OperationalTaskProgressRow(p.ProgressId,p.ProgressPercent,p.OldStatus,p.NewStatus,p.Description,p.BlockerReason,p.ReportedBy,p.ReportedAt)).ToArrayAsync(token);
                var state = workflowReady ? await TaskWorkflowRead.Query(db, now).Where(v => v.TaskId == id).Select(v => v.State).SingleOrDefaultAsync(token) : null;
                var priority = await db.ReferenceValues.AsNoTracking().Where(r => r.ReferenceId == entity.PriorityId).Select(r => r.DisplayName).SingleOrDefaultAsync(token);
                return new(RoomTypeBatchStatus.Success, new OperationalTaskDetail(TaskRow(entity) with { WorkflowStatus = state },entity.Description,entity.CategoryId,entity.PriorityId,
                    entity.RoomId,entity.AreaId,entity.CompletionNote,progress.Select(p=>p with {ReportedAt=UtcTaskDate(p.ReportedAt)!.Value}).ToArray(),total,priority));
            }
            if (query.DepartmentId is { } department) tasks=tasks.Where(t=>t.DepartmentId==department);
            if (!string.IsNullOrWhiteSpace(query.Search)) {var term=query.Search.Trim();tasks=tasks.Where(t=>t.Title.Contains(term)||t.TaskCode.Contains(term));}
            if (!string.IsNullOrWhiteSpace(query.Status)) tasks=tasks.Where(t=>t.Status==query.Status);
            if (query.WorkflowStatus != null)
            {
                var matching = TaskWorkflowRead.Query(db, now).Where(v => v.State == query.WorkflowStatus).Select(v => v.TaskId);
                tasks = tasks.Where(t => matching.Contains(t.TaskId));
            }
            var count=await tasks.CountAsync(token);
            var page=await tasks.OrderByDescending(t=>t.CreatedAt).ThenByDescending(t=>t.TaskId)
                .Skip((query.Page-1)*query.PageSize).Take(query.PageSize).ToArrayAsync(token);
            var ids = page.Select(t => t.TaskId).ToArray();
            var states = workflowReady ? await TaskWorkflowRead.Query(db, now).Where(v => ids.Contains(v.TaskId)).ToDictionaryAsync(v => v.TaskId, v => v.State, token) : [];
            return new(RoomTypeBatchStatus.Success,new OperationalTaskPage(page.Select(t => TaskRow(t) with { WorkflowStatus = states.GetValueOrDefault(t.TaskId) }).ToArray(),count,query.Page,query.PageSize));
        },ct,requireRoomTypeSchema:false,featurePermission:async token=>
        {
            var access=await permissions.EvaluateBranchAsync("TASK_VIEW",branch,token);
            if(access.Decision==PermissionDecision.Unavailable) return access;
            var wide=access.IsGranted;
            var own=false;
            if(!wide)
            {
                var data=new EfPermissionDataSource(db);var now=clock.GetUtcNow().UtcDateTime;
                if(await data.IsActiveUserAsync(ticket.UserId,token)&&await data.IsActiveBranchAsync(branch,token))
                    own=(await data.ReadPathsAsync(ticket.UserId,"TASK_VIEW",now,token)).Any(p=>p.PermissionCode=="TASK_VIEW"
                        &&p.BranchId==branch&&PermissionGrantRules.IsEligible(p,now)&&PermissionGrantRules.EffectiveScope(p)=="OWN");
            }
            // Do not return data selected with broader authority if it expires before the final gate.
            if(!wide&&!own || wideScope.HasValue&&wideScope.Value!=wide) return new(PermissionDecision.Denied);
            wideScope=wide;return new(PermissionDecision.Granted);
        });
    }

    private static DateTime? UtcTaskDate(DateTime? value)=>value is {} date?DateTime.SpecifyKind(date,DateTimeKind.Utc):null;
    private static OperationalTaskRow TaskRow(OperationalTask t)=>new(t.TaskId,t.TaskCode,t.BranchId,t.DepartmentId,t.Title,t.Status,
        t.ProgressPercent,UtcTaskDate(t.PlannedStartAt),UtcTaskDate(t.DueAt),UtcTaskDate(t.CompletedAt));
}
