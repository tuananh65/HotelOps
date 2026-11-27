using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class StaffSupplyService
{
    internal Task Release(long workId,Guid key,CancellationToken ct)=>atomic.Run(async()=>{
        Output.Key(key);await Lock(db,ct);
        var reservations=await db.SupplyReservations.Where(x=>x.WorkId==workId&&x.Status=="RESERVED").ToArrayAsync(ct);
        foreach(var reservation in reservations)await ReleaseReservation(reservation,key,ct);
        await db.SaveChangesAsync(ct);return true;
    },ct);

    private Task<decimal> AvailableForReservation(StaffSupplyBalance balance,decimal held,CancellationToken ct)
    {
        Output.Require(balance.Reserved>=held,"SUPPLY_BALANCE_INCONSISTENT");
        // Detached accounting view: do not mutate tracked custody while checking expiry.
        var available=new StaffSupplyBalance{BalanceId=balance.BalanceId,InitialIssued=balance.InitialIssued,
            AdditionalIssued=balance.AdditionalIssued,Used=balance.Used,Returned=balance.Returned,
            ReclassifiedQuantity=balance.ReclassifiedQuantity,Reserved=balance.Reserved-held};
        return StockBatchAccounting.StaffAvailable(db,available,Now,ct);
    }

    private async Task ReleaseReservation(SupplyReservation reservation,Guid key,CancellationToken ct,long? authorizingUserId=null)
    {
        var b=await db.StaffSupplyBalances.SingleAsync(x=>x.BalanceId==reservation.BalanceId,ct);
        Output.Require(b.Reserved>=reservation.Quantity,"SUPPLY_BALANCE_INCONSISTENT");
        b.Reserved-=reservation.Quantity;reservation.Status="RELEASED";reservation.ResolvedAt=Now;
        if(reservation.Quantity>0)Ledger(b,"RELEASE",reservation.Quantity,key,reservation.WorkId,reservation.ReservationId,authorizingUserId:authorizingUserId);
    }
}
