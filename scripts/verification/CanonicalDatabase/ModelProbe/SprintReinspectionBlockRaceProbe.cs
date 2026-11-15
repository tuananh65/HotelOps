using System.Data;
using System.Diagnostics;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.EntityFrameworkCore;

internal static class SprintReinspectionBlockRaceProbe
{
    // Invoked only within the GUID/run-verified disposable parent. No API/database
    // guard is removed: a temporary delay observes the real retained SQL locks.
    internal static async Task<RoomTypeBatchResult[]> RunAsync(HotelOpsDbContext db, ReinspectionExecutionDto source,
        Func<Task<RoomTypeBatchResult[]>> conclude, Func<Task<RoomTypeBatchResult>> read, CancellationToken ct)
    {
        if (Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BROWSER_CONCLUDE") == "1"
            || db.Database.CurrentTransaction != null || await db.Database.SqlQuery<int>($"""
                SELECT CASE WHEN DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
                  AND CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
                  AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
                  AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
                  AND OBJECT_ID('dbo.TR_DEV3_ReinspectionBlockDelay') IS NULL
                  AND EXISTS(SELECT 1 FROM dbo.fe05_reinspection_bindings_v1 WHERE inspection_id={source.Id}
                    AND room_id={source.RoomId} AND branch_id={source.BranchId} AND reinspector_user_id={source.InspectorId})
                  THEN 1 ELSE 0 END AS Value
                """).SingleAsync(ct) != 1) throw new Exception("Owned disposable reinspection block-race source required");
        var existing = await db.RoomBlocks.AsNoTracking().Where(b => b.RoomId == source.RoomId && b.ReleasedAt == null).ToArrayAsync(ct);
        var blocked = source.EffectiveReadiness == "BLOCKED";
        if (existing.Length != (blocked ? 1 : 0)) throw new Exception("Exact initial block facts required");
        // Source.Id is a proven positive bigint, never caller-provided SQL text.
        var ddl = $"""
            CREATE TRIGGER dbo.TR_DEV3_ReinspectionBlockDelay ON dbo.room_inspections AFTER UPDATE AS
            BEGIN SET NOCOUNT ON;
              IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.inspection_id=i.inspection_id
                WHERE i.inspection_id={source.Id} AND d.workflow_status='ASSIGNED' AND i.workflow_status IN('PASSED','FAILED'))
              BEGIN
                DECLARE @result int;
                EXEC @result=sys.sp_getapplock @Resource='DEV3:ReinspectionBlockRace',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=1000;
                IF @result<0 THROW 51999,'Reinspection race synchronization failed',1;
                WAITFOR DELAY '00:00:03';
              END
            END
            """;
        await using (var rehearsal = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            await db.Database.ExecuteSqlRawAsync(ddl, ct);
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_ReinspectionBlockDelay", ct);
            await rehearsal.RollbackAsync(ct);
        }
        long? incidentId = null, insertedBlock = null;
        var installed = false;
        Task<RoomTypeBatchResult[]>? pending = null;
        try
        {
            if (!blocked)
            {
                var incident = new Incident { IncidentNo = "D3-REINSPECTION-BLOCK", BranchId = source.BranchId,
                    RoomId = source.RoomId, IncidentTitle = "Synthetic conclusion block race", Description = "Synthetic",
                    Status = "OPEN", ReportedBy = source.InspectorId, ReportedAt = DateTime.UtcNow, DiscoveredAt = DateTime.UtcNow };
                db.Incidents.Add(incident); await db.SaveChangesAsync(ct); incidentId = incident.IncidentId; db.ChangeTracker.Clear();
            }
            await db.Database.ExecuteSqlRawAsync(ddl, ct); installed = true;
            pending = conclude();
            var until = DateTime.UtcNow.AddSeconds(20); var observed = false;
            while (DateTime.UtcNow < until && !pending.IsCompleted)
            {
                if (await db.Database.SqlQueryRaw<int>("SELECT APPLOCK_TEST('public','DEV3:ReinspectionBlockRace','Shared','Session') AS Value").SingleAsync(ct) == 0)
                { observed = true; break; }
                await Task.Delay(50, ct);
            }
            if (!observed) throw new Exception("Conclusion did not reach the synchronized domain write");
            await using var writer = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            var elapsed = Stopwatch.StartNew();
            if (blocked)
                await writer.RoomBlocks.Where(b => b.BlockId == existing[0].BlockId).ExecuteUpdateAsync(s => s.SetProperty(b => b.ReleasedAt, DateTime.UtcNow), ct);
            else
            {
                var block = new RoomBlock { RoomId = source.RoomId, IncidentId = incidentId, BlockedBy = source.InspectorId,
                    BlockedAt = DateTime.UtcNow, Reason = "Synthetic concurrent reinspection block" };
                writer.RoomBlocks.Add(block); await writer.SaveChangesAsync(ct); insertedBlock = block.BlockId;
            }
            elapsed.Stop();
            var results = await pending;
            if (elapsed.Elapsed < TimeSpan.FromSeconds(1)) throw new Exception("Concurrent block write bypassed retained conclusion locks");
            if (results.Any(r => r.Status != RoomTypeBatchStatus.Success || r.Value is not ReinspectionConclusionResult))
                throw new Exception("Concurrent conclusion failed: " + string.Join(',', results.Select(r => r.Status)));
            var receipt = (ReinspectionConclusionResult)results[0].Value!;
            var expectedCurrent = blocked ? receipt.State == "PASSED" ? "READY" : "REWORK_REQUIRED" : "BLOCKED";
            var current = await read();
            if (current.Value is not ReinspectionExecutionDto terminal || terminal.EffectiveReadiness != expectedCurrent
                || terminal.Conclusion != receipt || terminal.SourceETag != receipt.SourceETag
                || terminal.Conclusion.EffectiveReadiness != (blocked ? "BLOCKED" : receipt.State == "PASSED" ? "READY" : "REWORK_REQUIRED"))
                throw new Exception("Concurrent block facts overwrote receipt or failed to refresh current readiness");
            Console.WriteLine($"PASS reinspection {receipt.State} concurrent block {(blocked ? "release" : "insert")}: waited {elapsed.ElapsedMilliseconds}ms; current {terminal.EffectiveReadiness}; receipt {receipt.EffectiveReadiness}");
            return results;
        }
        finally
        {
            if (pending != null) try { await pending; } catch { /* Preserve the primary failure, then clean owned synchronization. */ }
            if (installed) await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_ReinspectionBlockDelay", CancellationToken.None);
            await using var cleanup = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None);
            if (insertedBlock is { } ownBlock)
                await db.RoomBlocks.Where(b => b.BlockId == ownBlock && b.RoomId == source.RoomId && b.IncidentId == incidentId && b.BlockedBy == source.InspectorId).ExecuteDeleteAsync(CancellationToken.None);
            if (blocked)
                await db.RoomBlocks.Where(b => b.BlockId == existing[0].BlockId && b.RoomId == source.RoomId)
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.ReleasedAt, existing[0].ReleasedAt), CancellationToken.None);
            if (incidentId is { } ownIncident)
                await db.Incidents.Where(i => i.IncidentId == ownIncident && i.IncidentNo == "D3-REINSPECTION-BLOCK" && i.ReportedBy == source.InspectorId).ExecuteDeleteAsync(CancellationToken.None);
            await cleanup.CommitAsync(CancellationToken.None); db.ChangeTracker.Clear();
        }
    }
}
