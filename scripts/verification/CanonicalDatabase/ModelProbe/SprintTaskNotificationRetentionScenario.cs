using System.Reflection;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class SprintTaskNotificationRetentionScenario
{
    private sealed class EventRow
    {
        public string SourceType { get; set; } = "";
        public long SourceId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    internal static async Task RunAsync(HotelOpsDbContext db, List<object> checks, CancellationToken ct)
    {
        var kinds = new Dictionary<string, string>
        {
            ["TASK_SUBMISSION"] = "Submission", ["TASK_VERIFICATION"] = "Verification",
            ["TASK_DECLINE"] = "Decline", ["TASK_ASSIGNMENT"] = "Assignment",
            ["TASK_PLAN"] = "Plan", ["TASK_SERIES"] = "Series", ["TASK_REOPEN"] = "Reopen",
        };
        var names = kinds.Keys.ToArray();
        var notices = await db.Notifications.AsNoTracking().Where(n => names.Contains(n.SourceType!))
            .Select(n => new { n.NotificationId, n.SourceType, n.SourceId, n.CreatedAt }).ToArrayAsync(ct);
        if (notices.Length == 0) throw new Exception("Committed task notices required for retention verification");
        var events = notices.Select(n => new EventRow { SourceType = n.SourceType!, SourceId = n.SourceId!.Value, CreatedAt = n.CreatedAt }).ToList();
        events.AddRange(await db.Database.SqlQuery<EventRow>($"""
            SELECT 'TASK_SUBMISSION' AS SourceType,progress_id AS SourceId,submitted_at AS CreatedAt FROM dbo.task_submission_events
            UNION ALL
            SELECT 'TASK_VERIFICATION' AS SourceType,submission_id AS SourceId,decided_at AS CreatedAt FROM dbo.task_verification_events
            """).ToArrayAsync(ct));
        events = events.DistinctBy(e => (e.SourceType, e.SourceId)).ToList();
        if (names.Any(kind => !events.Any(n => n.SourceType == kind)))
            throw new Exception("All seven committed task notification families required");
        var ids = notices.Select(n => n.NotificationId).ToArray();
        var source = await Fingerprint(db, ct);
        // This scenario is inside the identity-verified disposable clone harness.
        // Delete only delivered synthetic inbox records, never their immutable sources.
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.NotificationRecipients.Where(r => ids.Contains(r.NotificationId)).ExecuteDeleteAsync(ct);
            await db.Notifications.Where(n => ids.Contains(n.NotificationId)).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
        }
        foreach (var days in new[] { 365, 30 })
        {
            var now = new DateTimeOffset(DateTime.SpecifyKind(events.Max(n => n.CreatedAt), DateTimeKind.Utc)).AddDays(days + 1);
            foreach (var pair in kinds)
            {
                await using var fresh = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
                    .UseSqlServer(db.Database.GetConnectionString()!).Options);
                var type = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Notifications.Task" + pair.Value + "NotificationDelivery", true)!;
                var instance = Activator.CreateInstance(type, fresh, new Clock(now), Options.Create(new NotificationCenterOptions { RetentionDays = days }))!;
                var pending = await (Task<long[]>)type.GetMethod("ReadPendingAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(instance, [0L, 100, ct])!;
                if (pending.Length != 0) throw new Exception(pair.Value + " rediscovered expired intent");
                foreach (var notice in events.Where(n => n.SourceType == pair.Key))
                {
                    var result = await (Task<string>)type.GetMethod("DeliverAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(instance, [notice.SourceId, ct])!;
                    if (result != "EVENT_OUTSIDE_RETENTION") throw new Exception(pair.Value + " did not reject expired event: " + result);
                }
                checks.Add(new { taskNotificationRetention = pair.Value, days, discoveryEmpty = true,
                    directDeliveriesDenied = events.Count(n => n.SourceType == pair.Key) });
            }
        }
        if (await db.Notifications.AnyAsync(n => names.Contains(n.SourceType!), ct)) throw new Exception("Purged task notices were recreated");
        if (source != await Fingerprint(db, ct)) throw new Exception("Retention modified task/audit provenance");
        checks.Add(new { agedTaskNotificationsNotRecreated = true, immutableSourcesUnchanged = true, deletedSyntheticNotices = ids.Length });
    }

    private static async Task<string> Fingerprint(HotelOpsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<string>($"""
            SELECT CONCAT(
              (SELECT * FROM dbo.audit_logs ORDER BY audit_id FOR JSON PATH,INCLUDE_NULL_VALUES),
              (SELECT * FROM dbo.task_submission_events ORDER BY progress_id FOR JSON PATH,INCLUDE_NULL_VALUES),
              (SELECT * FROM dbo.task_verification_events ORDER BY submission_id FOR JSON PATH,INCLUDE_NULL_VALUES),
              (SELECT * FROM dbo.task_reopen_events ORDER BY previous_submission_id FOR JSON PATH,INCLUDE_NULL_VALUES),
              (SELECT * FROM dbo.operational_tasks ORDER BY task_id FOR JSON PATH,INCLUDE_NULL_VALUES)) AS Value
            """).SingleAsync(ct);
}
