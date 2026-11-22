using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
public sealed partial class InsightService(HotelOpsCompactDbContext db,Access access,Actor actor,TimeProvider clock,NotificationService notifications)
{
    public async Task<object> Administration(CancellationToken ct)
    {
        var users=await access.Allows("USER_VIEW",ct:ct);
        var branches=await access.Allows("MASTER_DATA_MANAGE",ct:ct);
        var roles=await access.Allows("ROLE_MANAGE",ct:ct);
        Output.Require(users||branches||roles,"PERMISSION_DENIED",403);
        return new {
            users=users ? await db.Users.AsNoTracking().Where(u=>u.DeletedAt==null).GroupBy(u=>u.AccountStatus).Select(g=>new{status=g.Key,count=g.Count()}).ToArrayAsync(ct) : null,
            branches=branches ? await db.Branches.AsNoTracking().GroupBy(b=>b.IsActive).Select(g=>new{active=g.Key,count=g.Count()}).ToArrayAsync(ct) : null,
            roles=roles ? (int?)await db.Roles.AsNoTracking().CountAsync(r=>r.IsActive,ct) : null
        };
    }
    public async Task<object> Stock(long branchId,CancellationToken ct)
    {
        return await StockRows(branchId,ct);
    }
    public async Task<object> Dashboard(long branchId,CancellationToken ct){
        await access.Require("DASHBOARD_VIEW",branchId,ct:ct);
        // A dashboard grant does not grant access to the underlying operational domains.
        object? rooms=null,work=null,stock=null;
        if(await access.Allows("ROOM_VIEW",branchId,ct:ct))rooms=await db.VRoomReadinesses.Where(x=>x.BranchId==branchId).GroupBy(x=>new{x.Readiness,x.IsOccupied}).Select(g=>new{g.Key.Readiness,g.Key.IsOccupied,Count=g.Count()}).ToArrayAsync(ct);
        if(await access.Allows("TASK_VIEW",branchId,ct:ct))work=await db.WorkItems.Where(x=>x.BranchId==branchId&&x.Kind=="OPERATIONAL").GroupBy(x=>x.Status).Select(g=>new{Status=g.Key,Count=g.Count()}).ToArrayAsync(ct);
        if(await access.Allows("INVENTORY_VIEW",branchId,ct:ct))stock=await db.VStockBalances.Where(x=>x.BranchId==branchId).Select(x=>new{x.WarehouseId,x.ItemId,x.BaseQty}).ToArrayAsync(ct);
        var now=clock.GetUtcNow().UtcDateTime;
        object? scheduling=null;
        if(await access.Allows("SHIFT_MANAGE",branchId,ct:ct)){
            var zone=await db.Branches.AsNoTracking().Where(b=>b.BranchId==branchId).Select(b=>b.Timezone).SingleAsync(ct);
            var today=WorkdayService.LocalDate(now,zone);
            var start=today.AddDays(-((int)today.DayOfWeek+6)%7);var end=start.AddDays(7);
            var assignments=from a in db.WorkdayAssignments.AsNoTracking() join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId where s.BranchId==branchId select a;
            var week=assignments.Where(a=>a.WorkDate>=start&&a.WorkDate<end&&a.Status!="CANCELLED");
            var shifts=await db.WorkdayShifts.AsNoTracking().Where(s=>s.BranchId==branchId&&s.IsActive).Select(s=>new{s.ShiftId,s.Capacity}).ToArrayAsync(ct);
            var counts=await week.GroupBy(a=>new{a.ShiftId,a.WorkDate}).Select(g=>new{g.Key.ShiftId,g.Key.WorkDate,Count=g.Count()}).ToArrayAsync(ct);
            scheduling=new {weekStart=start,assignedStaff=await week.Select(a=>a.UserId).Distinct().CountAsync(ct),
                workingStaff=await assignments.Where(a=>a.Status=="WORKING").Select(a=>a.UserId).Distinct().CountAsync(ct),
                underfilledShifts=shifts.Sum(s=>Enumerable.Range(0,7).Count(i=>(counts.FirstOrDefault(c=>c.ShiftId==s.ShiftId&&c.WorkDate==start.AddDays(i))?.Count??0)<s.Capacity)),
                assignedSlots=await week.CountAsync(ct),capacity=shifts.Sum(s=>s.Capacity)*7};
        }
        var tasks=await access.Allows("TASK_VIEW",branchId,ct:ct);
        var requests=await access.Allows("IR_VIEW",branchId,ct:ct);
        var materials=await access.Allows("MR_VIEW",branchId,ct:ct);
        var active=db.WorkItems.AsNoTracking().Where(w=>w.BranchId==branchId&&!WorkRules.Terminal.Contains(w.Status));
        var monitoring=new List<object>();
        var attention=new List<object>();
        foreach(var kind in new[]{"CLEANING","INSPECTION","REWORK","MAINTENANCE","INCIDENT","INTERNAL_REQUEST"}){
            if(!await access.Allows(WorkRules.Permission(kind,"read"),branchId,ct:ct))continue;
            var domain=active.Where(w=>w.Kind==kind);
            monitoring.Add(new{kind,active=await domain.CountAsync(ct),overdue=await domain.CountAsync(w=>w.DueAt<now,ct)});
            attention.AddRange(await domain.Where(w=>w.DueAt<now).OrderBy(w=>w.DueAt).ThenBy(w=>w.WorkId).Take(5)
                .Select(w=>new{w.WorkId,w.WorkNo,w.Kind,w.Title,w.Status,w.DueAt}).ToArrayAsync(ct));
        }
        object? assets=null;
        if(await access.Allows("ASSET_VIEW",branchId,ct:ct))assets=await db.Assets.AsNoTracking().Where(a=>a.BranchId==branchId&&a.IsActive)
            .GroupBy(a=>a.ConditionCode).Select(g=>new{condition=g.Key,count=g.Count()}).ToArrayAsync(ct);
        return new{branchId,rooms,work,stock,scheduling,monitoring,attention,assets,operations=new{
            activeTasks=tasks?(int?)await active.CountAsync(w=>w.Kind=="OPERATIONAL",ct):null,
            overdueTasks=tasks?(int?)await active.CountAsync(w=>w.Kind=="OPERATIONAL"&&w.DueAt<now,ct):null,
            pendingRequests=requests?(int?)await active.CountAsync(w=>w.Kind=="INTERNAL_REQUEST"&&(w.Status=="SUBMITTED"||w.Status=="PENDING_ACCEPTANCE"),ct):null,
            pendingMaterials=materials?(int?)await db.InventoryDocuments.AsNoTracking().CountAsync(d=>d.BranchId==branchId&&d.Kind=="MATERIAL_REQUEST"&&d.Status=="PENDING_APPROVAL",ct):null}};
    }
    public async Task<object> Report(string report,long branchId,DateTime? from,DateTime? to,CancellationToken ct){
        await access.Require("REPORT_VIEW",branchId,ct:ct);var start=from??DateTime.UtcNow.AddDays(-30);var end=to??DateTime.UtcNow;
        Output.Require(start<=end&&(end-start).TotalDays<=366,"REPORT_RANGE_MAX_366_DAYS",400);
        if(report=="assets"){
            await access.Require("ASSET_VIEW",branchId,ct:ct);
            return await db.Assets.AsNoTracking().Where(a=>a.BranchId==branchId).OrderBy(a=>a.AssetId).Take(10000)
                .Select(a=>new{a.AssetCode,a.Name,a.ConditionCode,a.IsActive,a.RoomId,a.LocationId,a.WarrantyUntil}).ToArrayAsync(ct);
        }
        if(report=="rooms"){
            await access.Require("ROOM_VIEW",branchId,ct:ct);
            return await (from r in db.Rooms.AsNoTracking() join v in db.VRoomReadinesses on r.RoomId equals v.RoomId
                where r.BranchId==branchId orderby r.Code
                select new{r.Code,r.OperationalStatus,v.Readiness,v.IsOccupied}).Take(10000).ToArrayAsync(ct);
        }
        if(report=="materials"){
            await access.Require("MR_VIEW",branchId,ct:ct);
            return await db.InventoryDocuments.AsNoTracking().Where(d=>d.BranchId==branchId&&d.Kind=="MATERIAL_REQUEST"&&d.CreatedAt>=start&&d.CreatedAt<=end)
                .OrderBy(d=>d.DocumentId).Take(10000).Select(d=>new{d.DocumentId,d.DocumentNo,d.Status,d.CreatedAt,d.ApprovedAt,d.ClosedAt}).ToArrayAsync(ct);
        }
        if(report=="workday"){
            await access.Require("SHIFT_MANAGE",branchId,ct:ct);
            var first=DateOnly.FromDateTime(start);var last=DateOnly.FromDateTime(end);
            return await (from a in db.WorkdayAssignments.AsNoTracking() join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
                join u in db.Users on a.UserId equals u.UserId join d in db.Departments on s.DepartmentId equals d.DepartmentId
                where s.BranchId==branchId&&a.WorkDate>=first&&a.WorkDate<=last orderby a.WorkDate,s.StartMinute,u.FullName
                select new{a.AssignmentId,u.EmployeeCode,u.FullName,department=d.Name,shift=s.Name,a.WorkDate,a.Status,a.StartedAt,a.EndedAt}).Take(10000).ToArrayAsync(ct);
        }
        if(report=="stock"){
            await access.Require("INVENTORY_VIEW",branchId,ct:ct);return (await db.VStockBalances.AsNoTracking().Where(x=>x.BranchId==branchId).Take(10000).ToArrayAsync(ct)).Select(Output.Record);
        }
        if(report=="documents"){
            await access.Require("INVENTORY_VIEW",branchId,ct:ct);
            var rows=await db.VInventoryDocumentTotals.AsNoTracking().Where(x=>x.BranchId==branchId).Take(10000).ToArrayAsync(ct);
            var statuses=await TransferStatusProjection.Read(db,rows.Where(d=>d.Kind=="TRANSFER").Select(d=>d.DocumentId).ToArray(),ct);
            return rows.Select(d=>TransferStatusProjection.Record(d,d.DocumentId,statuses));
        }
        var kind=report switch{"tasks"=>"OPERATIONAL","cleaning"=>"CLEANING","incidents"=>"INCIDENT","maintenance"=>"MAINTENANCE","requests"=>"INTERNAL_REQUEST",_=>throw new ApiException(404,"REPORT_NOT_FOUND")};
        await access.Require(WorkRules.Permission(kind,"read"),branchId,ct:ct);
        return await db.WorkItems.AsNoTracking().Where(w=>w.BranchId==branchId&&w.Kind==kind&&w.CreatedAt>=start&&w.CreatedAt<=end)
            .OrderBy(w=>w.WorkId).Take(10000).Select(w=>new{w.WorkId,w.WorkNo,w.Status,w.Title,w.CreatedAt,w.CompletedAt,w.VerifiedAt,w.DueAt,
                completedSeconds=db.VWorkDurations.Where(t=>t.WorkId==w.WorkId).Sum(t=>t.CompletedSeconds)}).ToArrayAsync(ct);
    }
    public async Task<object> Audit(long branchId,int page,CancellationToken ct,string? search=null){await access.Require("AUDIT_VIEW",branchId,ct:ct);
        var global=await access.Allows("AUDIT_VIEW",ct:ct);
        var query=db.AuditLogs.AsNoTracking().Where(x=>x.BranchId==branchId||(global&&x.BranchId==null));
        if(!string.IsNullOrWhiteSpace(search)){
            var term=search.Trim();query=query.Where(x=>x.EntityType.Contains(term)||x.EntityKey.Contains(term)||x.Action.Contains(term));
        }
        return await query.OrderByDescending(x=>x.AuditId).Skip((Math.Max(1,page)-1)*100).Take(100)
            .Select(x=>new{x.AuditId,x.BranchId,x.ActorUserId,x.EntityType,x.EntityKey,x.Action,x.OccurredAt,x.OperationKey}).ToArrayAsync(ct);}
    public async Task<object> Time(long branchId,long? userId,CancellationToken ct){var user=userId??actor.Id;
        await access.Require("TASK_VIEW",branchId,user==actor.Id,ct);return (await db.WorkIntervals.AsNoTracking().Where(x=>x.BranchId==branchId&&x.UserId==user).OrderByDescending(x=>x.IntervalId).Take(500).ToArrayAsync(ct)).Select(Output.Record);}
}
