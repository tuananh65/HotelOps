using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

internal static class FixtureRunSafety
{
    internal static bool ValidMarker(string marker)=>Regex.IsMatch(marker,"^D2A[0-9a-f]{10}$",RegexOptions.CultureInvariant);
    internal static async Task Write(string path,object value)
    {var temporary=path+".pending";await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}),CancellationToken.None);File.Move(temporary,path,true);}
    internal static async Task Lease(HotelOpsDbContext db,CancellationToken ct=default)
    {
        await db.Database.OpenConnectionAsync(ct);await HostAcceptance.VerifyIdentity(db);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:DEV2:FixtureHarness',@LockMode=N'Exclusive',@LockOwner=N'Session',@LockTimeout=0; IF @r<0 THROW 51000,'Fixture harness lease unavailable',1;",ct);
    }
    internal static async Task<string[]> Orphans(HotelOpsDbContext db,CancellationToken ct=default)
    {
        var candidates=new List<string>();
        candidates.AddRange(await db.Users.AsNoTracking().Where(x=>x.Username.StartsWith("D2A")).Select(x=>x.Username).ToListAsync(ct));
        candidates.AddRange(await db.Branches.AsNoTracking().Where(x=>x.BranchCode.StartsWith("D2A")).Select(x=>x.BranchCode).ToListAsync(ct));
        candidates.AddRange(await db.Roles.AsNoTracking().Where(x=>x.RoleCode.StartsWith("D2A")).Select(x=>x.RoleCode).ToListAsync(ct));
        candidates.AddRange(await db.ItemCategories.AsNoTracking().Where(x=>x.CategoryCode.StartsWith("D2A")).Select(x=>x.CategoryCode).ToListAsync(ct));
        candidates.AddRange(await db.ReferenceValues.AsNoTracking().Where(x=>x.ValueCode.StartsWith("D2A")).Select(x=>x.ValueCode).ToListAsync(ct));
        return candidates.Select(x=>Regex.Match(x,"^(D2A[0-9a-f]{10})(?:u[0-6]|[AB]|MAIN|OWN)?$",RegexOptions.CultureInvariant)).Where(x=>x.Success).Select(x=>x.Groups[1].Value).Distinct().Order(StringComparer.Ordinal).ToArray();
    }
    internal static async Task Gate(HotelOpsDbContext db,string output,CancellationToken ct=default)
    {
        await Lease(db,ct);var markers=await Orphans(db,ct);
        await Write(Path.Combine(output,"ORPHAN_PREFLIGHT.json"),new{status=markers.Length==0?"PASS":"BLOCKED_ORPHANS",markers,fixtureCreationAllowed=markers.Length==0,databaseGuid=HostAcceptance.DatabaseGuid});
        if(markers.Length>0)throw new AcceptanceFailure("ORPHAN_FIXTURE_RECOVERY_REQUIRED");
    }
    internal static Task State(string output,string marker,string phase,string? action=null)=>Write(Path.Combine(output,"HARNESS_LIFECYCLE.json"),new{marker,phase,action,processId=Environment.ProcessId,utc=DateTime.UtcNow,databaseGuid=HostAcceptance.DatabaseGuid,cleanupGuaranteedAfterForceTermination=false});
}
