using System.Text.Json;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

// Persisted in the report event; links the correction to the original collected set.
public sealed record MissingDirtySource(long MovementId,long PositionId,decimal Quantity);
internal sealed record MissingCustodyReport(string Kind,long? ItemId,decimal Quantity,bool Reusable,
    MissingDirtySource[]? DirtySources=null);

internal static class ReusableMissingCustody
{
    internal static async Task<(long WorkId,string Status,MissingCustodyReport Data)[]> Reports(
        HotelOpsCompactDbContext db,long branch,long item,CancellationToken ct)
    {
        var rows=await (from e in db.WorkEvents.AsNoTracking() join w in db.WorkItems.AsNoTracking() on e.WorkId equals w.WorkId
            where w.BranchId==branch&&e.EventType=="ROOM_ISSUE_REPORTED"
            select new{w.WorkId,w.Status,e.SnapshotJson}).ToArrayAsync(ct);
        return rows.Select(r=>(r.WorkId,r.Status,Data:JsonSerializer.Deserialize<MissingCustodyReport>(r.SnapshotJson??"{}")))
            .Where(r=>r.Data is {Kind:"MISSING",Reusable:true,DirtySources:not null}&&r.Data.ItemId==item)
            .Select(r=>(r.WorkId,r.Status,r.Data!)).ToArray();
    }

    internal static async Task<decimal> RoomUnavailable(HotelOpsCompactDbContext db,long branch,long room,long item,CancellationToken ct)
    {
        var rows=await (from e in db.WorkEvents.AsNoTracking() join w in db.WorkItems.AsNoTracking() on e.WorkId equals w.WorkId
            where w.BranchId==branch&&w.RoomId==room&&w.Status!="CANCELLED"&&e.EventType=="ROOM_ISSUE_REPORTED"
            select new{w.WorkId,w.Status,e.SnapshotJson}).ToArrayAsync(ct);
        decimal unavailable=0;
        foreach(var row in rows){
            var data=JsonSerializer.Deserialize<MissingCustodyReport>(row.SnapshotJson??"{}");
            if(data is not {Reusable:true}||data.ItemId!=item||data.DirtySources!=null)continue;
            if(data.Kind=="MISSING")unavailable+=data.Quantity;
            else if(data.Kind=="BROKEN"){
                var received=await db.ReusableMovements.Where(m=>m.WorkId==row.WorkId&&m.ItemId==item&&m.Kind=="DAMAGE").SumAsync(m=>(decimal?)m.Quantity,ct)??0;
                unavailable+=Math.Max(0,data.Quantity-received);
            }
        }
        return unavailable;
    }

    internal static async Task<bool> HasPending(HotelOpsCompactDbContext db,long position,CancellationToken ct,long? exceptWork=null)
    {
        var p=await db.ReusablePositions.AsNoTracking().SingleAsync(p=>p.PositionId==position,ct);
        return (await Reports(db,p.BranchId,p.ItemId,ct)).Any(r=>r.WorkId!=exceptWork&&r.Status is not ("CLOSED" or "CANCELLED")
            &&r.Data.DirtySources!.Any(s=>s.PositionId==position));
    }

