using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HotelOps.Api.Data;

// Opt-in disposable runtime only. Each fresh encrypted connection loses its login
// permissions before application queries. No cookie, pooling, impersonation or fallback.
public sealed class IsolatedSqlApplicationRole : DbConnectionInterceptor
{
    private readonly string role,password,database,server,ownership;
    private readonly Guid databaseGuid;
    public IsolatedSqlApplicationRole(string connectionString,string role,string password,string server,Guid databaseGuid,string ownership,bool development)
    {
        var connection=new SqlConnectionStringBuilder(connectionString);
        if(!development||connection.Pooling||connection.Enlist||connection.Encrypt==SqlConnectionEncryptOption.Optional
            ||!connection.InitialCatalog.StartsWith("HotelOps_DEV3_142A_",StringComparison.Ordinal)
            ||string.IsNullOrWhiteSpace(role)||role.Length>128||string.IsNullOrWhiteSpace(password)||password.Length is <32 or >128
            ||string.IsNullOrWhiteSpace(server)||databaseGuid==Guid.Empty||!Guid.TryParseExact(ownership,"N",out _))
            throw new InvalidOperationException("Unsafe isolated SQL application-role configuration.");
        this.role=role;this.password=password;this.server=server;this.databaseGuid=databaseGuid;this.ownership=ownership;database=connection.InitialCatalog;
    }

    public static IsolatedSqlApplicationRole? FromConfiguration(IConfiguration configuration,IHostEnvironment environment)
    {
        var settings=configuration.GetSection("IsolatedSqlApplicationRole");
        if(!settings.Exists())return null;
        if(!Guid.TryParse(settings["DatabaseGuid"],out var identity))throw new InvalidOperationException("Isolated database identity required.");
        return new(configuration.GetConnectionString("HotelOpsDb")??"",settings["Name"]??"",settings["Password"]??"",
            settings["Server"]??"",identity,settings["Ownership"]??"",environment.IsDevelopment());
    }

    public override InterceptionResult ConnectionOpening(DbConnection connection,ConnectionEventData eventData,InterceptionResult result)
        => throw new InvalidOperationException("Isolated application-role connections require asynchronous access.");

    public override Task ConnectionOpenedAsync(DbConnection connection,ConnectionEndEventData eventData,CancellationToken cancellationToken=default)
        => ActivateAsync(connection,cancellationToken);

    public async Task ActivateAsync(DbConnection connection,CancellationToken ct)
    {
        try
        {
            if(connection is not SqlConnection sql||System.Transactions.Transaction.Current!=null)
                throw new InvalidOperationException("Fresh SQL connection outside ambient transactions required.");
            var actual=new SqlConnectionStringBuilder(sql.ConnectionString);
            if(actual.Pooling||actual.Enlist||actual.Encrypt==SqlConnectionEncryptOption.Optional||actual.InitialCatalog!=database)
                throw new InvalidOperationException("Connection does not match isolated role configuration.");
            await using var identity=new SqlCommand("""
                SELECT CASE WHEN DB_NAME()=@database AND CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=@server
                  AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid=@guid)
                  AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=@ownership)
                  THEN 1 ELSE 0 END
                """,sql);
            identity.Parameters.Add("@database",SqlDbType.NVarChar,128).Value=database;
            identity.Parameters.Add("@server",SqlDbType.NVarChar,128).Value=server;
            identity.Parameters.Add("@guid",SqlDbType.UniqueIdentifier).Value=databaseGuid;
            identity.Parameters.Add("@ownership",SqlDbType.NVarChar,128).Value=ownership;
            if(Convert.ToInt32(await identity.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException("Disposable identity mismatch.");
            // Direct RPC is required: parameterized text executes under sp_executesql,
            // which SQL Server correctly rejects for application-role activation.
            await using var activate=new SqlCommand("sys.sp_setapprole",sql){CommandType=CommandType.StoredProcedure};
            activate.Parameters.Add("@rolename",SqlDbType.NVarChar,128).Value=role;
            activate.Parameters.Add("@password",SqlDbType.NVarChar,128).Value=password;
            activate.Parameters.Add("@fCreateCookie",SqlDbType.Bit).Value=false;
            await activate.ExecuteNonQueryAsync(ct);
            await using var verify=new SqlCommand("SELECT CASE WHEN USER_NAME()=@role AND DB_NAME()=@database AND HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL')=0 THEN 1 ELSE 0 END",sql);
            verify.Parameters.Add("@role",SqlDbType.NVarChar,128).Value=role;
            verify.Parameters.Add("@database",SqlDbType.NVarChar,128).Value=database;
            if(Convert.ToInt32(await verify.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException("Restricted role required.");
        }
        catch(OperationCanceledException){await connection.CloseAsync();throw;}
        catch
        {
            await connection.CloseAsync();
            // Never attach provider errors containing credentials or command text.
            throw new InvalidOperationException("Isolated SQL application-role activation failed; connection closed.");
        }
    }
}
