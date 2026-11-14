using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.EntityFrameworkCore;

internal static class SprintEvidenceGuardProbe
{
    internal static async Task<int> RunAsync(string report)
    {
        if (File.Exists(report)) throw new IOException("Evidence already exists");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = timeout.Token;
        var sql = (await File.ReadAllTextAsync("scripts/database/release/housekeeping/04_EvidenceInvalidation.sql", ct)).Replace("\r\n", "\n");
        var lf = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
        var crlf = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql.Replace("\n", "\r\n"))));
        if (lf != "314DBD336CED88E2DCFDCF6762F8126BFA95D349247ACADA7294F83F19B1F7AD"
            || crlf != "E94CD891B0EDE6AD98384B91B35B6DC395E70E53D3CAD994854F4B7237C61CAC")
            throw new InvalidOperationException("Reviewed evidence script changed");
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        await db.Database.OpenConnectionAsync(ct);
        await using var identity = db.Database.GetDbConnection().CreateCommand();
        identity.CommandText = """
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
              AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
                AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run'
                AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c') THEN 1 ELSE 0 END
            """;
        if (!Equals(await identity.ExecuteScalarAsync(ct), 1)) throw new InvalidOperationException("Disposable identity rejected");
        var service = new RoomTypeCreationService(db, null!, null!, null!, TimeProvider.System);
        var method = typeof(RoomTypeCreationService).GetMethod("InspectionEvidenceRevisionInstalled", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Task<bool> Guard() => (Task<bool>)method.Invoke(service, [ct])!;
        if (!await Guard()) throw new InvalidOperationException("Installed evidence package not recognized");
        var checks = new List<object>();
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            try
            {
                foreach (var (name, hash, expected) in new[] { ("Git LF", lf, true), ("Windows CRLF", crlf, true), ("Unknown package", new string('0', 64), false) })
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_updateextendedproperty @name=N'HotelOps.Housekeeping.EvidenceRevision.V1',@value={hash}", ct);
                    var actual = await Guard();
                    checks.Add(new { name, expected, actual });
                    if (actual != expected) throw new InvalidOperationException("Evidence guard mismatch: " + name);
                }
                await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_updateextendedproperty @name=N'HotelOps.Housekeeping.EvidenceRevision.V1',@value={lf}", ct);
                await db.Database.ExecuteSqlRawAsync("DISABLE TRIGGER dbo.TR_hk_attachment_revision_v1 ON dbo.attachments", ct);
                var disabled = await Guard();
                checks.Add(new { name = "Disabled trigger", expected = false, actual = disabled });
                if (disabled) throw new InvalidOperationException("Disabled trigger accepted");
            }
            finally { await transaction.RollbackAsync(CancellationToken.None); }
        }
        var restored = await Guard();
        if (!restored) throw new InvalidOperationException("Evidence guard state not restored");
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { success = true, checks, restored,
            scope = "Actual private read guard with rolled-back synthetic marker/trigger negatives; no authority bypass" },
            new JsonSerializerOptions { WriteIndented = true }), ct);
        Console.WriteLine("Evidence guard: 4 PASS; rollback restoration PASS");
        return 0;
    }
}
