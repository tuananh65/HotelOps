using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
public sealed class TemplateService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,TimeProvider clock)
{
    public async Task<object> ChecklistDetail(long id,CancellationToken ct){
        var template=await db.ChecklistTemplates.AsNoTracking().SingleOrDefaultAsync(t=>t.ChecklistTemplateId==id,ct)??throw new ApiException(404,"CHECKLIST_NOT_FOUND");
        await access.Require("CHECKLIST_MANAGE",template.BranchId,ct:ct);
        return new{template=Output.Record(template),items=(await db.ChecklistItems.AsNoTracking().Where(i=>i.ChecklistTemplateId==id).OrderBy(i=>i.LineNo).ToArrayAsync(ct)).Select(Output.Record)};
    }
    private async Task Validate(TemplateRequest r,CancellationToken ct){
        Output.Require(r.Kind is "MAINTENANCE" or "OPERATIONAL","SCHEDULE_KIND_UNSUPPORTED",400);
        Output.Require(!string.IsNullOrWhiteSpace(r.Code)&&!string.IsNullOrWhiteSpace(r.Title),"SCHEDULE_CODE_TITLE_REQUIRED",400);
        Output.Require(r.Frequency is "DAILY" or "WEEKLY" or "MONTHLY"&&r.FrequencyInterval is >0 and <=365&&r.NextDueAt!=null,"INVALID_SCHEDULE",400);
        if(r.AssetId!=null)Output.Require(await db.Assets.AnyAsync(a=>a.AssetId==r.AssetId&&a.BranchId==r.BranchId&&a.IsActive,ct),"ASSET_UNAVAILABLE");
        if(r.RoomId!=null)Output.Require(await db.Rooms.AnyAsync(a=>a.RoomId==r.RoomId&&a.BranchId==r.BranchId&&a.IsActive,ct),"ROOM_UNAVAILABLE");
        if(r.DepartmentId!=null)Output.Require(await db.Departments.AnyAsync(a=>a.DepartmentId==r.DepartmentId&&a.BranchId==r.BranchId&&a.IsActive,ct),"TARGET_DEPARTMENT_REQUIRED");
        if(r.ChecklistTemplateId!=null)Output.Require(await db.ChecklistTemplates.AnyAsync(t=>t.ChecklistTemplateId==r.ChecklistTemplateId&&t.BranchId==r.BranchId&&t.Kind==r.Kind&&t.IsActive&&t.PublishedAt!=null,ct),"CHECKLIST_KIND_MISMATCH");
    }
    public async Task<object> Detail(long id,CancellationToken ct){
        var t=await db.WorkTemplates.AsNoTracking().SingleOrDefaultAsync(t=>t.WorkTemplateId==id,ct)??throw new ApiException(404,"TEMPLATE_NOT_FOUND");
        await access.Require(t.Kind=="MAINTENANCE"?"PM_MANAGE":"TASK_MANAGE",t.BranchId,ct:ct);
        return new{template=Output.Record(t),occurrences=await db.WorkItems.AsNoTracking().Where(w=>w.WorkTemplateId==id).OrderByDescending(w=>w.OccurrenceAt).Take(100)
            .Select(w=>new{w.WorkId,w.Kind,w.Title,w.Status,w.OccurrenceAt,w.DueAt,w.CompletedAt,w.VerifiedAt}).ToArrayAsync(ct)};
    }
    public async Task<object> List(long branchId,CancellationToken ct){
        var checklists=await access.Allows("CHECKLIST_MANAGE",branchId,ct:ct);
        var maintenance=await access.Allows("PM_MANAGE",branchId,ct:ct);
        var tasks=await access.Allows("TASK_MANAGE",branchId,ct:ct);
        Output.Require(checklists||maintenance||tasks,"PERMISSION_DENIED",403);
        return new{
        checklists=(await db.ChecklistTemplates.AsNoTracking().Where(t=>checklists&&t.BranchId==branchId).ToArrayAsync(ct)).Select(Output.Record),
        schedules=(await db.WorkTemplates.AsNoTracking().Where(t=>t.BranchId==branchId&&((maintenance&&t.Kind=="MAINTENANCE")||(tasks&&t.Kind=="OPERATIONAL"))).ToArrayAsync(ct)).Select(Output.Record)};}
    public Task<object> Checklist(ChecklistCreateRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        if(string.IsNullOrWhiteSpace(r.Code))r=r with{Code=$"CHK-{Guid.NewGuid():N}"[..20]};
        await access.Require("CHECKLIST_MANAGE",r.BranchId,ct:ct);Output.Require(r.Items.Count is >0 and <=200&&r.Items.All(i=>!string.IsNullOrWhiteSpace(i.Description)&&i.Weight>=0),"CHECKLIST_ITEMS_REQUIRED",400);
        Output.Require(!string.IsNullOrWhiteSpace(r.Code)&&!string.IsNullOrWhiteSpace(r.Name),"CHECKLIST_CODE_NAME_REQUIRED",400);
        Output.Require(!await db.ChecklistTemplates.AnyAsync(t=>t.BranchId==r.BranchId&&t.Code==r.Code&&t.Kind!=r.Kind,ct),"CHECKLIST_KIND_MISMATCH");
        var version=(await db.ChecklistTemplates.Where(t=>t.BranchId==r.BranchId&&t.Code==r.Code).MaxAsync(t=>(int?)t.VersionNo,ct)??0)+1;
        var t=new ChecklistTemplate{BranchId=r.BranchId,Code=r.Code,Name=r.Name,Kind=r.Kind,VersionNo=version,CreatedBy=actor.Id,IsActive=true};db.ChecklistTemplates.Add(t);await db.SaveChangesAsync(ct);
        db.ChecklistItems.AddRange(r.Items.Select((i,n)=>new ChecklistItem{ChecklistTemplateId=t.ChecklistTemplateId,LineNo=n+1,Description=i.Description,IsRequired=i.IsRequired,Weight=i.Weight}));await db.SaveChangesAsync(ct);
        t.PublishedAt=clock.GetUtcNow().UtcDateTime;await db.SaveChangesAsync(ct);audit.Add("ChecklistTemplate",t.ChecklistTemplateId,"PUBLISH",r.BranchId);return (object)Output.Record(t);
    },ct);
    public Task<object> Schedule(TemplateRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        if(string.IsNullOrWhiteSpace(r.Code))r=r with{Code=$"TPL-{Guid.NewGuid():N}"[..20]};
        Output.Require(r.Kind is "MAINTENANCE" or "OPERATIONAL","SCHEDULE_KIND_UNSUPPORTED",400);
        await access.Require(r.Kind=="MAINTENANCE"?"PM_MANAGE":"TASK_MANAGE",r.BranchId,ct:ct);
        await Validate(r,ct);
        var t=new WorkTemplate{BranchId=r.BranchId,Code=r.Code,Kind=r.Kind,Title=r.Title,DepartmentId=r.DepartmentId,RoomId=r.RoomId,AssetId=r.AssetId,ChecklistTemplateId=r.ChecklistTemplateId,
            Frequency=r.Frequency,FrequencyInterval=r.FrequencyInterval,NextDueAt=r.NextDueAt,CreatedBy=actor.Id,IsActive=true};
        db.WorkTemplates.Add(t);await db.SaveChangesAsync(ct);audit.Add("WorkTemplate",t.WorkTemplateId,"CREATE",r.BranchId);return (object)Output.Record(t);
    },ct);
    public Task<object> Update(long id,UpdateTemplateRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var t=await db.WorkTemplates.SingleOrDefaultAsync(t=>t.WorkTemplateId==id,ct)??throw new ApiException(404,"TEMPLATE_NOT_FOUND");
        await access.Require(t.Kind=="MAINTENANCE"?"PM_MANAGE":"TASK_MANAGE",t.BranchId,ct:ct);Output.Version(t.RowVersion,r.RowVersion);
        var p=r.Plan;Output.Require(p.BranchId==t.BranchId&&p.Kind==t.Kind&&p.Code==t.Code,"TEMPLATE_IDENTITY_IMMUTABLE",400);
        Output.Require(t.IsActive,"TEMPLATE_INACTIVE");await Validate(p,ct);
        Output.Require(!await db.WorkItems.AnyAsync(w=>w.WorkTemplateId==id&&w.OccurrenceAt==p.NextDueAt,ct),"OCCURRENCE_ALREADY_GENERATED");
        t.Title=p.Title;t.AssetId=p.AssetId;t.RoomId=p.RoomId;t.DepartmentId=p.DepartmentId;t.ChecklistTemplateId=p.ChecklistTemplateId;t.Frequency=p.Frequency;t.FrequencyInterval=p.FrequencyInterval;t.NextDueAt=p.NextDueAt;
        audit.Add("WorkTemplate",id,"UPDATE",t.BranchId,after:p);await db.SaveChangesAsync(ct);return (object)Output.Record(t);
    },ct);
    public Task<object> Disable(long id,VersionRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var t=await db.WorkTemplates.FindAsync([id],ct)??throw new ApiException(404,"TEMPLATE_NOT_FOUND");await access.Require(t.Kind=="MAINTENANCE"?"PM_MANAGE":"TASK_MANAGE",t.BranchId,ct:ct);
        Output.Version(t.RowVersion,r.RowVersion);t.IsActive=false;audit.Add("WorkTemplate",id,"DISABLE",t.BranchId);await db.SaveChangesAsync(ct);return (object)Output.Record(t);
    },ct);
}
