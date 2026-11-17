using HotelOps.Api.Data;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

internal static class ShiftHandoverResponsibilities
{
    internal static async Task<UserResponsibilityAssessment> ReadAsync(HotelOpsDbContext db,long user,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction==null)throw new InvalidOperationException("Owning responsibility transaction required.");
        if(!await ShiftHandoverStore.InstalledAsync(db,ct))return new([], ["SHIFT_HANDOVER_STORAGE_REQUIRED"]);
        var pending=await ShiftHandoverStore.Rows(db).Where(h=>h.State=="PENDING_ACKNOWLEDGMENT"&&(h.SenderId==user||h.ReceiverId==user))
            .Select(h=>new{h.Id,h.SenderId,h.ReceiverId}).ToArrayAsync(ct);
        return new(pending.Select(h=>$"SHIFT_HANDOVER_{(h.ReceiverId==user?"RECEIVER":"SENDER")}:{h.Id}").ToArray(),[]);
    }
}
