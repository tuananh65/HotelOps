using HotelOps.Api.Data;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.PreventiveMaintenance;

public sealed record PreventiveTechnician(long UserId,string Name);
public sealed record PreventiveTechnicianPage(IReadOnlyList<PreventiveTechnician> Items,long? NextAfterId);

internal static class PreventiveTechnicians
{
    internal static async Task<bool> EligibleAsync(HotelOpsDbContext db,long user,long branch,CancellationToken ct)
    {
        if(!await MaintenanceExecutionAccess.GrantsAsync(db,user,branch,ct))return false;
        var read=await MaintenanceReadAccess.ReadAsync(db,user,branch,ct);
        return read.Lead || read.Staff;
    }
    internal static async Task<PreventiveTechnicianPage> ReadAsync(HotelOpsDbContext db,long branch,long afterId,CancellationToken ct)
    {
        if(afterId<0)throw new InternalRequestReadException(400,"INVALID_TECHNICIAN_PAGE");
        var candidates=await db.UserRoles.AsNoTracking().Where(r=>r.BranchId==branch && r.UserId>afterId
            && (r.Role.RoleCode=="MAINTENANCE_STAFF" || r.Role.RoleCode=="MAINTENANCE_LEAD"))
            .Select(r=>new {r.UserId,r.User.FullName}).Distinct().OrderBy(r=>r.UserId).Take(101).ToArrayAsync(ct);
        var items=new List<PreventiveTechnician>();
        foreach(var candidate in candidates.Take(100))
            if(await EligibleAsync(db,candidate.UserId,branch,ct))items.Add(new(candidate.UserId,candidate.FullName));
        return new(items,candidates.Length>100?candidates[99].UserId:null);
    }
}
