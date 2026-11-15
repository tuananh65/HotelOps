using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

// Feasibility only: no server login/configuration changes, no application bypass.
internal static class SprintApplicationRoleProbe
{
    private const string Connection = "Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Pooling=false;Enlist=false";
    internal static async Task<int> RunAsync(string report)
    {
        if(File.Exists(report))throw new IOException("Evidence already exists");
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(1));var ct=timeout.Token;
        await using var admin=new SqlConnection(Connection);await admin.OpenAsync(ct);
        async Task<object?> Scalar(SqlConnection connection,string sql)
        {await using var command=new SqlCommand(sql,connection);return await command.ExecuteScalarAsync(ct);}
        if(Convert.ToInt32(await Scalar(admin,"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
              AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
              AND DATABASE_PRINCIPAL_ID('DEV3_142A_ApplicationTrial') IS NULL
              THEN 1 ELSE 0 END
            """))!=1)throw new InvalidOperationException("Disposable identity/fresh role required");
        var password=Convert.ToHexString(RandomNumberGenerator.GetBytes(32))+"aA1!";
        var created=false;var checks=new List<string>();string? failure=null;
        try
        {
            for(var pass=0;pass<2;pass++)
            {
                await using var tx=(SqlTransaction)await admin.BeginTransactionAsync(IsolationLevel.Serializable,ct);
                await using var install=new SqlCommand("""
                    DECLARE @ddl nvarchar(max)=N'CREATE APPLICATION ROLE DEV3_142A_ApplicationTrial WITH PASSWORD='''+REPLACE(@password,'''','''''')+N''', DEFAULT_SCHEMA=dbo';
                    EXEC sys.sp_executesql @ddl;
                    GRANT SELECT ON SCHEMA::dbo TO DEV3_142A_ApplicationTrial;
                    GRANT VIEW DEFINITION TO DEV3_142A_ApplicationTrial;
                    DENY INSERT,UPDATE,DELETE ON dbo.audit_logs TO DEV3_142A_ApplicationTrial;
                    """,admin,tx);
                install.Parameters.Add("@password",SqlDbType.NVarChar,128).Value=password;
                await install.ExecuteNonQueryAsync(ct);
                if(pass==0){await tx.RollbackAsync(ct);checks.Add("role creation rollback");}
                else{await tx.CommitAsync(ct);created=true;}
            }
            IsolatedSqlApplicationRole Role(string secret,Guid identity)=>new(Connection,"DEV3_142A_ApplicationTrial",secret,"meita",identity,"cc78c13ffc124efcad67b3d804baa84c",true);
            var guid=Guid.Parse("1D5A9B53-7D19-435A-96AB-9FDFF447B6E0");
            foreach(var invalid in new[]{Role(password+"invalid",guid),Role(password,Guid.NewGuid())})
            {
                await using var denied=new SqlConnection(Connection);await denied.OpenAsync(ct);
                try{await invalid.ActivateAsync(denied,ct);throw new Exception("Invalid activation succeeded");}
                catch(InvalidOperationException) when(denied.State==ConnectionState.Closed){checks.Add("invalid password/identity closes connection without fallback");}
            }
            var runtimeRole=Role(password,guid);
            await using(var context=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(Connection).AddInterceptors(runtimeRole).Options))
                for(var attempt=0;attempt<2;attempt++)
                    if(await context.Database.SqlQueryRaw<string>("SELECT USER_NAME() AS Value").SingleAsync(ct)!="DEV3_142A_ApplicationTrial")throw new Exception("EF connection retained login permissions");
            checks.Add("EF async connections activate restricted role on reopen");
            await using(var restricted=new SqlConnection(Connection))
            {
                await restricted.OpenAsync(ct);
                await runtimeRole.ActivateAsync(restricted,ct);
                if(Convert.ToString(await Scalar(restricted,"SELECT USER_NAME()"))!="DEV3_142A_ApplicationTrial")throw new InvalidOperationException("Role activation missing");
                if(Convert.ToInt32(await Scalar(restricted,"SELECT CASE WHEN HAS_PERMS_BY_NAME('dbo.audit_logs','OBJECT','INSERT')=0 AND HAS_PERMS_BY_NAME('dbo.audit_logs','OBJECT','UPDATE')=0 AND HAS_PERMS_BY_NAME('dbo.audit_logs','OBJECT','DELETE')=0 AND HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL')=0 THEN 1 ELSE 0 END"))!=1)
                    throw new InvalidOperationException("Privileged permissions retained");
                checks.Add("effective principal restricted; audit DML and database CONTROL denied");
                await Scalar(restricted,"SELECT COUNT_BIG(*) FROM dbo.audit_logs");checks.Add("authorized read succeeds");
                try{await Scalar(restricted,"DELETE dbo.audit_logs WHERE 1=0");throw new InvalidOperationException("Forbidden DML succeeded");}
                catch(SqlException error) when(error.Number==229){checks.Add("real forbidden DML denied");}
                await using var transaction=(SqlTransaction)await restricted.BeginTransactionAsync(IsolationLevel.Serializable,ct);
                await transaction.RollbackAsync(ct);checks.Add("restricted serializable transaction supported");
            }
        }
        catch(Exception error){failure=error.GetType().Name+(error is SqlException sql?" SQL "+sql.Number:"");}
        finally
        {
            if(created)
            {
                await using var cleanup=new SqlCommand("DROP APPLICATION ROLE DEV3_142A_ApplicationTrial; SELECT CASE WHEN DATABASE_PRINCIPAL_ID('DEV3_142A_ApplicationTrial') IS NULL THEN 1 ELSE 0 END",admin);
                if(Convert.ToInt32(await cleanup.ExecuteScalarAsync(CancellationToken.None))!=1)throw new InvalidOperationException("Role cleanup failed");
                checks.Add("application role removed; no pooled restricted connection");
            }
        }
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{success=failure==null,checks,failure},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(failure==null?"PASS application-role feasibility and cleanup":"FAIL application-role feasibility; see receipt");return failure==null?0:1;
    }
}
