using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

internal sealed partial class Fixtures
{
    internal string Stage {get;private set;}="00 transaction-open";
    internal string? SaveBoundary {get;private set;}
    internal FailureDiagnostics? Failure {get;private set;}
    private readonly List<object> stages=[];

    private async Task Save(HotelOpsDbContext db,string stage,string boundary)
    {
        Stage=stage;SaveBoundary=boundary;
        var pending=db.ChangeTracker.Entries().Where(e=>e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).Select(e=>e.Metadata.ClrType.Name).Distinct().Order().ToArray();
        stages.Add(new{stage,boundary,status="STARTED",entityTypes=pending});await WriteStages();
        try
        {
            await db.SaveChangesAsync();stages.Add(new{stage,boundary,status="PASS",entityTypes=pending});await WriteStages();
        }
        catch(Exception error)
        {
            Failure=FailureDiagnostics.Capture(error,"fixture-create",stage,boundary,db,output,pending);
            stages.Add(new{stage,boundary,status="FAIL",entityTypes=pending});await WriteStages();throw;
        }
    }
    private Task WriteStages()=>File.WriteAllTextAsync(Path.Combine(output,"FIXTURE_STAGES.json"),JsonSerializer.Serialize(stages,new JsonSerializerOptions{WriteIndented=true}));

    internal async Task VerifyNoRoots()
    {
        await using var db=open();await HostAcceptance.VerifyIdentity(db);
        // Also verify global fixture catalog rows, users and test permissions after transaction rollback/cleanup.
        var remaining=await db.Branches.CountAsync(x=>x.BranchCode.StartsWith(Marker))
            +await db.Users.CountAsync(x=>x.Username.StartsWith(Marker))
            +await db.Roles.CountAsync(x=>x.RoleCode.StartsWith(Marker))
            +await db.AssetTypes.CountAsync(x=>x.TypeCode==Marker)
            +await db.UnitsOfMeasures.CountAsync(x=>x.UomCode==Marker)
            +await db.ItemCategories.CountAsync(x=>x.CategoryCode==Marker)
            +await db.ReferenceValues.CountAsync(x=>x.ValueCode==Marker)
            +await references.OwnedQuery(db).CountAsync()
            +await db.Permissions.CountAsync(x=>NewPermissionIds.Contains(x.PermissionId));
        if(remaining!=0)throw new AcceptanceFailure("FIXTURE_ROOTS_REMAIN");
        var userKeys=UserIds.Select(x=>x.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if(await db.AuditLogs.AnyAsync(x=>x.EntityType=="users"&&userKeys.Contains(x.EntityId)))throw new AcceptanceFailure("FIXTURE_AUTH_AUDIT_REFERENCES_REMAIN");
        await VerifyBorrowedReferences(db);
    }
}
