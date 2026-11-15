using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Security.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class SprintReinspectionConclusionProbe
{
    internal static async Task VerifyAsync(HotelOpsDbContext db, SessionReference ticket, long sid, long branch, long rework, CancellationToken ct)
    {
        // Called only inside the guarded synthetic assignment fixture; parent owns all cleanup.
        var mode = Environment.GetEnvironmentVariable("DEV3_REINSPECTION_OUTCOME") ?? "PASS";
        if (mode is not ("PASS" or "FAIL")) throw new Exception("Explicit PASS/FAIL conclusion trial mode required");
        var inspection = await db.Database.SqlQuery<long>($"SELECT inspection_id AS Value FROM dbo.fe05_reinspection_bindings_v1 WHERE rework_id={rework}").SingleAsync(ct);
        async Task<RoomTypeBatchResult> Run(Guid? operation = null, ConcludeReinspectionRequest? body = null, string? tag = null)
        {
            await using var context = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            await context.Database.OpenConnectionAsync(ct);
            await context.Database.ExecuteSqlRawAsync("EXECUTE AS USER='DEV3_Reinspector'", ct);
            try
            {
                var identity = new Identity(new(ticket.UserId, sid, false));
                var service = new RoomTypeCreationService(context, identity,
                    new PermissionService(identity, new EfPermissionDataSource(context), new UnresolvedPermissionResourceResolver(), TimeProvider.System),
                    new SqlSessionService(context, new SessionRevocationService(context), TimeProvider.System), TimeProvider.System);
                return operation == null
                    ? await service.ReadReinspectionExecutionAsync(ticket, sid, branch, inspection, SprintReworkFixture.EvidenceRoot, ct)
                    : await service.ConcludeReinspectionAsync(ticket, sid, branch, inspection, tag, operation.Value.ToString("D"), body!, SprintReworkFixture.EvidenceRoot, ct);
            }
            finally { await context.Database.ExecuteSqlRawAsync("REVERT", CancellationToken.None); }
        }
        var read = await Run();
        if (read.Status != RoomTypeBatchStatus.Success || read.Value is not ReinspectionExecutionDto source) throw new Exception("Conclusion source missing");
        var physicalBefore = await db.Rooms.Where(r => r.RoomId == source.RoomId).Select(r => r.OperationalStatus).SingleAsync(ct);
        var item = await db.InspectionDefects.Where(d => d.DefectId == source.Defects[0].Id).Select(d => d.ChecklistItemId).SingleAsync(ct);
        await using (var projectionTrial = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        {
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.inspection_results(inspection_id,checklist_item_id,outcome) VALUES({inspection},{item},'PASS')", ct);
                throw new Exception("Projection guard accepted result without Fixed decisions");
            }
            catch (SqlException e) when (e.Number == 51999) { }
            await projectionTrial.RollbackAsync(ct);
        }
        var png = new EvidenceUpload("synthetic-reinspection.png", "image/png", "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS1cAAAAASUVORK5CYII=");
        var body = new ConcludeReinspectionRequest(source.ReworkETag, source.BindingETag, "Synthetic exact-defect conclusion",
            source.Defects.Select((d, n) => new ReinspectionDefectInput(d.Id, mode == "PASS", "Checked remediation", n == 0 ? [png] : null)).ToArray());
        var files = Directory.GetFiles(SprintReworkFixture.EvidenceRoot).Length;
        async Task Unchanged()
        {
            var result = await Run();
            if (result.Value is not ReinspectionExecutionDto current || current.SourceETag != source.SourceETag
                || current.ReworkETag != source.ReworkETag || current.BindingETag != source.BindingETag
                || current.Defects.Any(d => d.Fixed != null || d.ReinspectionPhotos.Count != 0)
                || await db.InspectionResults.AnyAsync(r => r.InspectionId == inspection, ct)
                || await db.InspectionDefects.AnyAsync(d => d.InspectionId == inspection, ct)
                || Directory.GetFiles(SprintReworkFixture.EvidenceRoot).Length != files)
                throw new Exception("Rejected conclusion leaked lifecycle/results/evidence");
        }
        foreach (var request in new[] { body with { BindingETag = "\"FFFFFFFFFFFFFFFF\"" }, body with { ReworkETag = "\"FFFFFFFFFFFFFFFF\"" } })
            if ((await Run(Guid.NewGuid(), request, source.SourceETag)).Status != RoomTypeBatchStatus.Stale) throw new Exception("Stale aggregate conclusion admitted");
        if ((await Run(Guid.NewGuid(), body, "\"FFFFFFFFFFFFFFFF\"")).Status != RoomTypeBatchStatus.Stale) throw new Exception("Stale inspection conclusion admitted");
        if ((await Run(Guid.NewGuid(), body with { Defects = [new(long.MaxValue, true)] }, source.SourceETag)).Status != RoomTypeBatchStatus.InvalidReference)
            throw new Exception("Foreign defect conclusion admitted");
        try { await Run(Guid.NewGuid(), body with { Defects = [] }, source.SourceETag); throw new Exception("Missing decisions admitted"); }
        catch (ValidationException) { }
        var invalid = body with { Defects = body.Defects.Select((d, n) => n == 0 ? d with { Photos = [png, new("invalid.png", "image/png", "invalid")] } : d).ToArray() };
        try { await Run(Guid.NewGuid(), invalid, source.SourceETag); throw new Exception("Invalid photo admitted"); }
        catch (ValidationException) { }
        await Unchanged();
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE dbo.audit_logs ADD CONSTRAINT CK_DEV3_ReinspectionConclusionFault CHECK
            (ISNULL(JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.contract'),'')<>'FE05_REINSPECTION_CONCLUSION_V1');
            """, ct);
        try
        {
            try { await Run(Guid.NewGuid(), body, source.SourceETag); throw new Exception("Expected conclusion audit failure missing"); }
            catch (SqlException e) when (e.Number == 547) { }
            catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 547 }) { }
            await Unchanged();
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs DROP CONSTRAINT CK_DEV3_ReinspectionConclusionFault", CancellationToken.None); }
        var operation = Guid.NewGuid();
        Task<RoomTypeBatchResult[]> Conclude() => Task.WhenAll(Run(operation, body, source.SourceETag), Run(operation, body, source.SourceETag));
        var concurrent = Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BLOCK_RACE") == "1"
            ? await SprintReinspectionBlockRaceProbe.RunAsync(db, source, Conclude, () => Run(), ct)
            : await Conclude();
        if (concurrent.Any(r => r.Status != RoomTypeBatchStatus.Success || r.Value is not ReinspectionConclusionResult))
            throw new Exception("Concurrent conclusion failed: " + string.Join(',', concurrent.Select(r => r.Status)));
        var result = (ReinspectionConclusionResult)concurrent[0].Value!;
        if (!Equals(result, concurrent[1].Value) || result.State != (mode == "PASS" ? "PASSED" : "FAILED")
            || result.ReworkState != (mode == "PASS" ? "COMPLETED" : "FAILED")
            || result.EffectiveReadiness != (source.EffectiveReadiness == "BLOCKED" ? "BLOCKED" : mode == "PASS" ? "READY" : "REWORK_REQUIRED")
            || await db.AuditLogs.CountAsync(a => a.EntityType == "REINSPECTION" && a.EntityId == inspection.ToString() && a.Action == "UPDATE", ct) != 1
            || await db.InspectionDefects.CountAsync(d => d.InspectionId == inspection, ct) != (mode == "PASS" ? 0 : body.Defects.Count))
            throw new Exception("Conclusion terminal persistence/readiness/audit mismatch");
        if (await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.room_inspections i JOIN dbo.cleaning_reworks w ON w.rework_id={rework}
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=i.cleaning_task_id
            WHERE i.inspection_id={inspection} AND i.workflow_status={result.State} AND i.status='COMPLETED' AND i.result={mode}
              AND w.workflow_status={result.ReworkState} AND w.status='COMPLETED' AND w.completed_at=i.inspected_at
              AND t.workflow_status={(mode == "PASS" ? "COMPLETED" : "REWORK_REQUIRED")}
              AND t.status={(mode == "PASS" ? "COMPLETED" : "IN_PROGRESS")}
            """).SingleAsync(ct) != 1
            || await db.Rooms.Where(r => r.RoomId == source.RoomId).Select(r => r.OperationalStatus).SingleAsync(ct)
                != (result.EffectiveReadiness == "READY" ? "READY" : physicalBefore)
            || await db.RoomStatusHistories.CountAsync(h => h.TriggerType == "INSPECTION" && h.TriggerId == inspection, ct)
                != (result.EffectiveReadiness == "READY" && physicalBefore != "READY" ? 1 : 0))
            throw new Exception("Independent SQL lifecycle/physical status/history mismatch");
        if ((await Run(operation, body with { Reason = "Changed replay" }, source.SourceETag)).Status != RoomTypeBatchStatus.Duplicate)
            throw new Exception("Conclusion changed operation accepted");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={ticket.UserId} AND branch_id={branch}", ct);
        try { if ((await Run(operation, body, source.SourceETag)).Status == RoomTypeBatchStatus.Success) throw new Exception("Revoked conclusion replay admitted"); }
        finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={ticket.UserId} AND branch_id={branch}", CancellationToken.None); }
    }
    private sealed class Identity(CurrentUserInfo value) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = value; return true; } }
}
