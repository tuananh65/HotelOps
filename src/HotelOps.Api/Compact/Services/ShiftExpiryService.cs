using System.Text.Json;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed class ShiftExpiryService(HotelOpsCompactDbContext db, Atomic atomic, TimeProvider clock)
{
    public static DateTime Boundary(DateOnly day, int minute, string timezone) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue).AddMinutes(minute),
            TimeZoneInfo.FindSystemTimeZoneById(timezone));

    public Task<int> CloseExpired(long? branchId, CancellationToken ct) => atomic.Run(async () =>
    {
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.Workday',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51410,'Workday lock unavailable.',1;", ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var active = await (from a in db.WorkdayAssignments
            join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            join role in db.Roles on s.RoleId equals role.RoleId
            join b in db.Branches on s.BranchId equals b.BranchId
            // Housekeeping closes explicitly after work and personal stock are reconciled.
            where a.Status == "WORKING" && role.Code != "HK_STAFF" && a.StartedAt <= now && (branchId == null || s.BranchId == branchId)
            select new { Assignment = a, s.BranchId, s.EndMinute, b.Timezone }).ToArrayAsync(ct);
        var count = 0;
        foreach (var item in active)
        {
            var row = item.Assignment;
            var end = Boundary(row.WorkDate, item.EndMinute, item.Timezone);
            if (end > now) continue;
            var before = JsonSerializer.Serialize(Output.Record(row));
            row.Status = "ENDED";
            // Preserve the timestamp invariant for historical records started after their scheduled end.
            row.EndedAt = row.StartedAt > end ? row.StartedAt : end;
            db.AuditLogs.Add(new AuditLog
            {
                BranchId = item.BranchId, ActorUserId = null, EntityType = "Workday",
                EntityKey = row.AssignmentId.ToString(), Action = "AUTO_END", OperationKey = Guid.NewGuid(),
                BeforeJson = before, AfterJson = JsonSerializer.Serialize(Output.Record(row)), OccurredAt = now
            });
            count++;
        }
        await db.SaveChangesAsync(ct);
        return count;
    }, ct);
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
