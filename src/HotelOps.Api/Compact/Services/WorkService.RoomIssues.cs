using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class WorkService
{
    private sealed record IssueData(string Kind,long? ItemId,string ObjectName,decimal Quantity=1,string Impact="READINESS",long? CleaningWorkId=null,long? DetectedByUserId=null);
    private Task<bool> IsRoomIssue(long id,CancellationToken ct)=>db.WorkEvents.AnyAsync(e=>e.WorkId==id&&(e.EventType=="ROOM_ISSUE_REPORTED"||e.EventType=="LEGACY_ROOM_ISSUE_RECOVERED"),ct);
    private async Task<IssueData> IssueInfo(long id,CancellationToken ct) => JsonSerializer.Deserialize<IssueData>(
        await db.WorkEvents.Where(e=>e.WorkId==id&&(e.EventType=="ROOM_ISSUE_REPORTED"||e.EventType=="LEGACY_ROOM_ISSUE_RECOVERED")).Select(e=>e.SnapshotJson).SingleAsync(ct)
        ?? throw new ApiException(409,"ROOM_ISSUE_DATA_MISSING"))!;
    private Task<string?> IssueStage(long id,CancellationToken ct)=>db.WorkEvents.Where(e=>e.WorkId==id&&e.EventType.StartsWith("ROOM_ISSUE_"))
        .OrderByDescending(e=>e.WorkEventId).Select(e=>e.EventType).FirstOrDefaultAsync(ct);

    public Task<object> CreateRoomIssue(RoomIssueCreateRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.Require("IR_CREATE",request.BranchId,true,ct);
        Output.Require(!await access.HasRole("OPERATION_MANAGER",request.BranchId,ct),"OM_REQUEST_READ_ONLY",403);
        var housekeeper=await access.HasRole("HK_STAFF",request.BranchId,ct);
        var receptionist=await access.HasRole("RECEPTION",request.BranchId,ct);
        if(receptionist)Output.Require(await RoomService.ReceptionOnShift(db,actor.Id,clock,request.BranchId,ct),"RECEPTION_NOT_ON_SHIFT",403);
        Output.Require(housekeeper||receptionist||await access.HasRole("HOUSEKEEPING_SUPERVISOR",request.BranchId,ct),"ROOM_REPORTER_REQUIRED",403);
        Output.Key(request.OperationKey);
        var detectedBy=request.DetectedByUserId??actor.Id;
        await access.Eligible(detectedBy,request.BranchId,ct);
        Output.Require(!await db.WorkEvents.AnyAsync(e=>e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey,ct),"OPERATION_ALREADY_APPLIED");
        var room=await db.Rooms.SingleOrDefaultAsync(r=>r.RoomId==request.RoomId&&r.BranchId==request.BranchId&&r.IsActive,ct)??throw new ApiException(404,"ROOM_NOT_FOUND");
        Output.Version(room.RowVersion,request.RoomRowVersion);
        if(!receptionist){
            var context=await db.WorkItems.SingleOrDefaultAsync(w=>w.WorkId==request.CleaningWorkId&&w.BranchId==room.BranchId&&w.RoomId==room.RoomId,ct);
            Output.Require(context!=null&&await CanReportDuringHousekeeping(context,ct),"ROOM_REPORT_EXECUTION_REQUIRED",403);
        }
        Output.Require(request.Quantity>0&&SupplyMath.ValidQuantity(request.Quantity)&&request.Impact is "READINESS" or "SAFETY","ROOM_ISSUE_DETAILS_REQUIRED",400);
        if(housekeeper){
            if(request.CleaningWorkId!=null)await RequireExistingCleaningExecution(request.CleaningWorkId.Value,request.BranchId,ct);
            Output.Require(request.CleaningWorkId!=null&&await db.WorkItems.AnyAsync(w=>w.WorkId==request.CleaningWorkId&&w.BranchId==room.BranchId&&w.RoomId==room.RoomId&&w.TurnoverNo==room.TurnoverNo
                &&(w.Kind=="CLEANING"||w.Kind=="REWORK")&&db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE"),ct),"ROOM_REPORT_REQUIRES_ASSIGNED_WORK",403);
        }
        var type=await db.ReferenceValues.AsNoTracking().SingleOrDefaultAsync(r=>r.ReferenceId==request.TypeId&&r.Kind=="WORK_CATEGORY"&&r.IsActive,ct);
        Output.Require(type!=null&&IsRoomIssueType(type.Code,room.BranchId),"ROOM_ISSUE_TYPE_INVALID",400);
        Output.Require(IssueImpacts(type!.SettingsJson).Any(i=>i.Code==request.Impact&&i.IsActive),"ROOM_ISSUE_IMPACT_UNAVAILABLE",400);
        // Reporting identifies the room and object; notes are optional even for
        // older type configurations that previously required them.
        Output.Require(!receptionist||type!.Code.EndsWith("_BROKEN"),"RECEPTION_DAMAGE_REPORT_ONLY",403);
        Output.Require((request.AssetId!=null) != (request.ItemId!=null),"ROOM_ISSUE_OBJECT_REQUIRED",400);
        await StaffSupplyService.Lock(db,ct);
        string? objectName=null;
        if(request.AssetId!=null)objectName=await db.Assets.Where(a=>a.AssetId==request.AssetId&&a.RoomId==room.RoomId&&a.BranchId==room.BranchId&&a.IsActive&&a.RetiredAt==null).Select(a=>a.Name).SingleOrDefaultAsync(ct);
        else objectName=(await RoomIssuedItems(room.BranchId,room.RoomId,ct)).FirstOrDefault(i=>i.ItemId==request.ItemId)?.Name;
        var assessedConsumable=type!.Code.EndsWith("_MISSING")&&request.ItemId!=null&&
            await db.ItemLifecycleProfiles.AnyAsync(p=>p.BranchId==room.BranchId&&p.ItemId==request.ItemId&&p.Classification=="CONSUMABLE",ct);
        if(assessedConsumable){
            var outstanding=await RoomIssueQuantityRules.AssessedOutstanding(db,room.BranchId,room.RoomId,request.CleaningWorkId,request.ItemId!.Value,ct);
            var reported=await RoomIssueQuantityRules.Reports(db,room.BranchId,room.RoomId,request.CleaningWorkId,request.ItemId.Value,ct);
            RoomIssueQuantityRules.RequireWithin(request.Quantity,outstanding-reported.Sum(r=>r.Data.Quantity),"ROOM_ISSUE_EXCEEDS_ASSESSED_MISSING");
        }
        else if(type.Code.EndsWith("_MISSING")&&request.ItemId!=null){
            var recorded=await RoomIssueQuantityRules.RecordedRoomQuantity(db,room.BranchId,room.RoomId,request.ItemId.Value,ct);
            var reported=await RoomIssueQuantityRules.Reports(db,room.BranchId,room.RoomId,request.CleaningWorkId,request.ItemId.Value,ct);
            RoomIssueQuantityRules.RequireWithin(request.Quantity,recorded-reported.Sum(r=>r.Data.Quantity),"ROOM_ISSUE_EXCEEDS_RECORDED_ROOM_QUANTITY");
        }
        if(assessedConsumable)objectName=await db.InventoryItems.Where(i=>i.ItemId==request.ItemId).Select(i=>i.Name).SingleAsync(ct);
        Output.Require(objectName!=null,"ROOM_ISSUE_OBJECT_NOT_IN_ROOM",409);
        var group=type!.Code.EndsWith("_BROKEN")?"C":"B";
        Output.Require(assessedConsumable||await db.SupplyConfigurations.AnyAsync(c=>c.BranchId==room.BranchId&&c.IsCurrent&&c.IsActive&&c.SupplyGroup==group
            &&c.ItemId==request.ItemId&&c.AssetId==request.AssetId,ct),"ROOM_OBJECT_CLASSIFICATION_REQUIRED",409);
        var work=new WorkItem{BranchId=room.BranchId,RoomId=room.RoomId,TurnoverNo=room.TurnoverNo,Kind="INCIDENT",Status="OPEN",WorkNo=Guid.NewGuid().ToString("N"),
            Title=$"{type!.Name} · {objectName} · Phòng {room.Code}",Description=request.Note,AssetId=request.AssetId,CategoryId=type.ReferenceId,CreatedBy=actor.Id,CreatedAt=Now};
        Output.Require(work.Title.Length<=250,"TITLE_TOO_LONG",400);
        db.WorkItems.Add(work);await db.SaveChangesAsync(ct);
        if(request.CleaningWorkId!=null)db.WorkLinks.Add(new WorkLink{BranchId=work.BranchId,SourceWorkId=work.WorkId,TargetWorkId=request.CleaningWorkId.Value,Relation="SOURCE",CreatedBy=actor.Id,CreatedAt=Now});
        Event(work,"ROOM_ISSUE_REPORTED",request.OperationKey,request.Note,null);
        db.WorkEvents.Local.Single(e=>e.WorkId==work.WorkId&&e.EventType=="ROOM_ISSUE_REPORTED").SnapshotJson=JsonSerializer.Serialize(new IssueData(type.Code.EndsWith("_BROKEN")?"BROKEN":"MISSING",request.ItemId,objectName!,request.Quantity,request.Impact,request.CleaningWorkId,detectedBy));
        // The report blocks release until independent inspection, without falsifying the current cleaning result.
        db.RoomBlocks.Add(new RoomBlock{RoomId=room.RoomId,BranchId=room.BranchId,SourceWorkId=work.WorkId,Reason=work.Title,BlockedBy=actor.Id,BlockedAt=Now});
        db.Entry(room).Property(r=>r.Code).IsModified=true;
        await NotifyIssueRole(work,"HOUSEKEEPING_SUPERVISOR","ROOM_ISSUE_REPORTED",request.OperationKey,ct);
        await db.SaveChangesAsync(ct);
        await requests.RecordDomainTransition(work,group=="C"?"ROOM_EQUIPMENT_DAMAGE":"ROOM_DURABLE_ITEM_MISSING","PENDING_SUPERVISOR","HOUSEKEEPING_SUPERVISOR","report",housekeeper?"HK_STAFF":receptionist?"RECEPTION":"HOUSEKEEPING_SUPERVISOR",request.OperationKey,request.Note,ct);
        return (object)Output.Record(work);
    },ct);

    private async Task<WorkItem> LoadRoomIssue(long id,CancellationToken ct)
    {
        var work=await db.WorkItems.SingleOrDefaultAsync(w=>w.WorkId==id&&w.Kind=="INCIDENT",ct)??throw new ApiException(404,"WORK_NOT_FOUND");
        Output.Require(await IsRoomIssue(id,ct),"ROOM_ISSUE_REQUIRED",404);
        if(await access.HasRole("STOREKEEPER",work.BranchId,ct)&&await access.Allows("MR_FULFILL",work.BranchId,ct:ct))
            Output.Require(await db.WorkEvents.AnyAsync(e=>e.WorkId==id&&e.EventType=="ROOM_ISSUE_MATERIAL_REQUESTED",ct),"ROOM_ISSUE_NOT_ROUTED",403);
        else if(await access.HasRole("HK_STAFF",work.BranchId,ct)||await access.HasRole("RECEPTION",work.BranchId,ct)){
            await access.Require("IR_VIEW",work.BranchId,true,ct);Output.Require(work.CreatedBy==actor.Id,"REQUEST_VISIBILITY_DENIED",403);
        }else await access.Require("INCIDENT_VIEW",work.BranchId,work.CreatedBy==actor.Id,ct);
        return work;
    }
    public async Task<object> RoomIssues(long branch,CancellationToken ct,int? page=null,string? search=null)
    {
        Output.Require(page==null||page is >=1 and <=1000000,"PAGE_INVALID",400);
        var store=await access.HasRole("STOREKEEPER",branch,ct)&&await access.Allows("MR_FULFILL",branch,ct:ct);
        var personal=await access.HasRole("HK_STAFF",branch,ct)||await access.HasRole("RECEPTION",branch,ct);
        if(personal)await access.Require("IR_VIEW",branch,true,ct);
        else if(!store)await access.Require("INCIDENT_VIEW",branch,ct:ct);
        var query=db.WorkItems.AsNoTracking().Where(w=>w.BranchId==branch&&w.Kind=="INCIDENT"
            &&w.RoomId!=null&&(!personal||w.CreatedBy==actor.Id)
            &&(!store||db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&e.EventType=="ROOM_ISSUE_MATERIAL_REQUESTED")));
        if(!string.IsNullOrWhiteSpace(search)){
            var term=search.Trim();
            query=query.Where(w=>w.Title.Contains(term)||db.Rooms.Any(r=>r.RoomId==w.RoomId&&r.Code.Contains(term)));
        }
        var total=await query.CountAsync(ct);
        query=query.OrderByDescending(w=>w.WorkId);
        if(page!=null)query=query.Skip((page.Value-1)*25).Take(25);
        var rows=await query.Select(w=>new{w.WorkId,w.Title,w.Status,w.RoomId,
                legacy=!db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&(e.EventType=="ROOM_ISSUE_REPORTED"||e.EventType=="LEGACY_ROOM_ISSUE_RECOVERED")),
                stage=db.WorkEvents.Where(e=>e.WorkId==w.WorkId&&e.EventType.StartsWith("ROOM_ISSUE_")).OrderByDescending(e=>e.WorkEventId).Select(e=>e.EventType).FirstOrDefault()}).ToArrayAsync(ct);
        return page==null?rows:new{items=rows,total,page=page.Value,pageSize=25};
    }
    public async Task<object> RoomIssueDetail(long id,CancellationToken ct)
    {
        // Pre-workflow incidents remain actionable through their original, permission-checked
        // work lifecycle. Reading them must never manufacture a reporting event or close a block.
        if(!await IsRoomIssue(id,ct)){
            var legacy=await Load(id,"read",ct);
            Output.Require(legacy.Kind=="INCIDENT"&&legacy.RoomId!=null,"ROOM_ISSUE_REQUIRED",404);
            var legacyRoom=await db.Rooms.AsNoTracking().SingleAsync(r=>r.RoomId==legacy.RoomId&&r.BranchId==legacy.BranchId,ct);
            return new{legacy=true,work=Output.Record(legacy),room=new{legacyRoom.Code,rowVersion=Convert.ToBase64String(legacyRoom.RowVersion)},
                canRecover=legacy.Status=="OPEN"&&await access.HasRole("HOUSEKEEPING_SUPERVISOR",legacy.BranchId,ct)&&await access.Allows("IR_PROCESS",legacy.BranchId,ct:ct)
                    &&!await access.HasRole("OPERATION_MANAGER",legacy.BranchId,ct)&&!await access.HasRole("OWNER",legacy.BranchId,ct)
                    &&!await db.RequestWorkflows.AnyAsync(w=>w.WorkId==id,ct)
                    &&!await db.WorkItems.AnyAsync(w=>w.ParentWorkId==id&&w.Kind=="MAINTENANCE"&&w.Status!="CANCELLED",ct)
                    &&!await db.WorkAssignments.AnyAsync(a=>a.WorkId==id&&a.AssignmentStatus=="ACTIVE",ct),
                assets=await db.Assets.AsNoTracking().Where(a=>a.RoomId==legacy.RoomId&&a.BranchId==legacy.BranchId&&a.IsActive&&a.RetiredAt==null).OrderBy(a=>a.Name).Select(a=>new{a.AssetId,a.Name}).ToArrayAsync(ct)};
        }
        var work=await LoadRoomIssue(id,ct);var info=await IssueInfo(id,ct);var stage=await IssueStage(id,ct);
        var repairRequested=await db.WorkEvents.Where(e=>e.WorkId==id&&e.EventType=="ROOM_ISSUE_REPAIR_REQUESTED").MaxAsync(e=>(DateTime?)e.OccurredAt,ct);
        var canCreateRepair=stage is "ROOM_ISSUE_REPAIR_REQUESTED" or "ROOM_ISSUE_REPAIR_CREATED"
            &&!await db.WorkItems.AnyAsync(w=>w.ParentWorkId==id&&w.Kind=="MAINTENANCE"&&w.CreatedAt>=repairRequested&&w.Status!="CANCELLED",ct);
        var room=await db.Rooms.AsNoTracking().SingleAsync(r=>r.RoomId==work.RoomId,ct);
        return new{work=Output.Record(work),info,stage,canCreateRepair,canRequestInspection=stage=="ROOM_ISSUE_MATERIAL_REQUESTED"&&await access.HasRole("HOUSEKEEPING_SUPERVISOR",work.BranchId,ct)&&await access.Allows("INSPECTION_PERFORM",work.BranchId,ct:ct)&&await MaterialDelivered(await CurrentIssueMaterial(id,ct),ct),canReplaceMaterial=stage=="ROOM_ISSUE_MATERIAL_REQUESTED"&&await CanReplaceMaterial(id,ct),room=new{room.Code,rowVersion=Convert.ToBase64String(room.RowVersion)},
            head=false,investigation=await Investigation(id,ct),reporterUserId=work.CreatedBy,
            reporterName=await db.Users.AsNoTracking().Where(u=>u.UserId==work.CreatedBy).Select(u=>u.FullName).SingleAsync(ct),
            detectedByName=await db.Users.AsNoTracking().Where(u=>u.UserId==info.DetectedByUserId).Select(u=>u.FullName).SingleOrDefaultAsync(ct),
            people=await access.HasRole("HOUSEKEEPING_SUPERVISOR",work.BranchId,ct)||await access.HasRole("MAINTENANCE_SUPERVISOR",work.BranchId,ct)?await IncidentPeople(work.BranchId,ct):[],
            supervisor=await access.HasRole("HOUSEKEEPING_SUPERVISOR",work.BranchId,ct)&&await access.Allows("INSPECTION_PERFORM",work.BranchId,ct:ct),
            lead=await access.HasRole("MAINTENANCE_SUPERVISOR",work.BranchId,ct)&&await access.Allows("MAINTENANCE_EXECUTE",work.BranchId,true,ct:ct),
            store=await access.HasRole("STOREKEEPER",work.BranchId,ct)&&await access.Allows("MR_FULFILL",work.BranchId,ct:ct),
            maintenance=await db.WorkItems.AsNoTracking().Where(w=>w.ParentWorkId==id&&w.Kind=="MAINTENANCE").Select(w=>new{w.WorkId,w.Title,w.Status,
                externalRepair=db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&e.EventType=="EXTERNAL_REPAIR_ACCEPTED"),
                currentAssignee=db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE"),
                technicalReviewed=db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&e.EventType=="TECHNICAL_REVIEW_PASSED"&&e.OccurredAt>=w.CompletedAt)}).ToArrayAsync(ct),
            materials=await db.InventoryDocuments.AsNoTracking().Where(d=>d.SourceWorkId==id&&d.Kind=="MATERIAL_REQUEST").Select(d=>new{d.DocumentId,d.Status}).ToArrayAsync(ct),
            events=await db.WorkEvents.AsNoTracking().Where(e=>e.WorkId==id&&(e.EventType.StartsWith("ROOM_ISSUE_")||e.EventType=="LEGACY_ROOM_ISSUE_RECOVERED")).OrderByDescending(e=>e.WorkEventId)
                .Select(e=>new{e.EventType,e.Reason,e.OccurredAt}).ToArrayAsync(ct)};
    }
    private async Task NotifyIssueRole(WorkItem work,string code,string type,Guid key,CancellationToken ct)
    {
        var users=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.RoleId join u in db.Users on ur.UserId equals u.UserId
            where r.Code==code&&r.IsActive&&ur.BranchId==work.BranchId&&ur.RevokedAt==null&&ur.AssignedAt<=Now&&(ur.ExpiresAt==null||ur.ExpiresAt>Now)
                &&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null
                &&db.Memberships.Any(m=>m.UserId==u.UserId&&m.BranchId==work.BranchId&&m.Kind=="BRANCH_ACCESS"&&m.ValidFrom<=Now&&(m.ValidTo==null||m.ValidTo>Now)) select u.UserId).Distinct().ToArrayAsync(ct);
        foreach(var user in users)notifications.Add(work.BranchId,user,type,work.Title,work.WorkId,null,key,true);
    }
}
