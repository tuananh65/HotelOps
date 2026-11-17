using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Insights;

internal static class AuditPreservation
{
    internal static Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) => db.Database.SqlQueryRaw<bool>("""
        SELECT CONVERT(bit,CASE WHEN OBJECT_ID('dbo.audit_archive_state','U') IS NOT NULL
        AND OBJECT_ID('dbo.job06_archive_audit_v1','P') IS NOT NULL
        AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_audit_append_only_v1','TR_audit_no_credentials_v1','TR_audit_archive_immutable_v1') AND is_disabled=0)=3
        THEN 1 ELSE 0 END) AS Value
        """).SingleAsync(ct);
    internal static async Task<AuditRetentionView> ReadAsync(HotelOpsDbContext db, long id, CancellationToken ct)
    {
        if (!await InstalledAsync(db, ct)) return new("CAPABILITY_UNAVAILABLE", null, null, null, false, null, "PURGE_BLOCKED", "AUDIT_LOG_PURGE_DISABLED; storage guard installation required");
        return await db.Database.SqlQuery<AuditRetentionView>($"""
            SELECT archive_status AS ArchiveStatus,archived_at AS ArchivedAt,retention_until AS RetentionUntil,
            archived_by AS ArchivedBy,legal_hold AS LegalHold,legal_hold_until AS LegalHoldUntil,
            purge_eligibility AS PurgeEligibility,purge_block_reason AS Reason
            FROM dbo.audit_archive_state WHERE audit_id={id}
            """).SingleOrDefaultAsync(ct) ?? new("NOT_ARCHIVED", null, null, null, false, null, "PURGE_BLOCKED", "AUDIT_LOG_PURGE_DISABLED");
    }
}

public sealed class AuditArchiveWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<AuditArchiveWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // This worker only classifies immutable audit in place. It never deletes audit,
        // receipts, incidents, notification audit or business records, regardless of config.
        if (!configuration.GetValue<bool>("Job06:ArchiveAuditEnabled")) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
                if (!await AuditPreservation.InstalledAsync(db, stoppingToken)) logger.LogWarning("JOB06 archive capability unavailable; purge remains disabled.");
                else
                {
                    // ToArray avoids composing LINQ over EXEC. Each bounded batch owns its transaction and lease.
                    var count = await db.Database.SqlQueryRaw<int>("EXEC dbo.job06_archive_audit_v1 @batch_size=500").ToArrayAsync(stoppingToken);
                    logger.LogInformation("JOB06 archived {Count} audit records in place; purge disabled.", count.Single());
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception) { logger.LogWarning("JOB06 archive batch failed; retry scheduled; no audit/receipt purge."); }
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
