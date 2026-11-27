using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class StaffSupplyService
{
    internal static bool HasValidHistoricalConsumption(IReadOnlyCollection<SupplyReservation> rows,IReadOnlyCollection<StaffSupplyLedger> uses)
        => rows.Count>0&&rows.All(r=>r.Status=="CONSUMED")
            &&rows.All(r=>uses.Where(l=>l.ReservationId==r.ReservationId&&l.BalanceId==r.BalanceId).Sum(l=>l.Quantity)==r.Quantity)
            &&uses.All(l=>rows.Any(r=>r.ReservationId==l.ReservationId&&r.BalanceId==l.BalanceId));

    internal Task<string> ConsumeForAcceptance(long workId,Guid key,CancellationToken ct)=>atomic.Run(async()=>{
        Output.Key(key);await Lock(db,ct);
        var work=await AssessmentWork(workId,ct);
        Output.Require(work.Status is "ASSIGNED" or "IN_PROGRESS" or "ON_HOLD","CLEANING_EXECUTION_REQUIRED");
        await access.Require("CLEANING_EXECUTE",work.BranchId,true,ct);
        await HotelOps.Api.Compact.Security.ShiftCommandAccess.Require(db,actor.Id,work.BranchId,Now,ct);
        Output.Require(await db.WorkAssignments.AnyAsync(a=>a.WorkId==workId&&a.AssignmentStatus=="ACTIVE"&&a.AssigneeUserId==actor.Id,ct),"CURRENT_ASSIGNEE_REQUIRED",403);
        return await ConsumeAcceptedWork(work,actor.Id,key,ct);
    },ct);

    private async Task<string> ConsumeAcceptedWork(WorkItem work,long owner,Guid key,CancellationToken ct,
        StaffSupplyAllocation? recoveryAllocation=null,long? authorizingUserId=null)
    {
        var workId=work.WorkId;
        var rows=await db.SupplyReservations.Where(r=>r.WorkId==workId&&r.Status!="RELEASED").ToArrayAsync(ct);
        var uses=await db.StaffSupplyLedgers.AsNoTracking().Where(l=>l.WorkId==workId&&l.Kind=="USE").ToArrayAsync(ct);
        var ids=rows.Select(r=>r.BalanceId).Concat(uses.Select(l=>l.BalanceId)).Distinct().ToArray();
        var balances=await db.StaffSupplyBalances.Where(b=>ids.Contains(b.BalanceId)).ToDictionaryAsync(b=>b.BalanceId,ct);
        Output.Require(uses.All(l=>l.Quantity>0&&rows.Any(r=>r.ReservationId==l.ReservationId&&r.BalanceId==l.BalanceId&&r.Status=="CONSUMED")),"MATERIAL_CONSUMPTION_INVALID");
        Output.Require(rows.Where(r=>r.Status=="CONSUMED").All(r=>uses.Where(l=>l.ReservationId==r.ReservationId).Sum(l=>l.Quantity)==r.Quantity),"MATERIAL_CONSUMPTION_INVALID");
        foreach(var row in rows){
            var custody=await LoadAllocation(balances[row.BalanceId].AllocationId,ct);
            Output.Require(custody.BranchId==work.BranchId&&row.RoomId==work.RoomId,"SUPPLY_BRANCH_MISMATCH");
            if(row.Status=="RESERVED")Output.Require(custody.UserId==owner&&custody.Status!="CLOSED","SUPPLY_CUSTODY_MISMATCH");
        }
        // A valid posted observation belongs to the previous policy. Preserve it exactly;
        // neither top up immutable historical USE nor require a separate reconciliation.
        var confirmation=await Confirmation(workId,ct);
        if(confirmation!=null){
            var historical=ReadConfirmation(confirmation).Result;
            Output.Require(historical.WorkId==workId&&historical.RoomId==work.RoomId&&historical.Confirmed
                &&rows.All(r=>r.Status=="CONSUMED")
                &&historical.Lines.All(line=>line.RequiredQuantity!=null&&line.RequiredQuantity>=0
                    &&uses.Where(l=>balances[l.BalanceId].ItemId==line.ItemId).Sum(l=>l.Quantity)==line.RequiredQuantity)
                &&uses.All(l=>historical.Lines.Any(line=>line.ItemId==balances[l.BalanceId].ItemId)),"MATERIAL_CONSUMPTION_INVALID");
            return "HISTORICAL_CONSUMPTION";
        }
        if(HasValidHistoricalConsumption(rows,uses))return "HISTORICAL_CONSUMPTION";
        var (activeAllocation,sources)=await AssessmentSources(work,ct);
        var reusableAllocation=recoveryAllocation??activeAllocation;
        if(recoveryAllocation!=null){
            var capacity=await (from room in db.Rooms join type in db.RoomTypes on room.RoomTypeId equals type.RoomTypeId
                where room.RoomId==work.RoomId&&room.BranchId==work.BranchId select type.Capacity).SingleAsync(ct);
            var recoveryBalances=await db.StaffSupplyBalances.Where(b=>b.AllocationId==recoveryAllocation.AllocationId
                &&db.ItemLifecycleProfiles.Any(p=>p.ItemId==b.ItemId&&p.BranchId==work.BranchId&&p.Classification=="CONSUMABLE")).ToArrayAsync(ct);
            sources=recoveryBalances.Select(b=>new AssessmentSource(b,null,b.ItemId,b.UnitId,b.PerGuestQuantity,capacity)).ToArray();
        }
        Output.Require(sources.Length>0,"SUPPLY_CONFIGURATION_REQUIRED");
        var plan=new List<(AssessmentSource Source,StaffSupplyBalance Balance,decimal Quantity)>();
        foreach(var source in sources){
            var consumed=uses.Where(l=>balances[l.BalanceId].ItemId==source.ItemId).Sum(l=>l.Quantity);
            var standard=SupplyMath.Required(source.Capacity,source.PerGuest);
            var remaining=Math.Max(0,standard-consumed);
            if(remaining==0)continue;
            // Resolved reservations and their one USE are immutable in the existing schema.
            Output.Require(!rows.Any(r=>r.Status=="CONSUMED"&&balances[r.BalanceId].ItemId==source.ItemId),"MATERIAL_CONSUMPTION_INVALID");
            var candidates=rows.Where(r=>r.Status=="RESERVED"&&balances[r.BalanceId].ItemId==source.ItemId)
                .Select(r=>balances[r.BalanceId]).Concat(source.Balance==null?[]:new[]{source.Balance}).DistinctBy(b=>b.BalanceId).ToArray();
            StaffSupplyBalance? selected=null;decimal best=0;
            foreach(var balance in candidates){
                if(balance.PerGuestQuantity!=source.PerGuest||balance.UnitId!=source.UnitId)continue;
                var held=rows.Where(r=>r.Status=="RESERVED"&&r.BalanceId==balance.BalanceId).Sum(r=>r.Quantity);
                var available=await AvailableForReservation(balance,held,ct);best=Math.Max(best,available);
                if(available>=remaining){selected=balance;break;}
            }
            if(selected==null){
                var name=await db.InventoryItems.Where(i=>i.ItemId==source.ItemId).Select(i=>i.Name).SingleAsync(ct);
                throw new ApiException(409,"STAFF_SUPPLIES_INSUFFICIENT",$"{name}: cần {remaining}, khả dụng {best}, thiếu {Math.Max(0,remaining-best)}. Yêu cầu bổ sung vật tư UC-153 trước khi nhận công việc.");
            }
            plan.Add((source,selected,remaining));
        }
        if(reusableAllocation!=null){
            var roomCapacity=await (from room in db.Rooms join type in db.RoomTypes on room.RoomTypeId equals type.RoomTypeId
                where room.RoomId==work.RoomId select type.Capacity).SingleAsync(ct);
            await AllocateReusableForTask(reusableAllocation,workId,roomCapacity,key,ct);
        }
        // Release old holds only after the entire kit has passed custody and availability checks.
        foreach(var row in rows.Where(r=>r.Status=="RESERVED"))await ReleaseReservation(row,key,ct,authorizingUserId);
        await db.SaveChangesAsync(ct);
        foreach(var entry in plan){
            var reservation=new SupplyReservation{WorkId=workId,RoomId=work.RoomId!.Value,BalanceId=entry.Balance.BalanceId,
                Capacity=entry.Source.Capacity,PerGuestQuantity=entry.Source.PerGuest,Quantity=entry.Quantity,
                CreatedAt=Now,Status="CONSUMED",ResolvedAt=Now};
            db.SupplyReservations.Add(reservation);entry.Balance.Used+=entry.Quantity;
            await db.SaveChangesAsync(ct);
            Ledger(entry.Balance,"USE",entry.Quantity,key,workId,reservation.ReservationId,authorizingUserId:authorizingUserId);
            await db.SaveChangesAsync(ct);
            var ledger=await db.StaffSupplyLedgers.SingleAsync(l=>l.ReservationId==reservation.ReservationId&&l.Kind=="USE",ct);
            await new StockBatchAccounting(db,clock).Consume(ledger,ct);
        }
        var result=await HistoricalAssessment(work,ct);
        if(result==null){
            Output.Require(plan.Count==0&&sources.All(s=>SupplyMath.Required(s.Capacity,s.PerGuest)==0),"MATERIAL_CONSUMPTION_INVALID");
            var lines=new List<RoomSupplyAssessmentLine>();
            foreach(var source in sources){
                var item=await db.InventoryItems.AsNoTracking().SingleAsync(i=>i.ItemId==source.ItemId,ct);
                var unit=await db.ReferenceValues.AsNoTracking().SingleAsync(u=>u.ReferenceId==source.UnitId&&u.Kind=="UOM",ct);
                lines.Add(new(source.ItemId,item.Name,0,null,0,0,0,source.UnitId,unit.Code,null));
            }
            result=new(workId,work.RoomId!.Value,true,true,null,Convert.ToBase64String(work.RowVersion),
                recoveryAllocation?.AllocationId??sources.FirstOrDefault()?.Balance?.AllocationId,lines);
        }
        var snapshot=new{work.RoomId,items=plan.Select(p=>new{p.Source.ItemId,p.Quantity}),historicalUse=uses.Select(l=>l.LedgerId),result,
            authorizingUserId,recoveredAcceptedWork=recoveryAllocation!=null};
        if(authorizingUserId==null)audit.Add("WorkItem",workId,"SUPPLIES_CONSUMED_AT_ACCEPTANCE",work.BranchId,after:snapshot,key:key);
        else db.AuditLogs.Add(new AuditLog{BranchId=work.BranchId,ActorUserId=null,EntityType="WorkItem",EntityKey=workId.ToString(),
            Action="SUPPLIES_CONSUMED_AT_ACCEPTANCE",OperationKey=key,OccurredAt=Now,AfterJson=System.Text.Json.JsonSerializer.Serialize(snapshot)});
        await db.SaveChangesAsync(ct);return "FULL_NORM_AT_ACCEPTANCE";
    }
    internal async Task<string> ValidateForCompletion(long workId,Guid key,CancellationToken ct)
    {
        Output.Key(key);
        var assessment=await RoomAssessment(workId,ct);
        Output.Require(assessment.Confirmed,"MATERIAL_CONSUMPTION_REQUIRED");
        return "POSTED_CONSUMPTION";
    }

}
