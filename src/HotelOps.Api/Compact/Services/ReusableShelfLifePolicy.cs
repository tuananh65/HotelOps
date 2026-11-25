using System.Text.Json;
using HotelOps.Api.Compact.Data;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;

// Internal per-item review deadline, not a manufacturer expiry or an ownership write-off.
public static class ReusableShelfLifePolicy
{
    public static bool IsOverdue(DateOnly? deadline,DateOnly today)=>deadline!=null&&today>deadline.Value;
    public static bool RequiresUsableStock(string? source,string destination)=>
        source=="READY"&&(destination is "STAFF_HOLDING" or "IN_USE" or "IN_TRANSIT")
        ||source=="STAFF_HOLDING"&&destination=="IN_USE";
    internal static async Task<DateOnly?> Deadline(HotelOpsCompactDbContext db,long branch,long item,CancellationToken ct)
    {
        var entityKey=$"{branch}:{item}";
        var json=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="ReusableShelfLife"&&a.EntityKey==entityKey&&a.Action=="POLICY_SET")
            .OrderByDescending(a=>a.AuditId).Select(a=>a.AfterJson).FirstOrDefaultAsync(ct);
        if(json==null)return null;
        using var document=JsonDocument.Parse(json);
        return DateOnly.ParseExact(document.RootElement.GetProperty("deadline").GetString()!,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
    }
    internal static async Task<bool> Overdue(HotelOpsCompactDbContext db,long branch,long item,DateTime now,CancellationToken ct)
    {
        var deadline=await Deadline(db,branch,item,ct);
        var timezone=await db.Branches.AsNoTracking().Where(b=>b.BranchId==branch).Select(b=>b.Timezone).SingleAsync(ct);
        var local=TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now,DateTimeKind.Utc),TimeZoneInfo.FindSystemTimeZoneById(timezone));
        return IsOverdue(deadline,DateOnly.FromDateTime(local));
    }
}
