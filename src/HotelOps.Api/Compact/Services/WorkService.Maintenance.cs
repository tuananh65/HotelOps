using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    private async Task<ResourceDto[]> MaintenanceRows(Entities.WorkItem[] rows,CancellationToken ct)
    {
        if(rows.Length==0)return [];
        var ids=rows.Select(w=>w.WorkId).ToArray();
        var incidentCompletions=await db.WorkEvents.AsNoTracking().Where(e=>ids.Contains(e.WorkId)&&e.EventType=="CLEANING_WITH_INCIDENT").Select(e=>e.WorkId).Distinct().ToArrayAsync(ct);
        var assetIds=rows.Select(w=>w.AssetId).ToArray();var priorityIds=rows.Select(w=>w.PriorityId).ToArray();var branchIds=rows.Select(w=>w.BranchId).Distinct().ToArray();
        var assignments=await (from a in db.WorkAssignments.AsNoTracking() join u in db.Users on a.AssigneeUserId equals u.UserId
            where ids.Contains(a.WorkId)&&a.AssignmentStatus=="ACTIVE" select new{a.WorkId,u.FullName}).ToArrayAsync(ct);
        var assets=await db.Assets.AsNoTracking().Where(a=>branchIds.Contains(a.BranchId)&&assetIds.Contains(a.AssetId)).Select(a=>new{a.AssetId,a.Name}).ToArrayAsync(ct);
        var priorities=await db.ReferenceValues.AsNoTracking().Where(r=>priorityIds.Contains(r.ReferenceId)).Select(r=>new{r.ReferenceId,r.Name}).ToArrayAsync(ct);
        var roomIds=rows.Select(w=>w.RoomId).ToArray();
        var rooms=await db.Rooms.AsNoTracking().Where(r=>roomIds.Contains(r.RoomId)&&branchIds.Contains(r.BranchId)).Select(r=>new{r.RoomId,r.Code}).ToArrayAsync(ct);
        return rows.Select(w=>new ResourceDto(nameof(Entities.WorkItem),new Dictionary<string,object?>(Output.Record(w).Fields){
            ["assetName"]=assets.FirstOrDefault(a=>a.AssetId==w.AssetId)?.Name,
            ["priorityName"]=priorities.FirstOrDefault(r=>r.ReferenceId==w.PriorityId)?.Name,
            ["roomCode"]=rooms.FirstOrDefault(r=>r.RoomId==w.RoomId)?.Code,
            ["completedWithIncident"]=incidentCompletions.Contains(w.WorkId),
            ["assigneeName"]=string.Join(", ",assignments.Where(a=>a.WorkId==w.WorkId).Select(a=>a.FullName))})).ToArray();
    }
    private async Task<object?> MaintenanceContext(Entities.WorkItem w,CancellationToken ct)
    {
        if(w.Kind is not ("MAINTENANCE" or "INCIDENT"))return null;
        var materials=await access.Allows("MR_VIEW",w.BranchId,ct:ct);
        return new{
            priority=await db.ReferenceValues.AsNoTracking().Where(r=>r.ReferenceId==w.PriorityId).Select(r=>r.Name).SingleOrDefaultAsync(ct),
            severity=await db.ReferenceValues.AsNoTracking().Where(r=>r.ReferenceId==w.SeverityId).Select(r=>r.Name).SingleOrDefaultAsync(ct),
            asset=await db.Assets.AsNoTracking().Where(a=>a.AssetId==w.AssetId).Select(a=>new{a.AssetId,a.AssetCode,a.Name,a.ConditionCode}).SingleOrDefaultAsync(ct),
            assignees=await (from a in db.WorkAssignments.AsNoTracking() join u in db.Users on a.AssigneeUserId equals u.UserId where a.WorkId==w.WorkId orderby a.AssignedAt descending select new{u.FullName,u.EmployeeCode,a.AssignmentStatus,a.AssignedAt,a.AcceptedAt,a.EndedAt}).ToArrayAsync(ct),
            materials=materials?await db.InventoryDocuments.AsNoTracking().Where(d=>d.SourceWorkId==w.WorkId&&d.Kind=="MATERIAL_REQUEST")
                .Select(d=>new{d.DocumentId,d.DocumentNo,d.Kind,d.Status}).ToArrayAsync(ct):null,
            reviews=await (from e in db.WorkEvents.AsNoTracking() join u in db.Users on e.ActorUserId equals u.UserId
                where e.WorkId==w.WorkId&&(e.EventType=="VERIFY"||e.EventType=="REOPEN") orderby e.WorkEventId descending
                select new{e.EventType,e.Reason,e.OccurredAt,actor=u.FullName}).Take(50).ToArrayAsync(ct)};
    }

    public async Task<object> Technicians(long branchId,CancellationToken ct)
    {
        await RequireWorkMutation("MAINTENANCE",branchId,ct);
        await access.Require("MAINTENANCE_ASSIGN",branchId,ct:ct);
        var candidates=await CandidateRows(branchId,"MAINTENANCE",ct);
        var zone=await db.Branches.AsNoTracking().Where(b=>b.BranchId==branchId).Select(b=>b.Timezone).SingleAsync(ct);
        var today=WorkdayService.LocalDate(clock.GetUtcNow(),zone);
        var ids=candidates.Select(c=>c.UserId).ToArray();
        var shifts=await (from a in db.WorkdayAssignments.AsNoTracking() join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            where s.BranchId==branchId&&ids.Contains(a.UserId)&&a.WorkDate==today&&a.Status!="CANCELLED"
            select new{a.UserId,a.Status,s.Name,s.StartMinute,s.EndMinute}).ToArrayAsync(ct);
        var work=await (from a in db.WorkAssignments.AsNoTracking() join w in db.WorkItems on a.WorkId equals w.WorkId
            where w.BranchId==branchId&&w.Kind=="MAINTENANCE"&&ids.Contains(a.AssigneeUserId)
            select new{userId=a.AssigneeUserId,a.AssignmentStatus,w.WorkId,w.Status,w.CompletedAt}).ToArrayAsync(ct);
        var busy=await db.WorkIntervals.AsNoTracking().Where(t=>t.BranchId==branchId&&ids.Contains(t.UserId)&&t.ClosedAt==null).Select(t=>t.UserId).Distinct().ToArrayAsync(ct);
        return new{today,timezone=zone,items=candidates.Select(c=>new{c.UserId,c.EmployeeCode,c.FullName,accountStatus="ACTIVE",
            shifts=shifts.Where(s=>s.UserId==c.UserId),busy=busy.Contains(c.UserId),
            open=work.Where(w=>w.userId==c.UserId&&w.AssignmentStatus=="ACTIVE"&&!WorkRules.Terminal.Contains(w.Status)).Select(w=>w.WorkId).Distinct().Count(),
            completed=work.Where(w=>w.userId==c.UserId&&w.CompletedAt!=null&&(w.AssignmentStatus=="ACTIVE"||w.AssignmentStatus=="COMPLETED")).Select(w=>w.WorkId).Distinct().Count()})};
    }

    public Task<object> FromIncident(long id,MaintenanceFromIncidentRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var source=await Load(id,"read",ct);await access.Require("MAINTENANCE_ASSIGN",source.BranchId,ct:ct);
        await RequireWorkMutation("MAINTENANCE",source.BranchId,ct);
        Output.Version(source.RowVersion,r.RowVersion);Output.Key(r.OperationKey);Output.Reason(r.Reason);
        Output.Require(source.Kind=="INCIDENT"&&!WorkRules.Terminal.Contains(source.Status),"ACTIVE_INCIDENT_REQUIRED");
        Output.Require(!await db.WorkEvents.AnyAsync(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==r.OperationKey,ct),"OPERATION_ALREADY_APPLIED");
        Output.Require(!await db.WorkItems.AnyAsync(w=>w.ParentWorkId==id&&w.Kind=="MAINTENANCE"&&w.Status!="CLOSED"&&w.Status!="CANCELLED",ct),"MAINTENANCE_ALREADY_ACTIVE");
        var created=await Create(new CreateWorkRequest(source.BranchId,"MAINTENANCE",r.Title,r.Reason,source.DepartmentId,source.RoomId,source.LocationId,source.AssetId,id,null,source.PriorityId,source.SeverityId,source.CategoryId,r.DueAt??source.DueAt),ct);
        Event(source,"MAINTENANCE_CREATED",r.OperationKey,r.Reason,source.Status);db.Entry(source).Property(w=>w.Title).IsModified=true;await db.SaveChangesAsync(ct);
        return created;
    },ct);
}
