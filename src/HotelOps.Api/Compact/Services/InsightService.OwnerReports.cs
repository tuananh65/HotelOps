using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class InsightService
{
    private sealed record MonitoringRow(long Id,long BranchId,string Branch,string Code,string Title,string Kind,string Status,
        DateTime? CreatedAt=null,DateTime? CompletedAt=null,DateTime? VerifiedAt=null,DateTime? DueAt=null,decimal? Quantity=null,string? Unit=null);

    private static async Task<object> ReadPage<T>(IQueryable<T> query,int page,CancellationToken ct)
    {
        page=Math.Max(1,page);const int size=25;
        var count=await query.CountAsync(ct);var rows=await query.Skip((page-1)*size).Take(size).ToArrayAsync(ct);
        return new PageDto<T>(rows,count,page,size);
    }
    public async Task<object> OwnerReport(string report,long? branchId,DateOnly? from,DateOnly? to,string? search,string? status,string sort,int page,CancellationToken ct)
    {
        var ids=await ReadBranches("REPORT_VIEW",branchId,ct);
        var today=WorkdayService.LocalDate(clock.GetUtcNow().UtcDateTime,"Asia/Ho_Chi_Minh");
        var first=from??today.AddDays(-29);var last=to??today;
        var period=ReadPeriod(first,last,"Asia/Ho_Chi_Minh");
        Output.Require(sort is "latest" or "oldest" or "title","INVALID_REPORT_SORT",400);
        var term=search?.Trim()??"";page=Math.Max(1,page);
        if(report=="overview")return await OwnerOverview(branchId,first,last,ct,true);
        if(report=="staffing")return await OwnerStaffing(ids,branchId,first,last,ct);
        if(report=="stock"){
            await access.Require("INVENTORY_VIEW",branchId,ct:ct);
            var rows=new List<MonitoringRow>();
            foreach(var id in ids){var name=await db.Branches.AsNoTracking().Where(b=>b.BranchId==id).Select(b=>b.Name).SingleAsync(ct);
                foreach(var s in await StockRows(id,ct))rows.Add(new(s.ItemId,id,name,s.Code,s.Item+" · "+s.Warehouse,"STOCK",
                    s.BaseQty==0?"EMPTY":s.MinimumQty!=null&&s.BaseQty<s.MinimumQty?"LOW":"IN_STOCK",DueAt:s.LastMovement,Quantity:s.BaseQty,Unit:s.Unit));}
            var filtered=rows.Where(r=>(string.IsNullOrEmpty(status)||r.Status==status)&&(r.Code+" "+r.Title).Contains(term,StringComparison.OrdinalIgnoreCase));
            var ordered=sort=="oldest"?filtered.OrderBy(r=>r.Id):sort=="title"?filtered.OrderBy(r=>r.Title):filtered.OrderByDescending(r=>r.Id);
            return new PageDto<MonitoringRow>(ordered.Skip((page-1)*25).Take(25).ToArray(),filtered.Count(),page,25);
        }
        if(report=="rooms"){
            await access.Require("ROOM_VIEW",branchId,ct:ct);
            var query=from r in db.Rooms.AsNoTracking() join v in db.VRoomReadinesses on r.RoomId equals v.RoomId join b in db.Branches on r.BranchId equals b.BranchId
                where ids.Contains(r.BranchId)&&r.IsActive&&(term==""||r.Code.Contains(term))&&(status==null||status==""||(status=="OCCUPIED"?v.IsOccupied==true:status=="RESERVED"?r.OperationalStatus=="RESERVED":status=="BLOCKED"?v.Readiness=="BLOCKED":r.OperationalStatus!="RESERVED"&&v.IsOccupied==false&&v.Readiness==status))
                select new{r.RoomId,r.BranchId,b.Name,r.Code,v.Readiness,v.IsOccupied,r.OperationalStatus};
            var ordered=sort=="oldest"?query.OrderBy(r=>r.RoomId):sort=="title"?query.OrderBy(r=>r.Code):query.OrderByDescending(r=>r.RoomId);
            return await ReadPage(ordered.Select(r=>new MonitoringRow(r.RoomId,r.BranchId,r.Name,r.Code,"Phòng "+r.Code,"ROOM",r.OperationalStatus=="RESERVED"?"RESERVED":r.IsOccupied==true?"OCCUPIED":r.Readiness??"UNKNOWN",null,null,null,null,null,null)),page,ct);
        }
        if(report=="assets"){
            await access.Require("ASSET_VIEW",branchId,ct:ct);
            var query=from a in db.Assets.AsNoTracking() join b in db.Branches on a.BranchId equals b.BranchId where ids.Contains(a.BranchId)
                &&(term==""||a.AssetCode.Contains(term)||a.Name.Contains(term))&&(status==null||status==""||a.ConditionCode==status) select new{a,b.Name};
            var ordered=sort=="oldest"?query.OrderBy(x=>x.a.AssetId):sort=="title"?query.OrderBy(x=>x.a.Name):query.OrderByDescending(x=>x.a.AssetId);
            return await ReadPage(ordered.Select(x=>new MonitoringRow(x.a.AssetId,x.a.BranchId,x.Name,x.a.AssetCode,x.a.Name,"ASSET",x.a.ConditionCode,x.a.CreatedAt,null,null,null,null,null)),page,ct);
        }
        if(report=="preventive"){
            await access.Require("MAINTENANCE_VIEW",branchId,ct:ct);
            var query=from t in db.WorkTemplates.AsNoTracking() join b in db.Branches on t.BranchId equals b.BranchId
                where ids.Contains(t.BranchId)&&t.Kind=="MAINTENANCE"&&(term==""||t.Code.Contains(term)||t.Title.Contains(term))&&(status==null||status==""||(status=="ACTIVE"?t.IsActive:!t.IsActive)) select new{t,b.Name};
            var ordered=sort=="oldest"?query.OrderBy(x=>x.t.WorkTemplateId):sort=="title"?query.OrderBy(x=>x.t.Title):query.OrderByDescending(x=>x.t.WorkTemplateId);
            return await ReadPage(ordered.Select(x=>new MonitoringRow(x.t.WorkTemplateId,x.t.BranchId,x.Name,x.t.Code,x.t.Title,"PREVENTIVE",x.t.IsActive?"ACTIVE":"INACTIVE",null,null,null,x.t.NextDueAt,null,null)),page,ct);
        }
        if(report is "materials" or "documents"){
            await access.Require(report=="materials"?"MR_VIEW":"INVENTORY_VIEW",branchId,ct:ct);
            var mr=await access.Allows("MR_VIEW",branchId,ct:ct);var replenishment=await access.Allows("REPLENISHMENT_VIEW",branchId,ct:ct);
            var query=from d in db.InventoryDocuments.AsNoTracking() join b in db.Branches on d.BranchId equals b.BranchId where ids.Contains(d.BranchId)
                &&(report=="materials"?d.Kind=="MATERIAL_REQUEST":(d.Kind!="MATERIAL_REQUEST"||mr)&&(d.Kind!="REPLENISHMENT"||replenishment))&&d.CreatedAt>=period.Start&&d.CreatedAt<period.End
                &&(term==""||d.DocumentNo.Contains(term)||(d.Reason!=null&&d.Reason.Contains(term)))&&(status==null||status==""||d.Status==status) select new{d,b.Name};
            var ordered=sort=="oldest"?query.OrderBy(x=>x.d.DocumentId):sort=="title"?query.OrderBy(x=>x.d.DocumentNo):query.OrderByDescending(x=>x.d.DocumentId);
            return await ReadPage(ordered.Select(x=>new MonitoringRow(x.d.DocumentId,x.d.BranchId,x.Name,x.d.DocumentNo,x.d.Reason??x.d.DocumentNo,x.d.Kind,x.d.Status,x.d.CreatedAt,x.d.PostedAt,x.d.ApprovedAt,null,null,null)),page,ct);
        }
        var kind=report switch{"tasks"=>"OPERATIONAL","cleaning"=>"CLEANING","inspection"=>"INSPECTION","maintenance"=>"MAINTENANCE","incidents"=>"INCIDENT","requests"=>"INTERNAL_REQUEST",_=>throw new ApiException(404,"REPORT_NOT_FOUND")};
        await access.Require(WorkRules.Permission(kind,"read"),branchId,ct:ct);
        var work=from w in InternalRequestVisibility.Apply(db.WorkItems.AsNoTracking(),db,actor.Id,clock.GetUtcNow().UtcDateTime) join b in db.Branches on w.BranchId equals b.BranchId
            where ids.Contains(w.BranchId)&&w.Kind==kind&&w.CreatedAt>=period.Start&&w.CreatedAt<period.End
                &&(term==""||w.Title.Contains(term)||w.WorkNo.Contains(term))&&(status==null||status==""||w.Status==status) select new{w,b.Name};
        var sorted=sort=="oldest"?work.OrderBy(x=>x.w.WorkId):sort=="title"?work.OrderBy(x=>x.w.Title):work.OrderByDescending(x=>x.w.WorkId);
        return await ReadPage(sorted.Select(x=>new MonitoringRow(x.w.WorkId,x.w.BranchId,x.Name,x.w.WorkNo,x.w.Title,x.w.Kind,x.w.Status,x.w.CreatedAt,x.w.CompletedAt,x.w.VerifiedAt,x.w.DueAt,null,null)),page,ct);
    }
    private async Task<object> OwnerStaffing(long[] ids,long? branchId,DateOnly from,DateOnly to,CancellationToken ct)
    {
        // Only aggregates necessary for operational reporting. No User Management or attendance writes.
        var now=clock.GetUtcNow().UtcDateTime;
        var rows=new List<object>();
        var workKinds=new List<string>();foreach(var kind in new[]{"CLEANING","MAINTENANCE","OPERATIONAL"})
            if(await access.Allows(WorkRules.Permission(kind,"read"),branchId,ct:ct))workKinds.Add(kind);
        var departments=await (from d in db.Departments.AsNoTracking() join b in db.Branches on d.BranchId equals b.BranchId
            where ids.Contains(d.BranchId)&&d.IsActive orderby b.Name,d.Name select new{d.DepartmentId,d.BranchId,branch=b.Name,d.Name,b.Timezone}).ToArrayAsync(ct);
        foreach(var d in departments){
            var shifts=await db.WorkdayShifts.AsNoTracking().Where(s=>s.BranchId==d.BranchId&&s.DepartmentId==d.DepartmentId&&s.IsActive).Select(s=>new{s.ShiftId,s.Capacity}).ToArrayAsync(ct);
            var shiftIds=shifts.Select(s=>s.ShiftId).ToArray();
            var assignments=await db.WorkdayAssignments.AsNoTracking().Where(a=>shiftIds.Contains(a.ShiftId)&&a.WorkDate>=from&&a.WorkDate<=to&&a.Status!="CANCELLED")
                .Select(a=>new{a.ShiftId,a.WorkDate,a.UserId,a.Status}).ToArrayAsync(ct);
            var capacity=shifts.Sum(s=>s.Capacity)*(to.DayNumber-from.DayNumber+1);
            var p=ReadPeriod(from,to,d.Timezone);
            rows.Add(new{d.DepartmentId,d.BranchId,d.branch,department=d.Name,
                employees=await (from m in db.Memberships.AsNoTracking() join u in db.Users on m.UserId equals u.UserId where m.BranchId==d.BranchId&&m.DepartmentId==d.DepartmentId&&m.Kind=="EMPLOYMENT"&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now)&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null select m.UserId).Distinct().CountAsync(ct),
                assignedStaff=assignments.Select(a=>a.UserId).Distinct().Count(),assignedSlots=assignments.Length,capacity,
                underfilledShifts=shifts.Sum(s=>Enumerable.Range(0,to.DayNumber-from.DayNumber+1).Count(i=>assignments.Count(a=>a.ShiftId==s.ShiftId&&a.WorkDate==from.AddDays(i))<s.Capacity)),
                completedTasks=await db.WorkItems.AsNoTracking().CountAsync(w=>w.BranchId==d.BranchId&&w.DepartmentId==d.DepartmentId&&workKinds.Contains(w.Kind)&&w.CompletedAt>=p.Start&&w.CompletedAt<p.End,ct)});
        }
        return new{from,to,items=rows};
    }
    public async Task<object> OwnerPreventive(long id,CancellationToken ct)
    {
        var t=await db.WorkTemplates.AsNoTracking().Where(t=>t.WorkTemplateId==id&&t.Kind=="MAINTENANCE")
            .Select(t=>new{t.WorkTemplateId,t.BranchId,t.Code,t.Title,t.Instruction,t.Frequency,t.FrequencyInterval,t.NextDueAt,t.IsActive}).SingleOrDefaultAsync(ct)??throw new ApiException(404,"TEMPLATE_NOT_FOUND");
        await access.Require("REPORT_VIEW",t.BranchId,ct:ct);await access.Require("MAINTENANCE_VIEW",t.BranchId,ct:ct);
        return new{template=t,occurrences=await db.WorkItems.AsNoTracking().Where(w=>w.WorkTemplateId==id&&w.BranchId==t.BranchId&&w.Kind=="MAINTENANCE")
            .OrderByDescending(w=>w.OccurrenceAt).Take(100).Select(w=>new{w.WorkId,w.Title,w.Status,w.DueAt,w.CompletedAt,w.VerifiedAt}).ToArrayAsync(ct)};
    }
}
