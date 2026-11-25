using System.Text.Json;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed class ShiftExpiryService(HotelOpsCompactDbContext db, Atomic atomic, TimeProvider clock)
{
    public const string ExpiredWithoutStartAction = "AUTO_CANCEL_EXPIRED";

    public static DateTime Boundary(DateOnly day, int minute, string timezone) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue).AddMinutes(minute),
            TimeZoneInfo.FindSystemTimeZoneById(timezone));

    public Task<int> CloseExpired(long? branchId, CancellationToken ct) => atomic.Run(async () =>
    {
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.Workday',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51410,'Workday lock unavailable.',1;", ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var active = await (from a in db.WorkdayAssignments
            join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            join b in db.Branches on s.BranchId equals b.BranchId
            // Attendance expires for every role. Custody/reservations remain open until
            // the employee explicitly reconciles them; a timer is not a stock handover.
            where (a.Status == "PLANNED" || (a.Status == "WORKING" && a.StartedAt <= now))
                && (branchId == null || s.BranchId == branchId)
            select new { Assignment = a, s.BranchId, s.EndMinute, b.Timezone }).ToArrayAsync(ct);
        var count = 0;
        foreach (var item in active)
        {
            var row = item.Assignment;
            var before = JsonSerializer.Serialize(Output.Record(row));
            if (!TryExpire(row, item.EndMinute, item.Timezone, now)) continue;
            db.AuditLogs.Add(new AuditLog
            {
                BranchId = item.BranchId, ActorUserId = null, EntityType = "Workday",
                EntityKey = row.AssignmentId.ToString(),
                Action = row.Status == "CANCELLED" ? ExpiredWithoutStartAction : "AUTO_END", OperationKey = Guid.NewGuid(),
                BeforeJson = before, AfterJson = JsonSerializer.Serialize(Output.Record(row)), OccurredAt = now
            });
            count++;
        }
        await db.SaveChangesAsync(ct);
        return count;
    }, ct);

    public static bool TryExpire(WorkdayAssignment row, int endMinute, string timezone, DateTime now)
    {
        if (row.Status == "PLANNED")
        {
            if (row.StartedAt != null || row.EndedAt != null
                || Boundary(row.WorkDate, endMinute, timezone).AddHours(1) > now) return false;
            // Never fabricate attendance: ENDED requires a real start in the database.
            // The system audit distinguishes this expiry from a manual cancellation.
            row.Status = "CANCELLED";
            return true;
        }
        if (row.Status != "WORKING" || row.StartedAt == null || row.StartedAt > now) return false;
        var end = Boundary(row.WorkDate, endMinute, timezone);
        if (end > now) return false;
        row.Status = "ENDED";
        // Historical late starts must still satisfy ended_at >= started_at.
        row.EndedAt = row.StartedAt > end ? row.StartedAt : end;
        return true;
    }
}

public sealed class ShiftExpiryWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<ShiftExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Compact:AutoEndShiftsEnabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ShiftExpiryService>().CloseExpired(null, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Automatic shift completion failed; transaction rolled back."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
