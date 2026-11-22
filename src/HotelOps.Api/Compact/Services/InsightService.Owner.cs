using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class InsightService
{
    // Null means all active branches and requires an actual global grant for this domain.
    // A branch grant must never become global merely because another domain has ALL.
    private async Task<long[]> ReadBranches(string permission,long? branchId,CancellationToken ct)
    {
        await access.Require(permission,branchId,ct:ct);
        return await db.Branches.AsNoTracking().Where(b=>b.IsActive&&(branchId==null||b.BranchId==branchId))
            .OrderBy(b=>b.BranchId).Select(b=>b.BranchId).ToArrayAsync(ct);
    }
    private static (DateTime Start,DateTime End) ReadPeriod(DateOnly from,DateOnly to,string zone)
    {
        Output.Require(from.Year>1&&to.Year<9999&&from<=to&&to.DayNumber-from.DayNumber<366,"REPORT_RANGE_MAX_366_DAYS",400);
        var tz=TimeZoneInfo.FindSystemTimeZoneById(zone);
        return (TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue),tz),
            TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue),tz));
    }
    private static bool OpenForMonitoring(string kind,string status)=>kind=="MAINTENANCE"
        ? status is not "CLOSED" and not "CANCELLED"
        :kind=="INCIDENT"?status is not "CLOSED" and not "CANCELLED":!WorkRules.Terminal.Contains(status);

    public async Task<object> OwnerOverview(long? branchId,DateOnly? from,DateOnly? to,CancellationToken ct,bool report=false)
    {
        var ids=await ReadBranches(report?"REPORT_VIEW":"DASHBOARD_VIEW",branchId,ct);
        var branches=await db.Branches.AsNoTracking().Where(b=>ids.Contains(b.BranchId)).OrderBy(b=>b.Name)
            .Select(b=>new{b.BranchId,b.Code,b.Name,b.Timezone}).ToArrayAsync(ct);
        var now=clock.GetUtcNow().UtcDateTime;
        var today=WorkdayService.LocalDate(now,branches.FirstOrDefault()?.Timezone??"Asia/Ho_Chi_Minh");
        var first=from??today.AddDays(-29);var last=to??today;
        ReadPeriod(first,last,"Asia/Ho_Chi_Minh");
        var kinds=new List<string>();
        foreach(var kind in new[]{"CLEANING","INSPECTION","REWORK","MAINTENANCE","INCIDENT","OPERATIONAL","INTERNAL_REQUEST"})
            if(await access.Allows(WorkRules.Permission(kind,"read"),branchId,ct:ct))kinds.Add(kind);
        var roomsAllowed=await access.Allows("ROOM_VIEW",branchId,ct:ct);
        var assetAllowed=await access.Allows("ASSET_VIEW",branchId,ct:ct);
        var stockAllowed=await access.Allows("INVENTORY_VIEW",branchId,ct:ct);
        var mrAllowed=await access.Allows("MR_VIEW",branchId,ct:ct);
        var summaries=new List<object>();var metricRows=new List<Dictionary<string,int?>>();
        var timeline=new List<object>();var attention=new List<object>();var critical=new List<object>();
        var conditions=new List<(string Condition,int Count)>();var stockItems=new HashSet<long>();
        foreach(var b in branches)
        {
            var period=ReadPeriod(first,last,b.Timezone);
            var m=new Dictionary<string,int?>();
            if(roomsAllowed){
                var rooms=await (from r in db.Rooms.AsNoTracking() join v in db.VRoomReadinesses on r.RoomId equals v.RoomId
                    where r.BranchId==b.BranchId&&r.IsActive select new{v.Readiness,v.IsOccupied,
                        cleaning=db.WorkItems.Where(w=>w.WorkId==r.CurrentCleaningWorkId&&w.RoomId==r.RoomId&&w.TurnoverNo==r.TurnoverNo&&w.Kind=="CLEANING").Select(w=>w.Status).FirstOrDefault(),
                        blocked=v.Readiness=="BLOCKED"}).ToArrayAsync(ct);
                m["rooms"]=rooms.Length;
                m["ready"]=rooms.Count(r=>r.Readiness=="READY"&&r.IsOccupied==false&&!r.blocked);
                m["occupied"]=rooms.Count(r=>r.IsOccupied==true);
                m["needCleaning"]=rooms.Count(r=>r.cleaning==null||r.cleaning is "PENDING_ASSIGNMENT" or "ASSIGNED" or "IN_PROGRESS" or "ON_HOLD" or "REWORK_REQUIRED"||r.Readiness=="REWORK_REQUIRED");
                m["pendingInspection"]=rooms.Count(r=>r.cleaning=="PENDING_INSPECTION");
                m["blocked"]=rooms.Count(r=>r.blocked||r.Readiness=="BLOCKED");
            }
            var work=await db.WorkItems.AsNoTracking().Where(w=>w.BranchId==b.BranchId&&kinds.Contains(w.Kind))
                .Select(w=>new{w.WorkId,w.WorkNo,w.Title,w.Kind,w.Status,w.DueAt,w.CreatedAt,w.CompletedAt,w.SeverityId}).ToArrayAsync(ct);
            foreach(var k in kinds){var open=work.Where(w=>w.Kind==k&&OpenForMonitoring(w.Kind,w.Status)).ToArray();
                m[k]=open.Length;m[k+"_overdue"]=open.Count(w=>w.DueAt<now);}
            if(kinds.Contains("INCIDENT"))m["INCIDENT_unresolved"]=work.Count(w=>w.Kind=="INCIDENT"&&w.Status is not "CLOSED" and not "CANCELLED" and not "RESOLVED");
            m["created"]=work.Count(w=>w.CreatedAt>=period.Start&&w.CreatedAt<period.End);
            m["completed"]=work.Count(w=>w.CompletedAt>=period.Start&&w.CompletedAt<period.End);
            foreach(var day in Enumerable.Range(0,last.DayNumber-first.DayNumber+1).Select(first.AddDays)){
                var created=work.Count(w=>WorkdayService.LocalDate(DateTime.SpecifyKind(w.CreatedAt,DateTimeKind.Utc),b.Timezone)==day);
                var completed=work.Count(w=>w.CompletedAt!=null&&WorkdayService.LocalDate(DateTime.SpecifyKind(w.CompletedAt.Value,DateTimeKind.Utc),b.Timezone)==day);
                timeline.Add(new{b.BranchId,day,created,completed});
            }
            attention.AddRange(work.Where(w=>OpenForMonitoring(w.Kind,w.Status)&&w.DueAt<now).OrderBy(w=>w.DueAt).Take(5)
                .Select(w=>new{b.BranchId,branch=b.Name,w.WorkId,w.WorkNo,w.Title,w.Kind,w.Status,w.DueAt}));
            if(kinds.Contains("INCIDENT")){
                var severe=await db.ReferenceValues.AsNoTracking().Where(r=>r.Kind=="SEVERITY"&&(r.Code=="HIGH"||r.Code=="CRITICAL")).Select(r=>r.ReferenceId).ToArrayAsync(ct);
                critical.AddRange(work.Where(w=>w.Kind=="INCIDENT"&&w.Status!="CLOSED"&&w.Status!="CANCELLED"&&w.SeverityId!=null&&severe.Contains(w.SeverityId.Value))
                    .OrderBy(w=>w.DueAt).Take(5).Select(w=>new{b.BranchId,branch=b.Name,w.WorkId,w.Title,w.Kind,w.Status,w.DueAt}));
            }
            if(assetAllowed){var assets=await db.Assets.AsNoTracking().Where(a=>a.BranchId==b.BranchId&&a.IsActive).GroupBy(a=>a.ConditionCode).Select(g=>new{condition=g.Key,count=g.Count()}).ToArrayAsync(ct);
                m["assets"]=assets.Sum(a=>a.count);conditions.AddRange(assets.Select(a=>(a.condition,a.count)));}
            if(stockAllowed){var stock=await StockRows(b.BranchId,ct);m["stockedItems"]=stock.Select(s=>s.ItemId).Distinct().Count();
                m["lowStock"]=stock.Count(s=>s.MinimumQty!=null&&s.BaseQty<s.MinimumQty);
                m["emptyStock"]=stock.Count(s=>s.BaseQty==0);foreach(var s in stock)stockItems.Add(s.ItemId);}
            if(mrAllowed)m["pendingMaterials"]=await db.InventoryDocuments.AsNoTracking().CountAsync(d=>d.BranchId==b.BranchId&&d.Kind=="MATERIAL_REQUEST"&&d.Status=="PENDING_APPROVAL",ct);
            metricRows.Add(m);summaries.Add(new{b.BranchId,b.Code,b.Name,b.Timezone,metrics=m});
        }
        var metrics=metricRows.SelectMany(m=>m.Keys).Distinct().ToDictionary(key=>key,key=>(int?)metricRows.Sum(m=>m.GetValueOrDefault(key)??0));
        metrics["branches"]=branches.Length;if(stockAllowed)metrics["stockedItems"]=stockItems.Count;
        return new{branchId,from=first,to=last,updatedAt=now,metrics,branches=summaries,timeline,attention,critical,
            assetStates=conditions.GroupBy(c=>c.Condition).Select(g=>new{condition=g.Key,count=g.Sum(c=>c.Count)})};
    }

    public async Task<object> OwnerRooms(long? branchId,CancellationToken ct)
    {
        var ids=await ReadBranches("ROOM_VIEW",branchId,ct);
        return await (from r in db.Rooms.AsNoTracking() join v in db.VRoomReadinesses on r.RoomId equals v.RoomId
            join t in db.RoomTypes on r.RoomTypeId equals t.RoomTypeId join b in db.Branches on r.BranchId equals b.BranchId
            where ids.Contains(r.BranchId)&&r.IsActive orderby b.Name,r.Code
            select new{r.RoomId,r.BranchId,branchName=b.Name,r.Code,r.RoomTypeId,r.LocationId,r.OperationalStatus,v.Readiness,v.IsOccupied,
                cleaningStatus=db.WorkItems.Where(w=>w.WorkId==r.CurrentCleaningWorkId&&w.Kind=="CLEANING"&&w.RoomId==r.RoomId&&w.TurnoverNo==r.TurnoverNo).Select(w=>w.Status).FirstOrDefault(),
                inspectionStatus=db.WorkItems.Where(w=>w.ParentWorkId==r.CurrentCleaningWorkId&&w.Kind=="INSPECTION"&&w.RoomId==r.RoomId&&w.TurnoverNo==r.TurnoverNo&&w.Status!="CANCELLED").OrderByDescending(w=>w.CreatedAt).ThenByDescending(w=>w.WorkId).Select(w=>w.Status).FirstOrDefault(),
                roomTypeName=t.Name,t.Capacity,floor=db.Locations.Where(l=>l.LocationId==r.LocationId).Select(l=>l.Name).FirstOrDefault(),
                isBlocked=v.Readiness=="BLOCKED"}).ToArrayAsync(ct);
    }
    public async Task<object> OwnerAssets(long? branchId,CancellationToken ct)
    {
        var ids=await ReadBranches("ASSET_VIEW",branchId,ct);
        return await (from a in db.Assets.AsNoTracking() join b in db.Branches on a.BranchId equals b.BranchId
            where ids.Contains(a.BranchId) orderby a.Name
            select new{a.AssetId,a.BranchId,branchName=b.Name,a.AssetCode,a.Name,a.ConditionCode,a.SerialNumber,a.IsActive,a.RoomId,a.LocationId}).ToArrayAsync(ct);
    }
    public async Task<object> OwnerStock(long? branchId,CancellationToken ct)
    {
        var ids=await ReadBranches("INVENTORY_VIEW",branchId,ct);var rows=new List<object>();
        foreach(var id in ids)foreach(var s in await StockRows(id,ct))rows.Add(new{id,branchId=id,s.WarehouseId,s.Warehouse,s.ItemId,s.Item,s.Code,s.Unit,s.BaseQty,s.MinimumQty,s.TargetQty,s.LastMovement,s.IsActive});
        return rows;
    }
    public async Task<object> OwnerRoomLinks(long id,CancellationToken ct)
    {
        var room=await db.Rooms.AsNoTracking().SingleOrDefaultAsync(r=>r.RoomId==id,ct)??throw new ApiException(404,"ROOM_NOT_FOUND");
        await access.Require("ROOM_VIEW",room.BranchId,ct:ct);await access.Require("INCIDENT_VIEW",room.BranchId,ct:ct);
        return await db.WorkItems.AsNoTracking().Where(w=>w.RoomId==id&&w.BranchId==room.BranchId&&w.Kind=="INCIDENT")
            .OrderByDescending(w=>w.WorkId).Take(100).Select(w=>new{w.WorkId,w.Title,w.Kind,w.Status,w.DueAt}).ToArrayAsync(ct);
    }
}
