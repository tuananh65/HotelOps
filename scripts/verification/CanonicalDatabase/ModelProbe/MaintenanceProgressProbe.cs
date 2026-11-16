using System.Data;
using System.Reflection;
using HotelOps.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class MaintenanceProgressProbe
{
    internal static async Task<int> RunAsync()
    {
        const string server = @"meita\MSSQLSERVER01", database = "HotelOps_DEV3_142A_Recovery_2b221f40";
        var guid = Environment.GetEnvironmentVariable("HOTELOPS_DISPOSABLE_EXPECTED_GUID")
            ?? throw new InvalidOperationException("Verified disposable GUID required.");
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = budget.Token;
        var connection = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
            IntegratedSecurity = true, Encrypt = true, TrustServerCertificate = false };
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))={server} AND DB_NAME()={database}
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
                AND database_guid=CONVERT(uniqueidentifier,{guid})
                AND database_guid<>CONVERT(uniqueidentifier,'00000000-0000-0000-0000-000000000000')) THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1) throw new InvalidOperationException("Disposable identity mismatch.");
        var type = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Maintenance.MaintenanceProgressStore")!;
        Task Invoke(string name, params object[] values) => (Task)type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, values)!;
        async Task<bool> Installed() => await (Task<bool>)Invoke("InstalledAsync", db, ct);
        var originallyInstalled = await Installed();
        var installer = (await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/MaintenanceProgress.sql", ct))
            .Replace("$(ExpectedServer)", server).Replace("$(ExpectedDatabase)", database)
            .Replace("$(ExpectedDatabaseGuid)", guid).Replace("$(ApplyChanges)", "1");
        foreach (var scenario in new[] { "roundtrip", "range", "duplicate", "immutable" })
        {
            var prefix = "D3PROGRESS" + Guid.NewGuid().ToString("N")[..12];
            await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
            {
                try
                {
                    await db.Database.ExecuteSqlRawAsync(installer, ct);
                    await db.Database.ExecuteSqlRawAsync(installer, ct);
                    if (!await Installed()) throw new InvalidOperationException("Capability missing after installation.");
                    var fixture = (await db.Database.SqlQuery<Fixture>($"""
                        DECLARE @actor bigint,@work bigint,@log bigint;
                        INSERT dbo.users(employee_code,username,email,full_name,created_at,password_hash)
                          VALUES({prefix},{prefix},{prefix}+N'@example.invalid',N'Synthetic progress',SYSUTCDATETIME(),'Disabled synthetic account');
                        SET @actor=SCOPE_IDENTITY();
                        INSERT dbo.maintenance_works(work_no,title,status,created_by,created_at)
                          VALUES({prefix},N'Synthetic progress','IN_PROGRESS',@actor,SYSUTCDATETIME()); SET @work=SCOPE_IDENTITY();
                        INSERT dbo.maintenance_work_logs(maintenance_work_id,work_note,spent_minutes,status,logged_by,logged_at)
                          VALUES(@work,N'Synthetic progress',0,'IN_PROGRESS',@actor,SYSUTCDATETIME()); SET @log=SCOPE_IDENTITY();
                        SELECT @work AS Work,@log AS Log;
                        """).ToArrayAsync(ct)).Single();
                    await Invoke("WriteAsync", db, fixture.Log, 100, ct);
                    var read = Invoke("ReadAsync", db, fixture.Work, ct);
                    await read;
                    var rows = (Array)read.GetType().GetProperty("Result")!.GetValue(read)!;
                    if (rows.Length != 1 || (int)rows.GetValue(0)!.GetType().GetProperty("ProgressPercent")!.GetValue(rows.GetValue(0))! != 100)
                        throw new InvalidOperationException("Production EF roundtrip failed.");
                    if (await db.MaintenanceWorks.AsNoTracking().Where(w => w.MaintenanceWorkId == fixture.Work).Select(w => w.Status).SingleAsync(ct) != "IN_PROGRESS")
                        throw new InvalidOperationException("Percentage changed workflow state.");
                    if (scenario == "range")
                    {
                        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.maintenance_log_progress WHERE log_id={fixture.Log}", ct);
                        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.maintenance_log_progress(log_id,progress_percent) VALUES({fixture.Log},101)", ct);
                    }
                    else if (scenario == "duplicate")
                        await Invoke("WriteAsync", db, fixture.Log, 50, ct);
                    else if (scenario == "immutable")
                        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.maintenance_log_progress SET progress_percent=50 WHERE log_id={fixture.Log}", ct);
                    if (scenario != "roundtrip") throw new InvalidOperationException("Expected SQL rejection missing.");
                }
                catch (SqlException e) when ((scenario == "range" && e.Number == 547 && e.Message.Contains("CK_maintenance_log_progress_percent", StringComparison.Ordinal))
                    || (scenario == "duplicate" && e.Number == 2627) || (scenario == "immutable" && e.Number == 51844)) { }
                finally
                {
                    // Trigger errors can already have rolled back SQL's owning transaction.
                    try { await tx.RollbackAsync(CancellationToken.None); }
                    catch (InvalidOperationException) when (tx.GetDbTransaction().Connection == null) { }
                }
            }
            if (await db.Users.AsNoTracking().AnyAsync(u => u.Username == prefix, ct) || await Installed() != originallyInstalled)
                throw new InvalidOperationException("Rollback did not restore original state.");
            Console.WriteLine($"PASS MaintenanceProgress {scenario}; synthetic fixture and installer rolled back.");
        }
        return 0;
    }
    private sealed class Fixture { public long Work { get; set; } public long Log { get; set; } }
}
