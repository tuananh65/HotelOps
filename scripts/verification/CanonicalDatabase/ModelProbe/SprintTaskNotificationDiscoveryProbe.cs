using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class SprintTaskNotificationDiscoveryProbe
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    internal static async Task<int> RunAsync(string receiptPath, string reportPath)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = budget.Token;
        using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath, ct));
        var data = receipt.RootElement;
        var name = data.GetProperty("database").GetString()!;
        var run = data.GetProperty("run").GetString()!;
        var guid = Guid.Parse(data.GetProperty("databaseGuid").GetString()!);
        if (!System.Text.RegularExpressions.Regex.IsMatch(run, "^[a-f0-9]{32}$")
            || name != "HotelOps_DEV3_142A_CriteriaTrial_" + run[..8] || guid == Guid.Empty
            || data.GetProperty("status").GetString() != "CLONE_READY") throw new Exception("Verified clone required");
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer($"Server=localhost;Database={name};Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita' AND DB_NAME()={name}
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid={guid})
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run'
                AND CONVERT(nvarchar(128),value)={run}) THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1) throw new Exception("Clone identity mismatch");
        var newest = await db.AuditLogs.MaxAsync(a => a.OccurredAt, ct);
        var oldest = await db.AuditLogs.MinAsync(a => a.OccurredAt, ct);
        var checks = new List<object>();
        foreach (var kind in new[] { "Submission", "Verification", "Decline", "Assignment", "Plan", "Series", "Reopen" })
        foreach (var days in new[] { 30, 365 })
        foreach (var future in new[] { false, true })
        {
            var now = DateTime.SpecifyKind(future ? oldest.AddDays(-1) : newest, DateTimeKind.Utc);
            var type = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Notifications.Task" + kind + "NotificationDelivery", true)!;
            var instance = Activator.CreateInstance(type, db, new Clock(new DateTimeOffset(now)), Options.Create(new NotificationCenterOptions { RetentionDays = days }))!;
            var pending = await (Task<long[]>)type.GetMethod("ReadPendingAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, [0L, 100, ct])!;
            if (future ? pending.Length != 0 : pending.Length == 0) throw new Exception(kind + " discovery window mismatch");
            checks.Add(new { kind, days, future, pendingCount = pending.Length });
        }
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new { success = true, readOnly = true, checks }, new JsonSerializerOptions { WriteIndented = true }), ct);
        return 0;
    }
}
