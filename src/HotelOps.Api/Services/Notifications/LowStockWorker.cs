using System.Globalization;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed class LowStockOptions { public bool Enabled { get; set; } }

// JOB02 activation is explicit. Schema installation never starts the worker.
internal sealed class LowStockWorker(IServiceScopeFactory scopes, IOptions<LowStockOptions> options,
    ILogger<LowStockWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        while (!ct.IsCancellationRequested)
        {
            var minutes = 5;
            try
            {
                await using (var config = scopes.CreateAsyncScope())
                {
                    var db = config.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
                    if (!await LowStockAlerts.InstalledAsync(db, ct)) throw new InvalidOperationException("JOB02 versioned storage required.");
                    var values = await db.SystemSettings.AsNoTracking().Where(s => s.BranchId == null && s.SettingKey == "SYS-PAR-15")
                        .Select(s => s.SettingValue).ToArrayAsync(ct);
                    if (values.Length > 1 || values.Length == 1 && (!int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out minutes) || minutes is < 1 or > 1440))
                        throw new InvalidOperationException("Invalid JOB02 interval configuration.");
                }
                long warehouseCursor = 0, itemCursor = 0;
                while (!ct.IsCancellationRequested)
                {
                    (long Warehouse, long Item)[] pairs;
                    await using (var discovery = scopes.CreateAsyncScope())
                    {
                        var db = discovery.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
                        var keys = db.StockThresholds.AsNoTracking().Select(t => new { t.WarehouseId, t.ItemId })
                            .Union(LowStockAlerts.Query(db).Where(e => e.ClosedAt == null).Select(e => new { e.WarehouseId, e.ItemId }));
                        var page = await keys.Where(p => p.WarehouseId > warehouseCursor || p.WarehouseId == warehouseCursor && p.ItemId > itemCursor)
                            .OrderBy(p => p.WarehouseId).ThenBy(p => p.ItemId).Take(100).ToArrayAsync(ct);
                        pairs = page.Select(p => (p.WarehouseId, p.ItemId)).ToArray();
                    }
                    if (pairs.Length == 0) break;
                    foreach (var pair in pairs)
                    {
                        try
                        {
                            long? episode;
                            await using (var observation = scopes.CreateAsyncScope())
                                episode = await observation.ServiceProvider.GetRequiredService<LowStockAlerts>().ObserveAsync(pair.Warehouse, pair.Item, ct);
                            if (episode is {} id)
                            {
                                await using var delivery = scopes.CreateAsyncScope();
                                await delivery.ServiceProvider.GetRequiredService<LowStockAlerts>().DeliverAsync(id, ct);
                            }
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                        catch (Exception e) { logger.LogWarning("JOB02 pair deferred; retry next scan. Failure type {FailureType}", e.GetType().Name); }
                    }
                    (warehouseCursor, itemCursor) = pairs[^1];
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception e) { logger.LogWarning("JOB02 unavailable; no fabricated alert. Failure type {FailureType}", e.GetType().Name); minutes = 5; }
            try { await Task.Delay(TimeSpan.FromMinutes(minutes), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        }
    }
}
