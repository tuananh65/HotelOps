using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,TimeProvider clock,NotificationService notifications,DocumentService documents,RoomReadinessService readiness,StaffSupplyService supplies,RequestWorkflowService requests)
{
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    public async Task<bool> Owns(WorkItem w,CancellationToken ct)=>w.CreatedBy==actor.Id||await db.WorkAssignments.AnyAsync(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE",ct);
    public async Task<WorkItem> Load(long id,string action,CancellationToken ct)
    {
        var w=await db.WorkItems.SingleOrDefaultAsync(x=>x.WorkId==id,ct)??throw new ApiException(404,"WORK_NOT_FOUND");
        Output.Require(w.Kind!="OPERATIONAL"||!await access.HasRole("OPERATION_MANAGER",w.BranchId,ct),"WORK_ROLE_READ_ONLY",403);
        if(action!="read")Output.Require(!await requests.IsManagedRequest(id,ct),"USE_REQUEST_WORKFLOW",403);
        if(action=="read"){
            var visible=await RequestWorkflowVisibility.CanRead(db,access,actor.Id,id,Now,ct);
            if(visible!=null){Output.Require(visible.Value,"REQUEST_VISIBILITY_DENIED",403);return w;}
        }
        if(action!="read")await RequireWorkMutation(w.Kind,w.BranchId,ct);
        if(w.Kind=="MAINTENANCE"&&action is "execute" or "accept" or "start" or "resume" or "pause" or "progress" or "complete" or "unrepairable"){
            Output.Require(await access.HasRole("MAINTENANCE_STAFF",w.BranchId,ct)
                &&!await access.HasRole("MAINTENANCE_SUPERVISOR",w.BranchId,ct),"MAINTENANCE_STAFF_EXECUTION_REQUIRED",403);
            await WorkdayAuthority.Require(db,access,actor,clock,w.BranchId,"MAINTENANCE_STAFF",ct);
        }
        var owns=await Owns(w,ct);
        if(w.Kind=="MAINTENANCE"&&(action=="read"||action=="unrepairable"&&w.Status=="COMPLETED"&&w.Result=="FAIL"))
            owns=owns||await db.WorkAssignments.AnyAsync(a=>a.WorkId==id&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="COMPLETED",ct);
        if(action=="read" && w.Kind is "CLEANING" or "REWORK") owns=owns||await db.WorkEvents.AnyAsync(e=>e.WorkId==id&&e.EventType=="COMPLETE"&&e.ActorUserId==actor.Id,ct);
        if(action=="read"&&w.Kind=="INTERNAL_REQUEST"){
            Output.Require(await VisibleRequests(db.WorkItems.AsNoTracking()).AnyAsync(x=>x.WorkId==id,ct),"REQUEST_VISIBILITY_DENIED",403);
            owns=owns||await db.WorkAssignments.AnyAsync(a=>a.WorkId==id&&a.AssigneeUserId==actor.Id&&(a.AssignmentStatus=="COMPLETED"||(w.Status=="CANCELLED"&&a.AssignmentStatus=="CANCELLED")),ct);
        }
        if((action=="read"||action is "handover-accept" or "handover-reject")&&!owns)owns=await db.WorkEvents.AnyAsync(e=>e.WorkId==id&&e.EventType=="HANDOVER_OFFERED"&&e.RecipientUserId==actor.Id&&!db.WorkEvents.Any(d=>d.RelatedEventId==e.WorkEventId&&(d.EventType=="HANDOVER_ACCEPTED"||d.EventType=="HANDOVER_REJECTED")),ct);
        await access.Require(WorkRules.Permission(w.Kind,action),w.BranchId,owns,ct);
        if(w.Kind=="INCIDENT"&&action is "assign" or "cancel" or "verify" or "reopen")
            await access.Require("INCIDENT_TRIAGE",w.BranchId,ct:ct);
        return w;
    }
    public async Task<object> List(long? branchId,string kind,string? status,int page,int pageSize,CancellationToken ct,bool mine=false,string? search=null,long? departmentId=null,bool overdue=false,bool assignedToMe=false,string sort="latest",DateTime? from=null,DateTime? to=null,long? priorityId=null,string? queue=null,bool historyMine=false,bool cleaningCurrent=false)
    {
        if(kind=="OPERATIONAL"&&branchId is long operationBranch)
            Output.Require(!await access.HasRole("OPERATION_MANAGER",operationBranch,ct),"WORK_ROLE_READ_ONLY",403);
        var permission=WorkRules.Permission(kind,"read");var all=await access.Allows(permission,branchId,ct:ct);
        if(!all)await access.Require(permission,branchId,true,ct);
        Output.Require(!cleaningCurrent||kind is "CLEANING" or "REWORK","CLEANING_KIND_REQUIRED",400);
        var query=db.WorkItems.AsNoTracking().Where(w=>(branchId==null?db.Branches.Any(b=>b.BranchId==w.BranchId&&b.IsActive):w.BranchId==branchId)&&(cleaningCurrent?(w.Kind=="CLEANING"||w.Kind=="REWORK"):w.Kind==kind));
        if(cleaningCurrent)query=query.Where(w=>!WorkRules.Terminal.Contains(w.Status));
        if(kind=="INTERNAL_REQUEST")query=VisibleRequests(query);
        if(!all||mine)query=query.Where(w=>w.CreatedBy==actor.Id||db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&(a.AssignmentStatus=="ACTIVE"||((kind=="INTERNAL_REQUEST"||kind=="MAINTENANCE")&&(a.AssignmentStatus=="COMPLETED"||(w.Status=="CANCELLED"&&a.AssignmentStatus=="CANCELLED")))))
            ||db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&e.EventType=="HANDOVER_OFFERED"&&e.RecipientUserId==actor.Id&&!db.WorkEvents.Any(d=>d.RelatedEventId==e.WorkEventId&&(d.EventType=="HANDOVER_ACCEPTED"||d.EventType=="HANDOVER_REJECTED"))));
        if(assignedToMe)query=query.Where(w=>db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&(a.AssignmentStatus=="ACTIVE"||(a.AssignmentStatus=="COMPLETED"&&(kind!="MAINTENANCE"||w.Status=="CLOSED"||w.Status=="COMPLETED")))));
        if(historyMine)query=query.Where(w=>db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id));
        if(priorityId!=null)query=query.Where(w=>w.PriorityId==priorityId);
        if(!string.IsNullOrEmpty(queue)){
            Output.Require(kind=="MAINTENANCE","MAINTENANCE_QUEUE_REQUIRED",400);
            query=queue switch{
                "awaiting"=>query.Where(w=>w.Status=="PENDING"&&db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssignmentStatus=="ACTIVE"&&a.AcceptedAt==null)),
                "active"=>query.Where(w=>w.Status=="IN_PROGRESS"||w.Status=="ON_HOLD"),
                "review"=>query.Where(w=>w.Status=="COMPLETED"),
                "rework"=>query.Where(w=>!WorkRules.Terminal.Contains(w.Status)&&db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&e.EventType=="REOPEN")),
                "scheduled"=>query.Where(w=>w.WorkTemplateId!=null),
                _=>throw new ApiException(400,"INVALID_MAINTENANCE_QUEUE")};
        }
        if(!string.IsNullOrWhiteSpace(search)){var term=search.Trim();query=query.Where(w=>w.Title.Contains(term)||w.WorkNo.Contains(term)||db.Rooms.Any(r=>r.RoomId==w.RoomId&&r.Code.Contains(term)));}
        if(departmentId!=null)query=query.Where(w=>w.DepartmentId==departmentId);
        if(overdue)query=query.Where(w=>w.DueAt<Now&&!WorkRules.Terminal.Contains(w.Status));
        if(from!=null)query=query.Where(w=>w.CreatedAt>=from);
        if(to!=null)query=query.Where(w=>w.CreatedAt<to);
        Output.Require(from==null||to==null||from<to,"INVALID_DATE_RANGE",400);
        if(status!=null)query=query.Where(w=>w.Status==status);page=Math.Max(page,1);pageSize=Math.Clamp(pageSize,1,100);
        var ordered=sort switch{"latest"=>query.OrderByDescending(w=>w.WorkId),"due"=>query.OrderBy(w=>w.DueAt==null).ThenBy(w=>w.DueAt).ThenByDescending(w=>w.WorkId),"title"=>query.OrderBy(w=>w.Title).ThenByDescending(w=>w.WorkId),_=>throw new ApiException(400,"INVALID_WORK_SORT")};
        var rows=await ordered.Skip((page-1)*pageSize).Take(pageSize).ToArrayAsync(ct);
        var records=await MaintenanceRows(rows,ct);
        return new PageDto<ResourceDto>(records,await query.CountAsync(ct),page,pageSize);
    }
    public async Task<object> Detail(long id,CancellationToken ct)
    {
        var w=await Load(id,"read",ct);
        var root=w.Kind=="REWORK"?w.ParentWorkId:w.WorkId;
        var reviews=w.Kind is "CLEANING" or "REWORK"
            ? await db.WorkItems.AsNoTracking().Where(x=>x.BranchId==w.BranchId&&x.ParentWorkId==root&&x.Kind=="INSPECTION")
                .OrderByDescending(x=>x.WorkId).Take(10).Select(x=>new{x.WorkId,x.Status,x.Result,x.VerifiedAt,x.CompletionNote,
                    findings=db.InspectionFindings.Where(f=>f.InspectionWorkId==x.WorkId).Select(f=>new{f.Description,f.Status,f.ResolutionNote}).ToArray()}).ToArrayAsync(ct)
            : [];
        var context=await (from b in db.Branches.AsNoTracking() where b.BranchId==w.BranchId
            select new {branch=b.Name,room=db.Rooms.Where(r=>r.RoomId==w.RoomId).Select(r=>r.Code).FirstOrDefault(),
                roomCapacity=(from r in db.Rooms join t in db.RoomTypes on r.RoomTypeId equals t.RoomTypeId where r.RoomId==w.RoomId select (int?)t.Capacity).FirstOrDefault(),
                floor=(from r in db.Rooms join l in db.Locations on r.LocationId equals l.LocationId where r.RoomId==w.RoomId select l.Name).FirstOrDefault()}).SingleAsync(ct);
        var canCoordinate=w.Kind=="INTERNAL_REQUEST"&&await RequestAuthority(w,ct);
        var readOnlyActor=await IsReadOnlyWorkActor(w.Kind,w.BranchId,ct);
        var canHandle=!readOnlyActor&&(canCoordinate||await db.WorkAssignments.AnyAsync(a=>a.WorkId==id&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE",ct));
        var openIssues=w.Kind is "CLEANING" or "REWORK"&&await CleaningIssues(w.Kind=="REWORK"?w.ParentWorkId!.Value:w.WorkId).AnyAsync(i=>i.Status!="CLOSED"&&i.Status!="CANCELLED",ct);
        var canConfirmRoomIssue=openIssues&&w.Kind=="CLEANING"&&await CanReportDuringHousekeeping(w,ct)&&await access.HasRole("HOUSEKEEPING_SUPERVISOR",w.BranchId,ct);
        var canRecordPaperInspection=!openIssues&&w.Kind=="CLEANING"&&w.Status is "PENDING_INSPECTION" or "REWORK_REQUIRED"
            &&await access.Allows("INSPECTION_PERFORM",w.BranchId,ct:ct)
            &&!await db.WorkAssignments.AnyAsync(a=>a.AssigneeUserId==actor.Id&&(a.WorkId==id||db.WorkItems.Any(r=>r.WorkId==a.WorkId&&r.ParentWorkId==id&&r.Kind=="REWORK")),ct);
        var inspectionVariant=w.Kind=="CLEANING"&&(w.Status is "PENDING_INSPECTION" or "REWORK_REQUIRED")
            ? (await CleaningAfterRemediation(w.BranchId,ct)).Contains(id)?"SC":!openIssues&&await CleaningAfterRework(w.BranchId).ContainsAsync(id,ct)?"DL":null
            :w.Kind=="REWORK"&&w.Status=="PENDING_REINSPECTION"?"DL":null;
        return new {work=Output.Record(w),inspectionVariant,assignments=(await db.WorkAssignments.AsNoTracking().Where(x=>x.WorkId==id).ToArrayAsync(ct)).Select(Output.Record),
            context,reviews,roomIssues=await CleaningIssueDetails(w,ct),hasOpenRoomIssues=openIssues,maintenance=await MaintenanceContext(w,ct),capabilities=new{canRecordPaperInspection,canConfirmRoomIssue,canReportRoomIssue=await CanReportDuringHousekeeping(w,ct),
                canReconcileCleaningSupplies=CleaningReconciliationPolicy.CanReconcile(w.Kind,w.Status,w.ActualStartAt)
                    &&await access.HasRole("HOUSEKEEPING_SUPERVISOR",w.BranchId,ct)
                    &&await access.Allows("CLEANING_ASSIGN",w.BranchId,ct:ct)
                    &&!await db.SupplyReservations.AnyAsync(r=>r.WorkId==id&&(r.Status=="RESERVED"||r.Status=="CONSUMED"),ct),
                canCoordinateRequest=canCoordinate,canHandleRequest=canHandle,canCoordinateIncident=!readOnlyActor&&w.Kind=="INCIDENT"&&await access.Allows("INCIDENT_TRIAGE",w.BranchId,ct:ct)},
            attachments=await db.Attachments.AsNoTracking().Where(a=>a.WorkId==id&&a.InvalidatedAt==null).OrderByDescending(a=>a.AttachmentId).Select(a=>new{a.AttachmentId,a.OriginalName,a.MimeType,a.ByteLength,a.CreatedAt}).ToArrayAsync(ct),
            assignmentDetails=await (from a in db.WorkAssignments.AsNoTracking() join u in db.Users on a.AssignedBy equals u.UserId join recipient in db.Users on a.AssigneeUserId equals recipient.UserId where a.WorkId==id orderby a.AssignedAt descending select new{a.AssignmentId,assignedBy=u.FullName,assignee=recipient.FullName,a.AssignedAt,a.AcceptedAt,a.EndedAt,a.AssignmentStatus}).ToArrayAsync(ct),
            checklist=(await db.WorkChecklistItems.AsNoTracking().Where(x=>x.WorkId==id).OrderBy(x=>x.WorkChecklistItemId).ToArrayAsync(ct)).Select(Output.Record),
            findings=(await db.InspectionFindings.AsNoTracking().Where(x=>x.InspectionWorkId==id).ToArrayAsync(ct)).Select(Output.Record),
            links=(await db.WorkLinks.AsNoTracking().Where(x=>x.SourceWorkId==id).ToArrayAsync(ct)).Select(Output.Record),
            time=(await db.WorkIntervals.AsNoTracking().Where(x=>x.WorkId==id).ToArrayAsync(ct)).Select(Output.Record),
            events=(await db.WorkEvents.AsNoTracking().Where(x=>x.WorkId==id).OrderByDescending(x=>x.WorkEventId).Take(200).ToArrayAsync(ct)).Select(Output.Record)};
    }
    private async Task<bool> RequestAuthority(WorkItem w,CancellationToken ct) =>
        !await IsReadOnlyWorkActor(w.Kind,w.BranchId,ct)
        &&await access.Allows("IR_PROCESS",w.BranchId,ct:ct)
        &&await db.Memberships.AnyAsync(m=>m.UserId==actor.Id&&m.BranchId==w.BranchId&&m.Kind=="DEPARTMENT_AUTHORITY"&&m.DepartmentId==w.DepartmentId&&m.ValidFrom<=Now&&(m.ValidTo==null||m.ValidTo>Now),ct);
    public async Task<object> CleaningQueue(long branchId,string queue,int page,CancellationToken ct,string? search=null,string sort="due")
    {
        await access.Require("CLEANING_ASSIGN",branchId,ct:ct);
        await access.Require("CLEANING_VIEW",branchId,ct:ct);
        var query=db.WorkItems.AsNoTracking().Where(w=>w.BranchId==branchId&&w.Kind=="CLEANING"&&!WorkRules.Terminal.Contains(w.Status));
        var incidentWork=await CleaningWithOpenIssues(branchId).ToArrayAsync(ct);
        var afterRemediation=await CleaningAfterRemediation(branchId,ct);
        var afterRework=await CleaningAfterRework(branchId).ToArrayAsync(ct);
        var awaitingRemedy=await CleaningAwaitingRemedy(branchId,ct);
        query=query.Where(w=>!awaitingRemedy.Contains(w.WorkId)||!(w.Status=="PENDING_INSPECTION"||w.Status=="REWORK_REQUIRED"));
        var countRows=await query.Select(w=>new{w.WorkId,w.Status}).ToArrayAsync(ct);
        var counts=countRows.GroupBy(w=>(w.Status=="PENDING_INSPECTION"||w.Status=="REWORK_REQUIRED")&&afterRemediation.Contains(w.WorkId)?"PENDING_INSPECTION_SC":(w.Status=="PENDING_INSPECTION"||w.Status=="REWORK_REQUIRED")&&incidentWork.Contains(w.WorkId)?"INCIDENT_INSPECTION":afterRework.Contains(w.WorkId)?"PENDING_INSPECTION_DL":w.Status).Select(g=>new{status=g.Key,count=g.Count()}).ToArray();
        query=queue switch {
            "unassigned"=>query.Where(w=>w.Status=="PENDING_ASSIGNMENT"),
            "assigned"=>query.Where(w=>w.Status=="ASSIGNED"),
            "doing"=>query.Where(w=>w.Status=="IN_PROGRESS"||w.Status=="ON_HOLD"),
            "inspect"=>query.Where(w=>(w.Status=="PENDING_INSPECTION"||w.Status=="REWORK_REQUIRED")&&(afterRemediation.Contains(w.WorkId)||!incidentWork.Contains(w.WorkId)&&(w.Status=="PENDING_INSPECTION"||afterRework.Contains(w.WorkId)))),
            "incidents"=>query.Where(w=>(w.Status=="PENDING_INSPECTION"||w.Status=="REWORK_REQUIRED")&&incidentWork.Contains(w.WorkId)&&!afterRemediation.Contains(w.WorkId)),
            "rework"=>query.Where(w=>w.Status=="REWORK_REQUIRED"&&!incidentWork.Contains(w.WorkId)&&!afterRework.Contains(w.WorkId)),
            "all"=>query,
            "rooms"=>query.Where(w=>false),
            _=>throw new ApiException(400,"INVALID_QUEUE")};
        if(!string.IsNullOrWhiteSpace(search)){var term=search.Trim();query=query.Where(w=>w.Title.Contains(term)||db.Rooms.Any(r=>r.RoomId==w.RoomId&&r.Code.Contains(term)));}
        page=Math.Clamp(page,1,1000000);
        var rows=await query.Select(w=>new CleaningQueueEntry(w.WorkId,w.Title,w.RoomId,db.Rooms.Where(r=>r.RoomId==w.RoomId).Select(r=>r.Code).FirstOrDefault(),w.Status,w.DueAt,null,null,null,null)).ToArrayAsync(ct);
        var roomsQuery=RoomsNeedingCleaning(branchId);
        var roomCount=await roomsQuery.CountAsync(ct);
        if(!string.IsNullOrWhiteSpace(search)){var term=search.Trim();roomsQuery=roomsQuery.Where(r=>r.Code.Contains(term));}
        if(queue is "all" or "rooms"){
            var rooms=await (from room in roomsQuery join type in db.RoomTypes on room.RoomTypeId equals type.RoomTypeId
                join floor in db.Locations on room.LocationId equals floor.LocationId
                select new CleaningQueueEntry(null,"Phòng cần dọn",room.RoomId,room.Code,"PENDING_CLEANING",null,room.RowVersion,floor.Name,type.Name,type.Capacity)).ToArrayAsync(ct);
            rows=rows.Concat(rooms).ToArray();
        }
        var total=rows.Length;
        var ordered=sort switch{
            "latest"=>rows.OrderBy(r=>r.WorkId!=null).ThenByDescending(r=>r.WorkId).ThenBy(r=>r.Room),
            "due"=>rows.OrderBy(r=>r.DueAt==null).ThenBy(r=>r.DueAt).ThenBy(r=>r.Room),
            "title"=>rows.OrderBy(r=>r.Title).ThenBy(r=>r.Room),
            _=>throw new ApiException(400,"INVALID_WORK_SORT")};
        rows=ordered.Skip((page-1)*50).Take(50).ToArray();
        var ids=rows.Where(w=>w.WorkId!=null).Select(w=>w.WorkId!.Value).ToArray();
        var assignees=await (from a in db.WorkAssignments.AsNoTracking() join u in db.Users on a.AssigneeUserId equals u.UserId
            where ids.Contains(a.WorkId)&&a.AssignmentStatus=="ACTIVE" select new{a.WorkId,u.FullName}).ToArrayAsync(ct);
        var incidentIds=incidentWork.Where(i=>ids.Contains(i)).ToArray();
        return new{items=rows.Select(w=>new{w.WorkId,w.Title,w.RoomId,room=w.Room,w.Status,w.DueAt,w.RowVersion,w.Floor,w.RoomType,w.Capacity,isRoom=w.WorkId==null,inspectionVariant=w.WorkId!=null&&afterRemediation.Contains(w.WorkId.Value)?"SC":w.WorkId!=null&&afterRework.Contains(w.WorkId.Value)&&!incidentIds.Contains(w.WorkId.Value)?"DL":null,hasRoomIssue=w.WorkId!=null&&incidentIds.Contains(w.WorkId.Value),assignee=string.Join(", ",assignees.Where(a=>a.WorkId==w.WorkId).Select(a=>a.FullName))}),counts=counts.Concat([new{status="PENDING_CLEANING",count=roomCount}]),total,page,pageSize=50};
    }
    private sealed record CleaningQueueEntry(long? WorkId,string Title,long? RoomId,string? Room,string Status,DateTime? DueAt,byte[]? RowVersion,string? Floor,string? RoomType,int? Capacity);
    public async Task<object> Related(long id,CancellationToken ct)
    {
        var w=await Load(id,"read",ct);var root=w.ParentWorkId??w.WorkId;
        var candidates=await VisibleRequests(db.WorkItems.AsNoTracking()).Where(x=>x.BranchId==w.BranchId&&(x.WorkId==root||x.ParentWorkId==root)).OrderByDescending(x=>x.WorkId).ToArrayAsync(ct);
        var visible=new List<ResourceDto>();
        foreach(var item in candidates)if(await access.Allows(WorkRules.Permission(item.Kind,"read"),item.BranchId,await Owns(item,ct),ct:ct))visible.Add(Output.Record(item));
        return visible;
    }
    public async Task<object> Candidates(long branchId,string kind,CancellationToken ct,long? workId=null)
    {
        await RequireWorkMutation(kind,branchId,ct);
        if(kind is "MAINTENANCE" or "INCIDENT")
            await access.Require(kind=="MAINTENANCE"?"MAINTENANCE_ASSIGN":"INCIDENT_TRIAGE",branchId,ct:ct);
        if(workId==null)await access.Require(WorkRules.Permission(kind,"assign"),branchId,ct:ct);
        else {
            var work=await Load(workId.Value,"handover-offer",ct);
            Output.Require(work.BranchId==branchId&&work.Kind==kind,"WORK_SCOPE_MISMATCH",403);
            Output.Require(await db.WorkAssignments.AnyAsync(a=>a.WorkId==workId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE",ct),"CURRENT_ASSIGNEE_REQUIRED",403);
        }
        return await CandidateRows(branchId,kind,ct,onShiftOnly:kind is "CLEANING" or "REWORK");
    }
    private sealed record WorkCandidate(long UserId,string EmployeeCode,string FullName);
    private async Task<WorkCandidate[]> CandidateRows(long branchId,string kind,CancellationToken ct,bool onShiftOnly=false){
        var now=Now;var code=WorkRules.Permission(kind,"execute");
        var onShift=onShiftOnly?await CurrentCleaningShift(branchId,ct):db.WorkdayAssignments.AsNoTracking();
        var housekeepers=HousekeepingStaffIds(branchId);
        var rows=await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.RoleId join grant in db.RolePermissions on role.RoleId equals grant.RoleId join p in db.Permissions on grant.PermissionId equals p.PermissionId join user in db.Users on ur.UserId equals user.UserId
            where ur.RevokedAt==null&&ur.AssignedAt<=now&&(ur.ExpiresAt==null||ur.ExpiresAt>now)&&role.IsActive&&p.Code==code&&p.IsActive
            &&((ur.BranchId==null&&grant.DataScope=="ALL")||(ur.BranchId==branchId&&(grant.DataScope=="ALL"||grant.DataScope=="BRANCH"||grant.DataScope=="OWN")))
            &&user.AccountStatus=="ACTIVE"&&user.EmploymentStatus=="ACTIVE"&&user.DeletedAt==null
            &&(kind!="MAINTENANCE"||role.Code=="MAINTENANCE_STAFF")
            &&!db.UserRoles.Any(blocked=>blocked.UserId==user.UserId&&blocked.RevokedAt==null&&blocked.AssignedAt<=now&&(blocked.ExpiresAt==null||blocked.ExpiresAt>now)
                &&(blocked.BranchId==null||blocked.BranchId==branchId)&&db.Roles.Any(br=>br.RoleId==blocked.RoleId&&br.IsActive
                    &&(br.Code=="OWNER"||((kind=="INCIDENT"||kind=="INTERNAL_REQUEST")&&br.Code=="OPERATION_MANAGER"))))
            &&((kind!="CLEANING"&&kind!="REWORK")||!UnacceptedHousekeepers().Contains(user.UserId))
            &&(!onShiftOnly||(onShift.Any(a=>a.UserId==user.UserId)&&housekeepers.Contains(user.UserId)))
            &&db.Memberships.Any(m=>m.UserId==user.UserId&&m.BranchId==branchId&&m.Kind=="BRANCH_ACCESS"&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))
            select new{user.UserId,user.EmployeeCode,user.FullName}).Distinct().OrderBy(u=>u.FullName).Take(200).ToArrayAsync(ct);
        return rows.Select(u=>new WorkCandidate(u.UserId,u.EmployeeCode,u.FullName)).ToArray();
    }
    public Task<object> Create(CreateWorkRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        if(r.Kind is "CLEANING" or "REWORK")await StaffSupplyService.Lock(db,ct);
        await RequireWorkMutation(r.Kind,r.BranchId,ct);
        await access.Require(WorkRules.Permission(r.Kind,"create"),r.BranchId,true,ct);
        if(r.Kind=="INCIDENT")Output.Require(!await access.HasRole("HK_STAFF",r.BranchId,ct),"HOUSEKEEPING_STAFF_CANNOT_REPORT_INCIDENT",403);
        if(r.Kind=="INCIDENT"&&r.RoomId!=null)Output.Require(!await access.HasRole("HOUSEKEEPING_SUPERVISOR",r.BranchId,ct),"ROOM_ISSUE_USE_WORKFLOW",409);
        if(r.Kind is "CLEANING" or "REWORK"){
            Output.Require(await (await CurrentCleaningShift(r.BranchId,ct)).AnyAsync(a=>a.UserId==actor.Id,ct),"CLEANING_CREATOR_NOT_ON_SHIFT",403);
            Output.Require(r.AssigneeUserId!=null,"CLEANING_ASSIGNEE_REQUIRED",400);
            await RequireCleaningAssignee(r.AssigneeUserId!.Value,r.BranchId,ct);
            Output.Require(r.DurationMinutes is 20 or 25 or 30,"CLEANING_DURATION_REQUIRED",400);
        }
        Output.Require(r.Kind!="INSPECTION"||r.ChecklistTemplateId!=null,"INSPECTION_CHECKLIST_REQUIRED",400);
        var w=new WorkItem{BranchId=r.BranchId,Kind=r.Kind,Status=WorkRules.Initial(r.Kind),WorkNo=Guid.NewGuid().ToString("N"),Title=r.Title.Trim(),Description=r.Description,
            DepartmentId=r.DepartmentId,RequesterDepartmentId=r.RequesterDepartmentId,RoomId=r.RoomId,LocationId=r.LocationId,AssetId=r.AssetId,ParentWorkId=r.ParentWorkId,ChecklistTemplateId=r.ChecklistTemplateId,
            PriorityId=r.PriorityId,SeverityId=r.SeverityId,CategoryId=r.CategoryId,DueAt=r.DueAt,CreatedBy=actor.Id,CreatedAt=Now};
        if(w.Kind=="INTERNAL_REQUEST"){
            await ApplyInternalRequestType(w,ct,r.AssigneeUserId);
            var departments=await db.Memberships.Where(m=>m.UserId==actor.Id&&m.BranchId==r.BranchId&&m.Kind=="EMPLOYMENT"&&m.ValidFrom<=Now&&(m.ValidTo==null||m.ValidTo>Now)).Select(m=>m.DepartmentId).Distinct().ToArrayAsync(ct);
            w.RequesterDepartmentId??=departments.Length==1?departments[0]:null;
            Output.Require(w.RequesterDepartmentId!=null&&departments.Contains(w.RequesterDepartmentId),"REQUESTER_DEPARTMENT_MEMBERSHIP_REQUIRED");
        }
        Room? room=null;
        if(r.RoomId!=null){room=await db.Rooms.SingleOrDefaultAsync(x=>x.RoomId==r.RoomId&&x.BranchId==r.BranchId&&x.IsActive,ct)??throw new ApiException(409,"ROOM_UNAVAILABLE");w.TurnoverNo=room.TurnoverNo;}
        if(r.RoomRowVersion!=null){
            Output.Require(w.Kind=="CLEANING"&&room!=null,"CLEANING_ROOM_REQUIRED",400);
            Output.Version(room!.RowVersion,r.RoomRowVersion);
            Output.Require(await RoomsNeedingCleaning(r.BranchId).AnyAsync(x=>x.RoomId==room.RoomId,ct),"ROOM_NO_LONGER_NEEDS_CLEANING");
        }
        if(r.ParentWorkId!=null){var parent=await Load(r.ParentWorkId.Value,"read",ct);Output.Require(parent.BranchId==w.BranchId,"PARENT_BRANCH_MISMATCH");
            if(r.Kind=="MAINTENANCE"&&parent.Kind=="INCIDENT"){
                if(await IsRoomIssue(parent.WorkId,ct)){
                    Output.Require(await access.HasRole("MAINTENANCE_SUPERVISOR",w.BranchId,ct),"MAINTENANCE_SUPERVISOR_REQUIRED",403);
                    var repairStage=await IssueStage(parent.WorkId,ct);
                    Output.Require(repairStage is "ROOM_ISSUE_REPAIR_REQUESTED" or "ROOM_ISSUE_REPAIR_CREATED","ROOM_ISSUE_WRONG_STAGE");
                    Output.Require(w.RoomId==parent.RoomId&&w.AssetId==parent.AssetId,"ROOM_ISSUE_OBJECT_NOT_IN_ROOM");
                }
                Output.Require(!WorkRules.Terminal.Contains(parent.Status),"ACTIVE_INCIDENT_REQUIRED");
                Output.Require(!await db.WorkItems.AnyAsync(x=>x.ParentWorkId==parent.WorkId&&x.Kind=="MAINTENANCE"&&x.Status!="CLOSED"&&x.Status!="CANCELLED",ct),"MAINTENANCE_ALREADY_ACTIVE");}
            if(r.Kind is "INSPECTION" or "REWORK")Output.Require(parent.Status is "PENDING_INSPECTION" or "REWORK_REQUIRED","CLEANING_NOT_READY_FOR_REVIEW");
            if(r.Kind=="REWORK"){Output.Require(parent.Kind=="CLEANING"&&parent.RoomId==w.RoomId&&parent.TurnoverNo==w.TurnoverNo,"CLEANING_PARENT_REQUIRED");w.DepartmentId=parent.DepartmentId;}}
        if(w.Kind is "INSPECTION" or "REWORK"){
            Output.Require(w.ParentWorkId!=null,"CLEANING_PARENT_REQUIRED",400);
            Output.Require(!await db.WorkItems.AnyAsync(x=>x.ParentWorkId==w.ParentWorkId&&x.Kind==w.Kind&&!WorkRules.Terminal.Contains(x.Status)
                && !(w.Kind=="REWORK"&&x.Status=="PENDING_REINSPECTION"&&db.WorkItems.Any(i=>i.ParentWorkId==w.ParentWorkId&&i.Kind=="INSPECTION"&&i.Status=="FAILED"&&i.WorkId>x.WorkId&&i.VerifiedAt>=x.CompletedAt)),ct),"HOUSEKEEPING_WORK_ALREADY_ACTIVE");
            if(w.Kind=="REWORK")Output.Require(await db.WorkItems.AnyAsync(x=>x.WorkId==w.ParentWorkId&&x.Kind=="CLEANING"&&x.Status=="REWORK_REQUIRED",ct),"REWORK_REQUIRES_FAILED_INSPECTION");
            else {
                Output.Require(!await db.WorkItems.AnyAsync(x=>x.ParentWorkId==w.ParentWorkId&&x.Kind=="REWORK"&&!WorkRules.Terminal.Contains(x.Status)&&x.Status!="PENDING_REINSPECTION",ct),"REWORK_NOT_SUBMITTED");
                if(await db.WorkItems.AnyAsync(x=>x.WorkId==w.ParentWorkId&&x.Status=="REWORK_REQUIRED",ct))
                    Output.Require(await db.WorkItems.AnyAsync(x=>x.ParentWorkId==w.ParentWorkId&&x.Kind=="REWORK"&&x.Status=="PENDING_REINSPECTION"
                        &&!db.WorkItems.Any(i=>i.ParentWorkId==w.ParentWorkId&&i.Kind=="INSPECTION"&&i.Status=="FAILED"&&i.WorkId>x.WorkId&&i.VerifiedAt>=x.CompletedAt),ct),"REWORK_NOT_SUBMITTED");
            }
        }
        if(w.Kind=="CLEANING"){
            Output.Require(room!=null,"ROOM_REQUIRED",400);
            Output.Require(!await db.WorkItems.AnyAsync(x=>x.RoomId==r.RoomId&&x.Kind=="CLEANING"&&!WorkRules.Terminal.Contains(x.Status),ct),"CLEANING_ALREADY_ACTIVE");
            Output.Require(await RoomsNeedingCleaning(r.BranchId).AnyAsync(x=>x.RoomId==r.RoomId,ct),"ROOM_NO_LONGER_NEEDS_CLEANING");
            w.DepartmentId=await (from a in await CurrentCleaningShift(r.BranchId,ct) join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
                where a.UserId==r.AssigneeUserId select (long?)s.DepartmentId).FirstOrDefaultAsync(ct);
            Output.Require(w.DepartmentId!=null,"CLEANING_ASSIGNEE_NOT_ON_SHIFT");
        }
        if(w.AssetId!=null)Output.Require(await db.Assets.AnyAsync(a=>a.AssetId==w.AssetId&&a.BranchId==w.BranchId&&a.IsActive,ct),"ASSET_UNAVAILABLE");
        if(w.ChecklistTemplateId!=null){var template=await db.ChecklistTemplates.SingleOrDefaultAsync(t=>t.ChecklistTemplateId==w.ChecklistTemplateId&&t.BranchId==w.BranchId&&t.IsActive&&t.PublishedAt!=null,ct);
            Output.Require(template!=null&&template.Kind==(w.Kind=="REWORK"?"CLEANING":w.Kind),"CHECKLIST_KIND_MISMATCH");}
        db.WorkItems.Add(w);await db.SaveChangesAsync(ct);
        if(w.ChecklistTemplateId!=null)foreach(var item in await db.ChecklistItems.Where(x=>x.ChecklistTemplateId==w.ChecklistTemplateId).ToArrayAsync(ct))
            db.WorkChecklistItems.Add(new WorkChecklistItem{BranchId=w.BranchId,WorkId=w.WorkId,ChecklistTemplateId=w.ChecklistTemplateId.Value,ChecklistItemId=item.ChecklistItemId,DescriptionSnapshot=item.Description,RequiredSnapshot=item.IsRequired,WeightSnapshot=item.Weight});
        if(w.Kind=="CLEANING")room!.CurrentCleaningWorkId=w.WorkId;
        if(w.Kind=="MAINTENANCE"){
            await access.RequireRolePermission("MAINTENANCE_SUPERVISOR","MAINTENANCE_ASSIGN",w.BranchId,ct);
            if(r.AssigneeUserId!=null){
                await access.EligibleFor(r.AssigneeUserId.Value,w.BranchId,"MAINTENANCE_EXECUTE",ct);
                Output.Require(await EligibleMaintenanceAssignee(r.AssigneeUserId.Value,w.BranchId,ct),"MAINTENANCE_ASSIGNEE_REQUIRED",403);
                db.WorkAssignments.Add(new WorkAssignment{WorkId=w.WorkId,BranchId=w.BranchId,AssigneeUserId=r.AssigneeUserId.Value,AssignedBy=actor.Id,AssignedAt=Now,AssignmentStatus="ACTIVE"});
                notifications.Add(w.BranchId,r.AssigneeUserId.Value,"WORK_ASSIGNED",w.Title,w.WorkId,null,Guid.NewGuid(),true);
            }
        }
        if(w.Kind=="INTERNAL_REQUEST"&&r.AssigneeUserId!=null){
            db.WorkAssignments.Add(new WorkAssignment{WorkId=w.WorkId,BranchId=w.BranchId,AssigneeUserId=r.AssigneeUserId.Value,AssignedBy=actor.Id,AssignedAt=Now,AssignmentStatus="ACTIVE"});
        }
        Event(w,"CREATED",Guid.NewGuid(),null,null);await db.SaveChangesAsync(ct);
        if(w.Kind is "CLEANING" or "REWORK")return await Act(w.WorkId,"assign",new ActionRequest(Convert.ToBase64String(w.RowVersion),Guid.NewGuid(),UserId:r.AssigneeUserId,DurationMinutes:r.DurationMinutes),ct);
        return (object)Output.Record(w);
    },ct);
    public Task<object> Act(long id,string action,ActionRequest r,CancellationToken ct)=>action=="unrepairable"?CompleteUnrepairable(id,r,ct):ActCore(id,action,r,ct);
    private Task<object> ActCore(long id,string action,ActionRequest r,CancellationToken ct,bool roomWorkflow=false)=>atomic.Run(async()=>{
        if(action is "assign" or "accept" or "start" or "handover-accept" or "handover-offer")await StaffSupplyService.Lock(db,ct);
        var w=await Load(id,action is "handover-accept" or "handover-reject"?"read":action,ct);
        Output.Require(!await requests.IsManagedRequest(id,ct),"USE_REQUEST_WORKFLOW",403);
        await RequireWorkMutation(w.Kind,w.BranchId,ct);
        Output.Key(r.OperationKey);
        if(action=="complete"&&w.Kind is "CLEANING" or "REWORK" or "MAINTENANCE"){
            var previous=await db.WorkEvents.AsNoTracking().SingleOrDefaultAsync(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==r.OperationKey,ct);
            if(previous!=null){Output.Require(previous.EventType=="COMPLETE"&&previous.Reason==r.Reason,"IDEMPOTENCY_KEY_REUSED");return (object)Output.Record(w);}
        }
        Output.Version(w.RowVersion,r.RowVersion);Output.Key(r.OperationKey);
        Output.Require(w.Kind!="MAINTENANCE"||action!="verify","MAINTENANCE_STAFF_RESULT_REQUIRED",403);
        if(w.Kind=="MAINTENANCE"&&action is "complete" or "verify")await RequireReplacementSettled(id,ct);
        if(w.Kind=="MAINTENANCE")Output.Require(!action.StartsWith("handover-"),"MAINTENANCE_HANDOVER_NOT_SUPPORTED",403);
        Output.Require(w.Kind!="INCIDENT"||!await IsRoomIssue(id,ct),"ROOM_ISSUE_USE_WORKFLOW",409);
        if(w.Kind=="MAINTENANCE"&&w.ParentWorkId!=null&&await IsRoomIssue(w.ParentWorkId.Value,ct)){
            Output.Require(action is "assign" or "accept" or "start" or "complete" or "progress" or "pause" or "resume","ROOM_ISSUE_USE_WORKFLOW",403);
            Output.Require(await IssueStage(w.ParentWorkId.Value,ct) is "ROOM_ISSUE_REPAIR_REQUESTED" or "ROOM_ISSUE_REPAIR_CREATED","ROOM_ISSUE_WRONG_STAGE");
        }
        if(action is "handover-accept" or "handover-reject")await access.Require(WorkRules.Permission(w.Kind,"execute"),w.BranchId,true,ct);
        if(action.StartsWith("handover-")&&w.Kind is "MAINTENANCE" or "INCIDENT")
            await access.Require(w.Kind=="MAINTENANCE"?"MAINTENANCE_ASSIGN":"INCIDENT_TRIAGE",w.BranchId,ct:ct);
        if(w.RoomId!=null&&w.Kind is "CLEANING" or "INSPECTION" or "REWORK")Output.Require(await db.Rooms.AnyAsync(room=>room.RoomId==w.RoomId&&room.TurnoverNo==w.TurnoverNo&&room.IsActive,ct),"ROOM_CYCLE_NO_LONGER_CURRENT");
        Output.Require(!await db.WorkEvents.AnyAsync(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==r.OperationKey,ct),"OPERATION_ALREADY_APPLIED");
        var before=w.Status;var now=Now;
        var a=await db.WorkAssignments.SingleOrDefaultAsync(x=>x.WorkId==id&&x.AssignmentStatus=="ACTIVE",ct);
        if(w.Kind is "CLEANING" or "REWORK")await CleaningDeadline.LogIfOverdue(db,w,a,now,ct);
        if(action is "accept" or "start" or "resume" or "pause" or "complete" or "pass" or "fail" or "progress")
            Output.Require(a?.AssigneeUserId==actor.Id,"CURRENT_ASSIGNEE_REQUIRED",403);
        if(w.Kind=="INTERNAL_REQUEST"){
            if(action is "submit" or "verify" or "reopen")Output.Require(w.CreatedBy==actor.Id,"REQUESTER_REQUIRED",403);
            else if(action is "route" or "assign")Output.Require(await RequestAuthority(w,ct),"RECEIVING_DEPARTMENT_AUTHORITY_REQUIRED",403);
            else if(action is "accept" or "reject" or "start" or "complete" or "cancel" or "progress"){
                Output.Require(a?.AssigneeUserId==actor.Id||await RequestAuthority(w,ct),"RECEIVING_DEPARTMENT_AUTHORITY_REQUIRED",403);
            }
        }
        if(action is "triage" or "escalate"){
            Output.Require(!WorkRules.Terminal.Contains(w.Status),"WORK_ALREADY_TERMINAL");Output.Reason(r.Reason);
            if(action=="triage"){
                Output.Require(w.Kind=="INCIDENT"&&r.PriorityId!=null&&r.DueAt>now,"PRIORITY_AND_FUTURE_DEADLINE_REQUIRED",400);
                w.PriorityId=r.PriorityId;w.SeverityId=r.SeverityId;w.CategoryId=r.CategoryId;w.DueAt=r.DueAt;
            }else{
                Output.Require(w.Kind is "INCIDENT" or "INTERNAL_REQUEST","ESCALATION_KIND_UNSUPPORTED",400);
                await notifications.ForPermission(w.BranchId,w.Kind=="INCIDENT"?"INCIDENT_ESCALATION_HANDLE":"IR_OVERDUE_HANDLE","WORK_ESCALATED",w.Title,id,null,r.OperationKey,ct);
            }
        }else if(action=="assign"){
            if(w.Kind=="MAINTENANCE"){
                await access.RequireRolePermission("MAINTENANCE_SUPERVISOR","MAINTENANCE_ASSIGN",w.BranchId,ct);
                Output.Require(r.UserId!=null&&await EligibleMaintenanceAssignee(r.UserId.Value,w.BranchId,ct),"MAINTENANCE_ASSIGNEE_REQUIRED",403);
            }
            Output.Require(!WorkRules.Terminal.Contains(w.Status)&&r.UserId!=null,"ASSIGNMENT_NOT_ALLOWED");await access.Eligible(r.UserId!.Value,w.BranchId,ct);
            if(w.Kind is "CLEANING" or "REWORK")Output.Require(w.Status is not ("PENDING_INSPECTION" or "REWORK_REQUIRED" or "PENDING_REINSPECTION"),"SUBMITTED_HOUSEKEEPING_IMMUTABLE");
            if(w.Kind=="INSPECTION")Output.Require(w.Status!="PENDING_CONCLUSION","SUBMITTED_HOUSEKEEPING_IMMUTABLE");
            await access.EligibleFor(r.UserId.Value,w.BranchId,WorkRules.Permission(w.Kind,"execute"),ct);
            if(w.Kind is "CLEANING" or "REWORK")await RequireCleaningAssignee(r.UserId.Value,w.BranchId,ct,id);
            if(w.Kind=="INSPECTION")Output.Require(!await db.WorkAssignments.AnyAsync(x=>x.AssigneeUserId==r.UserId&&(x.WorkId==w.ParentWorkId||db.WorkItems.Any(q=>q.WorkId==x.WorkId&&q.ParentWorkId==w.ParentWorkId&&q.Kind=="REWORK")),ct),"INSPECTOR_MUST_BE_INDEPENDENT");
            if(w.Kind is "CLEANING" or "REWORK")Output.Require(r.DurationMinutes is 20 or 25 or 30,"CLEANING_DURATION_REQUIRED",400);
            // Assignment proposes the job; supplies are issued only when its recipient accepts.
            if(a!=null){if(w.Kind is not ("CLEANING" or "REWORK"))Output.Reason(r.Reason);await CloseTime(w,"REASSIGN",ct);now=Now;a.AssignmentStatus="REASSIGNED";a.EndedAt=now;await db.SaveChangesAsync(ct);}
            if(w.Kind is "CLEANING" or "REWORK"){w.DueAt=now.AddMinutes(r.DurationMinutes!.Value);w.CompletedAt=null;}
            db.WorkAssignments.Add(new WorkAssignment{WorkId=id,BranchId=w.BranchId,AssigneeUserId=r.UserId.Value,AssignedBy=actor.Id,AssignedAt=now,AssignmentStatus="ACTIVE",Reason=r.Reason});
            w.Status=w.Kind switch{"CLEANING" or "OPERATIONAL" or "INSPECTION"=>"ASSIGNED","REWORK"=>"PENDING_ACCEPTANCE","MAINTENANCE"=>"PENDING",_=>w.Status};
            notifications.Add(w.BranchId,r.UserId.Value,"WORK_ASSIGNED",w.Title,id,null,r.OperationKey,true);
        } else if(action=="progress"){
            Output.Require(w.Kind is not ("CLEANING" or "REWORK"),"HOUSEKEEPING_PROGRESS_NOT_SUPPORTED",403);
            Output.Require(a?.AssigneeUserId==actor.Id&&w.Status is "IN_PROGRESS" or "INSPECTING","ASSIGNEE_EXECUTION_REQUIRED",403);
            Output.Require(r.Progress is >=0 and <=100,"INVALID_PROGRESS",400);w.ProgressPercent=r.Progress!.Value;
        } else if(action.StartsWith("handover-")){
            if(w.Kind is "CLEANING" or "REWORK")
                Output.Require(a?.AcceptedAt==null&&w.ActualStartAt==null&&w.Status is "ASSIGNED" or "PENDING_ACCEPTANCE"
                    && !await db.WorkIntervals.AnyAsync(t=>t.WorkId==id,ct),"HOUSEKEEPING_HANDOVER_BEFORE_ACCEPT_ONLY");
            Output.Require(!WorkRules.Terminal.Contains(w.Status),"WORK_ALREADY_TERMINAL");Output.Reason(r.Reason);
            if(action=="handover-offer"){
                Output.Require(a?.AssigneeUserId==actor.Id&&r.UserId!=null,"ASSIGNEE_REQUIRED",403);await access.Eligible(r.UserId!.Value,w.BranchId,ct);
                await access.EligibleFor(r.UserId.Value,w.BranchId,WorkRules.Permission(w.Kind,"execute"),ct);
                if(w.Kind is "CLEANING" or "REWORK")await RequireCleaningAssignee(r.UserId.Value,w.BranchId,ct,id);
                Event(w,"HANDOVER_OFFERED",r.OperationKey,r.Reason,before,r.UserId);notifications.Add(w.BranchId,r.UserId.Value,"HANDOVER_OFFERED",w.Title,id,null,r.OperationKey,true);
            }else{
                Output.Require(action is "handover-accept" or "handover-reject","UNKNOWN_ACTION",400);
                var offer=await db.WorkEvents.SingleOrDefaultAsync(e=>e.WorkEventId==r.RelatedId&&e.WorkId==id&&e.EventType=="HANDOVER_OFFERED"&&e.RecipientUserId==actor.Id,ct)??throw new ApiException(403,"HANDOVER_RECIPIENT_REQUIRED");
                if(action=="handover-accept"){
                    await access.Eligible(actor.Id,w.BranchId,ct);Output.Require(a!=null&&a.AssigneeUserId==offer.ActorUserId,"HANDOVER_ASSIGNMENT_CHANGED");
                    await access.EligibleFor(actor.Id,w.BranchId,WorkRules.Permission(w.Kind,"execute"),ct);
                    if(w.Kind is "CLEANING" or "REWORK")await RequireCleaningAssignee(actor.Id,w.BranchId,ct);
                    if(w.Kind=="CLEANING")await supplies.Reserve(id,actor.Id,r.OperationKey,ct);
                    await CloseTime(w,"REASSIGN",ct);now=Now;a!.EndedAt=now;a.AssignmentStatus="REASSIGNED";await db.SaveChangesAsync(ct);
                    db.WorkAssignments.Add(new WorkAssignment{WorkId=id,BranchId=w.BranchId,AssigneeUserId=actor.Id,AssignedBy=actor.Id,AssignedAt=now,AcceptedAt=now,AssignmentStatus="ACTIVE",Reason=r.Reason});
                    if(w.Status=="IN_PROGRESS")w.Status=w.Kind=="OPERATIONAL"?"PAUSED":w.Kind is "CLEANING" or "REWORK" or "MAINTENANCE"?"ON_HOLD":w.Status;
                }
                Event(w,action=="handover-accept"?"HANDOVER_ACCEPTED":"HANDOVER_REJECTED",r.OperationKey,r.Reason,before,null,offer.WorkEventId);
            }
            // Touch the aggregate version even when only its history changed.
            db.Entry(w).Property(x=>x.Title).IsModified=true;await db.SaveChangesAsync(ct);return (object)Output.Record(w);
        } else {
            if(w.Kind is "CLEANING" or "REWORK"&&action is "accept" or "start" or "resume" or "pause" or "complete")
                await RequireExistingCleaningExecution(id,w.BranchId,ct);
            if(w.Kind=="CLEANING"&&action is "accept" or "start"&&a!.AcceptedAt==null)
                await supplies.Reserve(id,actor.Id,r.OperationKey,ct);
            if(action=="start"&&w.Kind is "CLEANING" or "REWORK"){
                if(a!.AcceptedAt==null){a.AcceptedAt=now;if(w.Kind=="REWORK")w.Status="ACCEPTED";}
            }
            var next=WorkRules.Transition(w.Kind,w.Status,action);
            if(w.Kind=="INTERNAL_REQUEST"&&action=="submit"&&a!=null){
                await access.EligibleFor(a.AssigneeUserId,w.BranchId,"IR_PROCESS",ct);
                next="PENDING_ACCEPTANCE";
                notifications.Add(w.BranchId,a.AssigneeUserId,"WORK_ASSIGNED",w.Title,id,null,r.OperationKey,true);
            }
            if(action is "accept" or "start" or "resume" or "pause" or "complete" or "pass" or "fail"){
                Output.Require(a?.AssigneeUserId==actor.Id,"CURRENT_ASSIGNEE_REQUIRED",403);
                if(action=="accept"){Output.Require(a!.AcceptedAt==null,"ALREADY_ACCEPTED");a.AcceptedAt=now;}
                if(action is "start" or "resume"){
                    Output.Require(a!.AcceptedAt!=null,"ACCEPT_ASSIGNMENT_FIRST");
                    db.WorkIntervals.Add(new WorkInterval{BranchId=w.BranchId,WorkId=id,AssignmentId=a.AssignmentId,UserId=actor.Id,OpenedBy=actor.Id,OpenedAt=now,EffectiveStartedAt=now});w.ActualStartAt??=now;
                }
            }
            if(action is "pause" or "reopen" or "cancel" or "reject" or "fail")Output.Reason(r.Reason);
            if(w.Kind=="CLEANING"&&action=="complete")await supplies.Consume(id,r.OperationKey,ct);
            if(w.Kind=="CLEANING"&&action=="cancel")await supplies.Release(id,r.OperationKey,ct);
            if(action is "pause" or "complete" or "cancel" or "pass" or "fail")await CloseTime(w,action=="pause"?"PAUSE":action=="cancel"?"CANCEL":"COMPLETE",ct);
            if(action=="route"){
                Output.Require(r.RelatedId!=null&&await db.Departments.AnyAsync(d=>d.DepartmentId==r.RelatedId&&d.BranchId==w.BranchId&&d.IsActive,ct),"TARGET_DEPARTMENT_REQUIRED",400);w.DepartmentId=r.RelatedId;
            }
            if(action=="verify"){
                if(w.Kind=="INTERNAL_REQUEST")Output.Require(w.CreatedBy==actor.Id,"REQUESTER_CONFIRMATION_REQUIRED",403);
                var operationalSelfReview=w.Kind=="OPERATIONAL"&&await access.HasRole("OPERATION_MANAGER",w.BranchId,ct);
                if(operationalSelfReview&&await db.WorkAssignments.AnyAsync(x=>x.WorkId==id&&x.AssigneeUserId==actor.Id,ct)){
                    Event(w,"OM_OPERATIONAL_SELF_REVIEW",Guid.NewGuid(),r.Reason,w.Status);
                    await db.SaveChangesAsync(ct);
                }
                if(!operationalSelfReview)Output.Require(!await db.WorkAssignments.AnyAsync(x=>x.WorkId==id&&x.AssigneeUserId==actor.Id,ct),"INDEPENDENT_VERIFIER_REQUIRED",403);
                if(w.Kind=="INCIDENT")Output.Require(!await db.WorkItems.AnyAsync(x=>x.ParentWorkId==id&&x.Kind=="MAINTENANCE"&&x.Status!="CLOSED"&&x.Status!="CANCELLED"
                    &&!(x.Status=="COMPLETED"&&x.Result=="PASS")
                    &&!(x.Status=="COMPLETED"&&x.Result=="FAIL"&&db.WorkLinks.Any(l=>l.TargetWorkId==x.WorkId&&l.Relation=="SOURCE"
                        &&db.WorkItems.Any(replacement=>replacement.WorkId==l.SourceWorkId&&replacement.ParentWorkId==id&&replacement.Status=="COMPLETED"&&replacement.Result=="PASS"))),ct),"MAINTENANCE_NOT_VERIFIED");
                w.VerifiedBy=actor.Id;w.VerifiedAt=now;w.ClosedAt=now;
            }
            if(action=="reopen"&&w.Kind=="MAINTENANCE")Output.Require(!await db.WorkAssignments.AnyAsync(x=>x.WorkId==id&&x.AssigneeUserId==actor.Id,ct),"INDEPENDENT_VERIFIER_REQUIRED",403);
            if(action=="complete"){
                if(w.Kind=="MAINTENANCE")Output.Reason(r.Reason);
                if(ExecutionChecklistPolicy.RequiresSnapshot(w.Kind,w.ChecklistTemplateId!=null))
                    Output.Require(w.ChecklistTemplateId!=null&&await db.WorkChecklistItems.AnyAsync(x=>x.WorkId==id&&x.RequiredSnapshot,ct),"EXECUTION_CHECKLIST_REQUIRED");
                Output.Require(!await db.WorkChecklistItems.AnyAsync(x=>x.WorkId==id&&x.RequiredSnapshot&&(x.Outcome==null||(w.Kind!="INSPECTION"&&x.Outcome!="PASS")),ct),"CHECKLIST_INCOMPLETE");
                w.CompletedAt=now;w.CompletionNote=r.Reason;w.ProgressPercent=100;
                if(w.Kind=="MAINTENANCE")w.Result="PASS";
                if(w.Kind is "CLEANING" or "REWORK"&&await CleaningIssues(w.Kind=="REWORK"?w.ParentWorkId!.Value:id).AnyAsync(i=>i.Status!="CLOSED"&&i.Status!="CANCELLED",ct))
                    Event(w,"CLEANING_WITH_INCIDENT",Guid.NewGuid(),"Hoàn thành vệ sinh, phòng có sự cố cần giám sát kiểm tra",w.Status);
            }
            if(action is "pass" or "fail"){
                Output.Require(w.ParentWorkId==null||!await CleaningIssues(w.ParentWorkId.Value).AnyAsync(i=>i.Status!="CLOSED"&&i.Status!="CANCELLED",ct),"CLEANING_INCIDENT_REVIEW_REQUIRED");
                if(action=="pass"){
                    Output.Require(w.ChecklistTemplateId!=null&&await db.WorkChecklistItems.AnyAsync(x=>x.WorkId==id,ct),"INSPECTION_CHECKLIST_REQUIRED",409);
                    Output.Require(!await db.WorkChecklistItems.AnyAsync(x=>x.WorkId==id&&x.RequiredSnapshot&&x.Outcome!="PASS",ct),"CHECKLIST_INCOMPLETE");
                }
                w.Status=next;w.Result=action=="pass"?"PASS":"FAIL";w.VerifiedBy=actor.Id;w.VerifiedAt=now;w.ClosedAt=now;
                var parent=await db.WorkItems.SingleAsync(x=>x.WorkId==w.ParentWorkId,ct);var parentBefore=parent.Status;
                if(action=="fail")Output.Require(await db.InspectionFindings.AnyAsync(f=>f.InspectionWorkId==id&&f.Status=="OPEN",ct),"FINDING_REQUIRED");
                parent.Status=action=="pass"?"COMPLETED":"REWORK_REQUIRED";
                if(action=="fail")foreach(var recipient in await db.WorkAssignments.Where(x=>x.WorkId==parent.WorkId&&x.AssignmentStatus=="ACTIVE").Select(x=>x.AssigneeUserId).ToArrayAsync(ct))
                    notifications.Add(w.BranchId,recipient,"INSPECTION_FAILED",parent.Title,parent.WorkId,null,r.OperationKey,true);
                if(action=="pass"){
                    foreach(var rework in await db.WorkItems.Where(x=>x.ParentWorkId==parent.WorkId&&x.Kind=="REWORK"&&x.Status=="PENDING_REINSPECTION").ToArrayAsync(ct)){rework.Status="COMPLETED";rework.ClosedAt=now;await EndAssignments(rework,now,ct);}
                    parent.ClosedAt=now;await EndAssignments(parent,now,ct);
                }
                Event(parent,action=="pass"?"INSPECTION_PASSED":"INSPECTION_FAILED",Guid.NewGuid(),r.Reason,parentBefore);
            }
            if(action=="reopen"){w.VerifiedAt=null;w.VerifiedBy=null;w.ClosedAt=null;w.CompletedAt=null;w.ProgressPercent=0;
                if(a==null){w.Status=w.Kind=="OPERATIONAL"?"PENDING_ASSIGNMENT":next;next=w.Status;}}
            w.Status=next;
            if(next is "CLOSED" or "CANCELLED" or "PASSED" or "FAILED"||w.Kind=="MAINTENANCE"&&next=="COMPLETED")await EndAssignments(w,now,ct);
        }
        db.Entry(w).Property(x=>x.Title).IsModified=true;
        Event(w,action.ToUpperInvariant(),r.OperationKey,r.Reason,before);
        if(action is "complete" or "fail" or "verify" or "reopen")notifications.Add(w.BranchId,w.CreatedBy,"WORK_"+action.ToUpperInvariant(),w.Title,w.WorkId,null,r.OperationKey);
        if(w.Kind=="MAINTENANCE"&&action is "verify" or "reopen"){
            var recipient=a?.AssigneeUserId??await db.WorkAssignments.Where(x=>x.WorkId==id).OrderByDescending(x=>x.AssignmentId).Select(x=>(long?)x.AssigneeUserId).FirstOrDefaultAsync(ct);
            if(recipient!=null)notifications.Add(w.BranchId,recipient.Value,"MAINTENANCE_"+action.ToUpperInvariant(),w.Title,id,null,r.OperationKey);
        }
        if(action=="complete"&&w.Kind=="OPERATIONAL")await notifications.ForPermission(w.BranchId,WorkRules.Permission(w.Kind,"verify"),"PENDING_VERIFICATION",w.Title,w.WorkId,null,r.OperationKey,ct);
        await db.SaveChangesAsync(ct);
        if(action=="complete"&&w.Kind=="MAINTENANCE")await MaintenanceSucceeded(w,r,ct);
        if(action=="pass"&&w.RoomId!=null)await readiness.Reevaluate(w.RoomId.Value,id,r.OperationKey,"INSPECTION_PASSED",ct);
        return (object)Output.Record(w);
    },ct);
    private async Task EndAssignments(WorkItem w,DateTime now,CancellationToken ct){
        await CloseTime(w,w.Status=="CANCELLED"?"CANCEL":"COMPLETE",ct);
        foreach(var a in await db.WorkAssignments.Where(x=>x.WorkId==w.WorkId&&x.AssignmentStatus=="ACTIVE").ToArrayAsync(ct)){a.AssignmentStatus=w.Status=="CANCELLED"?"CANCELLED":"COMPLETED";a.EndedAt=Now;}
    }
    private async Task CloseTime(WorkItem w,string reason,CancellationToken ct){
        foreach(var t in await db.WorkIntervals.Where(x=>x.WorkId==w.WorkId&&x.ClosedAt==null).ToArrayAsync(ct)){t.ClosedAt=Now;t.EffectiveEndedAt=t.ClosedAt;t.ClosedBy=actor.Id;t.CloseReason=reason;}
        // Guards require time closure to precede status/assignment closure.
        await db.SaveChangesAsync(ct);
    }
    private void Event(WorkItem w,string type,Guid key,string? reason,string? from,long? recipient=null,long? related=null){
        db.WorkEvents.Add(new WorkEvent{BranchId=w.BranchId,WorkId=w.WorkId,ActorUserId=actor.Id,EventType=type,OperationKey=key,FromStatus=from,ToStatus=w.Status,Reason=reason,OccurredAt=Now,RecipientUserId=recipient,RelatedEventId=related,ProgressPercent=w.ProgressPercent});
        audit.Add("Work",w.WorkId,type,w.BranchId,after:new{w.Status,reason},key:key);
    }
    public Task<object> Checklist(long id,long itemId,ChecklistResultRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var w=await Load(id,"execute",ct);Output.Require(w.Status is "IN_PROGRESS" or "INSPECTING","WORK_NOT_EXECUTING");
        Output.Require(await db.WorkAssignments.AnyAsync(a=>a.WorkId==id&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE",ct),"CURRENT_ASSIGNEE_REQUIRED",403);
        var item=await db.WorkChecklistItems.SingleOrDefaultAsync(x=>x.WorkId==id&&x.WorkChecklistItemId==itemId,ct)??throw new ApiException(404,"ITEM_NOT_FOUND");Output.Version(item.RowVersion,r.RowVersion);
        Output.Require(r.Outcome is "PASS" or "FAIL" or "NA","INVALID_OUTCOME",400);Output.Require(!(item.RequiredSnapshot&&r.Outcome=="NA"),"REQUIRED_ITEM_CANNOT_BE_NA",400);
        item.Outcome=r.Outcome;item.Note=r.Note;item.CheckedBy=actor.Id;item.CheckedAt=Now;audit.Add("ChecklistResult",itemId,"CHECK",w.BranchId,after:r);await db.SaveChangesAsync(ct);return (object)Output.Record(item);
    },ct);
    public Task<object> Finding(long id,FindingRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var w=await Load(id,"execute",ct);Output.Version(w.RowVersion,r.RowVersion);Output.Require(w.Kind=="INSPECTION"&&w.Status is "INSPECTING" or "PENDING_CONCLUSION","INSPECTION_REQUIRED");
        Output.Require(await db.WorkAssignments.AnyAsync(a=>a.WorkId==id&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE",ct),"CURRENT_ASSIGNEE_REQUIRED",403);
        var f=new InspectionFinding{BranchId=w.BranchId,InspectionWorkId=id,WorkChecklistItemId=r.WorkChecklistItemId,ReworkWorkId=r.ReworkWorkId,Description=r.Description,SeverityId=r.SeverityId,Status="OPEN"};db.InspectionFindings.Add(f);
        db.Entry(w).Property(x=>x.Title).IsModified=true;await db.SaveChangesAsync(ct);audit.Add("Finding",f.FindingId,"CREATE",w.BranchId);return (object)Output.Record(f);
    },ct);
    public Task<object> ResolveFinding(long id,long findingId,ActionRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var w=await Load(id,"execute",ct);var f=await db.InspectionFindings.SingleOrDefaultAsync(x=>x.InspectionWorkId==id&&x.FindingId==findingId,ct)??throw new ApiException(404,"FINDING_NOT_FOUND");
        await access.Require("DEFECT_RESOLVE",w.BranchId,ct:ct);
        Output.Version(f.RowVersion,r.RowVersion);Output.Reason(r.Reason);Output.Require(w.Status!="PASSED","FINAL_INSPECTION_IMMUTABLE");Output.Require(f.Status=="OPEN","FINDING_ALREADY_RESOLVED");
        if(r.RelatedId!=null)f.ReworkWorkId=r.RelatedId;
        Output.Require(f.ReworkWorkId!=null&&await db.WorkItems.AnyAsync(x=>x.WorkId==f.ReworkWorkId&&x.BranchId==w.BranchId&&x.Kind=="REWORK"&&x.ParentWorkId==w.ParentWorkId&&x.TurnoverNo==w.TurnoverNo&&x.Status=="PENDING_REINSPECTION",ct),"REWORK_NOT_SUBMITTED");
        Output.Require(!await db.WorkAssignments.AnyAsync(x=>x.WorkId==f.ReworkWorkId&&x.AssigneeUserId==actor.Id,ct),"INDEPENDENT_REVIEW_REQUIRED",403);
        f.Status="RESOLVED";f.ResolvedAt=Now;f.ResolutionNote=r.Reason;audit.Add("Finding",findingId,"RESOLVED",w.BranchId);await db.SaveChangesAsync(ct);return (object)Output.Record(f);
    },ct);
    public Task<object> Link(long id,LinkRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var w=await Load(id,"assign",ct);Output.Version(w.RowVersion,r.RowVersion);Output.Require(!WorkRules.Terminal.Contains(w.Status),"WORK_ALREADY_TERMINAL");
        var target=await Load(r.TargetWorkId,"read",ct);Output.Require(target.BranchId==w.BranchId&&target.WorkId!=id,"INVALID_LINK",400);
        if(r.Relation=="DEPENDS_ON"){
            var seen=new HashSet<long>{id};var frontier=new List<long>{target.WorkId};
            while(frontier.Count>0){Output.Require(!frontier.Contains(id),"DEPENDENCY_CYCLE",400);var next=frontier.Where(seen.Add).ToArray();if(next.Length==0)break;frontier=await db.WorkLinks.Where(l=>next.Contains(l.SourceWorkId)&&l.Relation=="DEPENDS_ON").Select(l=>l.TargetWorkId).ToListAsync(ct);}
        }
        db.WorkLinks.Add(new WorkLink{BranchId=w.BranchId,SourceWorkId=id,TargetWorkId=r.TargetWorkId,Relation=r.Relation,CreatedBy=actor.Id});db.Entry(w).Property(x=>x.Title).IsModified=true;
        audit.Add("Work",id,"LINK",w.BranchId,after:r);await db.SaveChangesAsync(ct);return (object)Output.Record(w);
    },ct);
}
