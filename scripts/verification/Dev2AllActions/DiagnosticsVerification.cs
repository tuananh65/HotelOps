using System.Text.Json;
using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class DiagnosticsVerification
{
    internal static Task<int> Run(string output)
    {
        var assertions=0;void Check(bool ok){if(!ok)throw new InvalidOperationException("Sanitized diagnostic verification failed");assertions++;}
        var tables=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"dbo.material_request_lines","dbo.users"};
        var constraints=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"CK_material_request_lines_quantities","UX_test"};
        var fk=FailureDiagnostics.ExtractKnownIdentifiers("The INSERT statement conflicted with the CHECK constraint \"CK_material_request_lines_quantities\". Table \"HotelOps_DEV2_FullStack_Trial.dbo.material_request_lines\".",tables,constraints);
        Check(fk.tables.SequenceEqual(new[]{"dbo.material_request_lines"}));Check(fk.constraints.SequenceEqual(new[]{"CK_material_request_lines_quantities"}));
        var unique=FailureDiagnostics.ExtractKnownIdentifiers("Cannot insert duplicate key row in object 'dbo.users' with unique index 'UX_test'. Duplicate key value (PRIVATE-EMAIL).",tables,constraints);
        Check(unique.tables.SequenceEqual(new[]{"dbo.users"}));Check(unique.constraints.SequenceEqual(new[]{"UX_test"}));
        var unknown=FailureDiagnostics.ExtractKnownIdentifiers("constraint 'PRIVATE-PASSWORD' table 'PRIVATE-COOKIE'",tables,constraints);Check(unknown.tables.Length==0&&unknown.constraints.Length==0);
        using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=lpc:KIENPC;Database=HotelOps_DEV2_FullStack_Trial;Integrated Security=true;Encrypt=false").Options);
        var user=new User{UserId=123,Username="PRIVATE-USERNAME",Email="PRIVATE-EMAIL",FullName="PRIVATE-PERSON",PasswordHash="PRIVATE-PASSWORD"};var entry=db.Entry(user);entry.State=EntityState.Added;
        var error=new DbUpdateException("PRIVATE-CSRF",new InvalidOperationException("PRIVATE-CONNECTION; PRIVATE-COOKIE"),new[]{entry});
        var diagnostic=FailureDiagnostics.Capture(error,"fixture-create","04 users","04.hashed-users",db,output,new[]{"User"});
        var json=JsonSerializer.Serialize(diagnostic);foreach(var secret in new[]{"PRIVATE-USERNAME","PRIVATE-EMAIL","PRIVATE-PERSON","PRIVATE-PASSWORD","PRIVATE-CSRF","PRIVATE-CONNECTION","PRIVATE-COOKIE"})Check(!json.Contains(secret,StringComparison.Ordinal));
        Check(diagnostic.EntityTypes.SequenceEqual(new[]{"User"}));Check(diagnostic.AffectedTables.Contains("dbo.users"));Check(diagnostic.ExceptionType==nameof(DbUpdateException));Check(diagnostic.InnerExceptionType==nameof(InvalidOperationException));Check(diagnostic.SaveChangesBoundary=="04.hashed-users");Check(!diagnostic.TransactionCommitted&&!diagnostic.CleanupCompleted);
        var matrix=Dev2ActionAdmissionRegistry.KnownActions.Select(x=>new ActionReceipt(x)).ToArray();Check(matrix.Length==61&&matrix.All(x=>x.Status=="NOT_RUN"&&x.ActualHttpStatus is null));
        return Task.FromResult(assertions);
    }
}
