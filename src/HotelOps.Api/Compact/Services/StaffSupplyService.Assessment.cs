using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class StaffSupplyService
{
    private async Task<Dictionary<long,AssessRoomSupplyLine>> AssessmentSnapshot(long workId,CancellationToken ct)
    {
        var json=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="WorkItem"&&a.EntityKey==workId.ToString()&&a.Action=="SUPPLIES_ASSESSED")
            .OrderByDescending(a=>a.AuditId).Select(a=>a.AfterJson).FirstOrDefaultAsync(ct);
        return json==null?[]:(System.Text.Json.JsonSerializer.Deserialize<AssessRoomSupplyLine[]>(json,new System.Text.Json.JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[]).ToDictionary(x=>x.ReservationId);
    }
    private async Task<WorkItem> AssessmentWork(long id,bool edit,CancellationToken ct)
    {
        var work=await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(x=>x.WorkId==id,ct)??throw new ApiException(404,"WORK_NOT_FOUND");
        var owns=await db.WorkAssignments.AnyAsync(x=>x.WorkId==id&&x.AssigneeUserId==actor.Id&&x.AssignmentStatus=="ACTIVE",ct);
        await access.Require("CLEANING_VIEW",work.BranchId,owns,ct);
        Output.Require(work.Kind=="CLEANING","CLEANING_WORK_REQUIRED",400);
        if(edit){
            await HotelOps.Api.Compact.Security.ShiftCommandAccess.Require(db,actor.Id,work.BranchId,Now,ct);
            Output.Require(owns,"ASSIGNEE_REQUIRED",403);
            await access.Require("CLEANING_EXECUTE",work.BranchId,true,ct);
            Output.Require(work.Status=="IN_PROGRESS","WORK_NOT_IN_PROGRESS");
        }
        return work;
    }

    public async Task<IReadOnlyList<RoomSupplyAssessmentLine>> RoomAssessment(long workId,CancellationToken ct)
    {
        await AssessmentWork(workId,false,ct);
        var snapshot=await AssessmentSnapshot(workId,ct);
        var rows=await (from r in db.SupplyReservations.AsNoTracking()
            join b in db.StaffSupplyBalances on r.BalanceId equals b.BalanceId
            join i in db.InventoryItems on b.ItemId equals i.ItemId
            where r.WorkId==workId&&r.Status!="RELEASED"&&db.ItemLifecycleProfiles.Any(p=>p.ItemId==b.ItemId&&p.Classification=="CONSUMABLE"&&db.StaffSupplyAllocations.Any(a=>a.AllocationId==b.AllocationId&&a.BranchId==p.BranchId)) orderby r.ReservationId
            select new{r,b.ItemId,i.Name}).ToArrayAsync(ct);
        return rows.Select(x=>new RoomSupplyAssessmentLine(x.r.ReservationId,x.ItemId,x.Name,
            SupplyMath.Required(x.r.Capacity,x.r.PerGuestQuantity),x.r.UsableQuantity,x.r.Quantity,x.r.AssessedAt,
            Convert.ToBase64String(x.r.RowVersion),snapshot.GetValueOrDefault(x.r.ReservationId)?.CurrentQuantity,
            snapshot.GetValueOrDefault(x.r.ReservationId)?.UnusableQuantity,snapshot.GetValueOrDefault(x.r.ReservationId)?.ExpiredQuantity,
            snapshot.GetValueOrDefault(x.r.ReservationId)?.ReplenishedQuantity,snapshot.GetValueOrDefault(x.r.ReservationId)?.Note)).ToArray();
    }

    public Task<IReadOnlyList<RoomSupplyAssessmentLine>> AssessRoom(long workId,AssessRoomSuppliesRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        Output.Key(request.OperationKey);await Lock(db,ct);
        var work=await AssessmentWork(workId,true,ct);
        var payload=System.Text.Json.JsonSerializer.Serialize(request.Lines.OrderBy(x=>x.ReservationId));
        var prior=await db.AuditLogs.SingleOrDefaultAsync(x=>x.OperationKey==request.OperationKey&&x.ActorUserId==actor.Id&&x.EntityType=="WorkItem"&&x.EntityKey==workId.ToString()&&x.Action=="SUPPLIES_ASSESSED",ct);
        if(prior!=null){Output.Require(prior.AfterJson==payload,"IDEMPOTENCY_KEY_REUSED");return await RoomAssessment(workId,ct);}
        await ReleaseReclassifiedReservations(work,request.OperationKey,ct);
        var rows=await db.SupplyReservations.Where(x=>x.WorkId==workId&&x.Status=="RESERVED").ToArrayAsync(ct);
        Output.Require(rows.Length>0&&request.Lines.Length==rows.Length&&request.Lines.Select(x=>x.ReservationId).Distinct().Count()==rows.Length,"ASSESSMENT_ALL_ITEMS_REQUIRED",400);
        foreach(var row in rows){
            var input=request.Lines.SingleOrDefault(x=>x.ReservationId==row.ReservationId)??throw new ApiException(400,"ASSESSMENT_ITEM_MISMATCH");
            Output.Version(row.RowVersion,input.RowVersion);
            var missing=RoomSupplyAssessmentRules.Validate(SupplyMath.Required(row.Capacity,row.PerGuestQuantity),input);
            var balance=await db.StaffSupplyBalances.SingleAsync(x=>x.BalanceId==row.BalanceId,ct);
            var allocation=await LoadAllocation(balance.AllocationId,ct);
            Output.Require(allocation.UserId==actor.Id,"SUPPLY_OWNER_REQUIRED",403);
            await AllocationActive(allocation,ct);
            var delta=missing-row.Quantity;
            Output.Require(delta<=await StockBatchAccounting.StaffAvailable(db,balance,Now,ct),"STAFF_SUPPLIES_INSUFFICIENT");
            balance.Reserved+=delta;
            row.Quantity=missing;row.UsableQuantity=input.UsableQuantity;row.AssessedAt=Now;row.AssessedBy=actor.Id;
            if(delta!=0)Ledger(balance,delta>0?"RESERVE":"RELEASE",Math.Abs(delta),request.OperationKey,workId,row.ReservationId);
        }
        audit.Add("WorkItem",workId,"SUPPLIES_ASSESSED",work.BranchId,after:request.Lines.OrderBy(x=>x.ReservationId),key:request.OperationKey);
        await db.SaveChangesAsync(ct);
        return await RoomAssessment(workId,ct);
    },ct);
}