    internal static async Task<MissingDirtySource[]> Available(HotelOpsCompactDbContext db,long branch,long room,long work,long item,CancellationToken ct)
    {
        var positions=await db.ReusablePositions.AsNoTracking().Where(p=>p.BranchId==branch&&p.ItemId==item).ToArrayAsync(ct);
        var moves=await db.ReusableMovements.AsNoTracking().Where(m=>m.BranchId==branch&&m.ItemId==item).OrderBy(m=>m.ReusableMovementId).ToArrayAsync(ct);
        var reports=await Reports(db,branch,item,ct);
        var corrections=reports.ToDictionary(r=>r.WorkId,r=>r.Data.DirtySources!);
        var lots=Trace(positions,moves,corrections);
        var originIds=moves.Where(m=>m.WorkId==work&&m.Kind=="REPLACEMENT"
            &&positions.Any(p=>p.PositionId==m.SourcePositionId&&p.RoomId==room&&p.Status=="IN_USE"))
            .Select(m=>m.ReusableMovementId).ToHashSet();
        var held=reports.Where(r=>r.Status is not ("CLOSED" or "CANCELLED")).SelectMany(r=>r.Data.DirtySources!).ToArray();
        return lots.Where(l=>originIds.Contains(l.MovementId))
            .Select(l=>l with{Quantity=Math.Max(0,l.Quantity-held.Where(h=>h.MovementId==l.MovementId).Sum(h=>h.Quantity))})
            .Where(l=>l.Quantity>0&&positions.Any(p=>p.PositionId==l.PositionId&&p.Status=="DIRTY"&&p.Quantity>=l.Quantity)).ToArray();
    }

    // A whole DIRTY handover proves custody continuity. A partial unlabelled dispatch
    // cannot prove which task supplied it, so none of that pool is offered as missing.
    internal static MissingDirtySource[] Trace(IReadOnlyCollection<ReusablePosition> positions,IReadOnlyCollection<ReusableMovement> moves,
        IReadOnlyDictionary<long,MissingDirtySource[]> corrections)
    {
        var states=positions.ToDictionary(p=>p.PositionId,p=>p.Status);
        var balances=new Dictionary<long,decimal>();var lots=new List<MissingDirtySource>();
        foreach(var m in moves.OrderBy(m=>m.ReusableMovementId)){
            if(m.SourcePositionId is long source){
                var before=balances.GetValueOrDefault(source);
                if(states.GetValueOrDefault(source)=="DIRTY"){
                    var sourceLots=lots.Where(l=>l.PositionId==source).ToArray();
                    if(m.Kind=="WRITE_OFF"&&m.WorkId is long incident&&corrections.TryGetValue(incident,out var correction)){
                        var needed=m.Quantity;
                        foreach(var c in correction.Where(c=>c.PositionId==source)){
                            var lot=lots.FirstOrDefault(l=>l.MovementId==c.MovementId&&l.PositionId==source);
                            if(lot==null)continue;
                            var take=Math.Min(needed,Math.Min(c.Quantity,lot.Quantity));
                            lots.Remove(lot);if(lot.Quantity>take)lots.Add(lot with{Quantity=lot.Quantity-take});needed-=take;
                        }
                        if(needed>0)lots.RemoveAll(l=>l.PositionId==source);
                    }else{
                        lots.RemoveAll(l=>l.PositionId==source);
                        if(before==m.Quantity&&m.DestinationPositionId is long target&&states.GetValueOrDefault(target)=="DIRTY")
                            lots.AddRange(sourceLots.Select(l=>l with{PositionId=target}));
                    }
                }
                balances[source]=before-m.Quantity;
            }
            if(m.DestinationPositionId is long destination){
                balances[destination]=balances.GetValueOrDefault(destination)+m.Quantity;
                if(states.GetValueOrDefault(destination)=="DIRTY"&&m.Kind=="REPLACEMENT"&&m.WorkId!=null
                    &&m.SourcePositionId is long from&&states.GetValueOrDefault(from)=="IN_USE")
                    lots.Add(new(m.ReusableMovementId,destination,m.Quantity));
            }
        }
        return lots.ToArray();
    }

    internal static MissingDirtySource[] Reserve(MissingDirtySource[] available,decimal quantity)
    {
        Output.Require(quantity>0&&decimal.Truncate(quantity)==quantity&&available.Sum(s=>s.Quantity)>=quantity,
            "MISSING_USED_CUSTODY_NOT_AVAILABLE",409);
        var result=new List<MissingDirtySource>();
        foreach(var source in available.OrderBy(s=>s.MovementId)){
            var take=Math.Min(quantity,source.Quantity);if(take<=0)break;
            result.Add(source with{Quantity=take});quantity-=take;
        }
        return result.ToArray();
    }
}
