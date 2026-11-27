using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    private async Task<bool> EligibleMaintenanceAssignee(long userId,long branchId,CancellationToken ct)=>
        await (await CurrentMaintenanceShift(branchId,ct)).AnyAsync(a=>a.UserId==userId,ct)
        &&await (from ur in db.UserRoles.AsNoTracking() join role in db.Roles on ur.RoleId equals role.RoleId
        where ur.UserId==userId&&ur.BranchId==branchId&&ur.RevokedAt==null&&ur.AssignedAt<=Now&&(ur.ExpiresAt==null||ur.ExpiresAt>Now)&&role.IsActive
            &&role.Code=="MAINTENANCE_STAFF"
            &&!db.UserRoles.Any(owner=>owner.UserId==userId&&owner.RevokedAt==null&&owner.AssignedAt<=Now
                &&(owner.ExpiresAt==null||owner.ExpiresAt>Now)&&(owner.BranchId==null||owner.BranchId==branchId)
                &&db.Roles.Any(r=>r.RoleId==owner.RoleId&&r.IsActive&&(r.Code=="OWNER"||r.Code=="MAINTENANCE_SUPERVISOR")))
            select ur.UserRoleId).AnyAsync(ct);

    private async Task SnapshotExecutionChecklist(WorkItem work,long templateId,CancellationToken ct)
    {
        var kind=work.Kind=="REWORK"?"CLEANING":work.Kind;
        var template=await db.ChecklistTemplates.AsNoTracking().SingleOrDefaultAsync(t=>t.ChecklistTemplateId==templateId&&t.BranchId==work.BranchId&&t.Kind==kind&&t.IsActive&&t.PublishedAt!=null,ct);
        Output.Require(template!=null,"PUBLISHED_EXECUTION_CHECKLIST_REQUIRED",400);
        var items=await db.ChecklistItems.AsNoTracking().Where(x=>x.ChecklistTemplateId==templateId).ToArrayAsync(ct);
        Output.Require(items.Any(x=>x.IsRequired),"REQUIRED_CHECKLIST_ITEMS_MISSING",400);
        work.ChecklistTemplateId=templateId;
        // The database composite FK includes the work's template ID. Scalar-only
        // Database First mappings do not order this UPDATE before item INSERTs.
        // Persist the parent first inside the caller's existing transaction.
        await db.SaveChangesAsync(ct);
        foreach(var item in items)db.WorkChecklistItems.Add(new WorkChecklistItem{BranchId=work.BranchId,WorkId=work.WorkId,ChecklistTemplateId=templateId,
            ChecklistItemId=item.ChecklistItemId,DescriptionSnapshot=item.Description,RequiredSnapshot=item.IsRequired,WeightSnapshot=item.Weight});
    }

    public async Task<object> ExecutionChecklistOptions(long id,CancellationToken ct)
    {
        var work=await Load(id,"read",ct);var kind=work.Kind=="REWORK"?"CLEANING":work.Kind;
        Output.Require(work.Kind=="MAINTENANCE","EXECUTION_CHECKLIST_KIND_REQUIRED",400);
        return await db.ChecklistTemplates.AsNoTracking().Where(t=>t.BranchId==work.BranchId&&t.Kind==kind&&t.IsActive&&t.PublishedAt!=null
            &&db.ChecklistItems.Any(i=>i.ChecklistTemplateId==t.ChecklistTemplateId&&i.IsRequired))
            .OrderByDescending(t=>t.ChecklistTemplateId).Select(t=>new{t.ChecklistTemplateId,t.Name,t.VersionNo}).ToArrayAsync(ct);
    }

    public Task<object> AttachExecutionChecklist(long id,AttachChecklistRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        var work=await Load(id,"execute",ct);Output.Version(work.RowVersion,request.RowVersion);
        Output.Require(work.Kind=="MAINTENANCE","EXECUTION_CHECKLIST_KIND_REQUIRED",400);
        Output.Require(work.Status is "ASSIGNED" or "PENDING_ACCEPTANCE" or "ACCEPTED" or "PENDING" or "IN_PROGRESS","WORK_NOT_EXECUTING");
        Output.Require(await db.WorkAssignments.AnyAsync(a=>a.WorkId==id&&a.AssignmentStatus=="ACTIVE"&&a.AssigneeUserId==actor.Id,ct),"CURRENT_ASSIGNEE_REQUIRED",403);
        Output.Require(work.ChecklistTemplateId==null&&!await db.WorkChecklistItems.AnyAsync(i=>i.WorkId==id,ct),"CHECKLIST_SNAPSHOT_IMMUTABLE");
        await SnapshotExecutionChecklist(work,request.ChecklistTemplateId,ct);
        audit.Add("WorkItem",id,"EXECUTION_CHECKLIST_ATTACHED",work.BranchId,after:new{request.ChecklistTemplateId});
        await db.SaveChangesAsync(ct);return (object)Output.Record(work);
    },ct);
}

public static class ExecutionChecklistPolicy
{
    // Execution reports use photos; legacy checklist snapshots remain available as history.
    public static bool RequiresSnapshot(string kind,bool hasTemplate)=>
        false;
}
