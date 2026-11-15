using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

internal static class SprintReinspectionTerminalReadProbe
{
    // Only invoked inside the parent's identity-verified synthetic fixture; parent owns cleanup.
    internal static async Task VerifyAsync(HotelOpsDbContext db, SessionReference ticket, long sid, long branch, long rework, CancellationToken ct, string? expectedUnblockedReadiness = null)
    {
        var inspection = await db.Database.SqlQuery<long>($"SELECT inspection_id AS Value FROM dbo.fe05_reinspection_bindings_v1 WHERE rework_id={rework}").SingleAsync(ct);
        async Task<RoomTypeBatchResult> Read(long? file = null, long? alternateBranch = null, bool conclude = false, bool original = false)
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
                var value = original ? await service.ReadReinspectionOriginalAsync(ticket, sid, alternateBranch ?? branch, inspection, SprintReworkFixture.EvidenceRoot, ct, file)
                    : await service.ReadReinspectionExecutionAsync(ticket, sid, alternateBranch ?? branch, inspection, SprintReworkFixture.EvidenceRoot, ct, file);
                if (!conclude || value.Value is not ReinspectionExecutionDto source) return value;
                return await service.ConcludeReinspectionAsync(ticket, sid, branch, inspection, source.SourceETag, Guid.NewGuid().ToString("D"),
                    new(source.ReworkETag, source.BindingETag, "Must not reconclude terminal inspection", source.Defects.Select(d => new ReinspectionDefectInput(d.Id, true)).ToArray()),
                    SprintReworkFixture.EvidenceRoot, ct);
            }
            finally { await context.Database.ExecuteSqlRawAsync("REVERT", CancellationToken.None); }
        }
        var response = await Read();
        if (response.Value is not ReinspectionExecutionDto { Conclusion: not null } terminal
            || terminal.State is not ("PASSED" or "FAILED") || terminal.Conclusion.State != terminal.State
            || terminal.Defects.Any(d => d.Fixed == null) || response.ETag != terminal.SourceETag)
            throw new Exception("Protected terminal read failed: " + response.Status);
        var photos = terminal.Defects.SelectMany(d => d.OriginalPhotos.Concat(d.RemediationPhotos).Concat(d.ReinspectionPhotos)).Distinct().ToArray();
        var originalRead = await Read(original: true);
        if (originalRead.Value is not ReinspectionOriginalDto originalSource || originalSource.Id != terminal.OriginalInspectionId
            || originalSource.Evidence.Count == 0) throw new Exception("Terminal original source denied");
        foreach (var file in originalSource.Evidence)
            if ((await Read(file.Id, original: true)).Value is not HotelOps.Api.DTOs.HotelStructure.PrivateHistoryFileDto)
                throw new Exception("Terminal original evidence denied");
        if ((await Read(terminal.Defects.SelectMany(d => d.ReinspectionPhotos).First(), original: true)).Status != RoomTypeBatchStatus.NotFound)
            throw new Exception("Terminal original source admitted conclusion evidence");
        if (!terminal.Defects.Any(d => d.ReinspectionPhotos.Count > 0)) throw new Exception("Terminal conclusion photos missing");
        foreach (var file in photos)
            if ((await Read(file)).Value is not HotelOps.Api.DTOs.HotelStructure.PrivateHistoryFileDto) throw new Exception("Terminal scoped evidence denied");
        if ((await Read(long.MaxValue)).Status != RoomTypeBatchStatus.NotFound || (await Read(alternateBranch: branch + 100000)).Status == RoomTypeBatchStatus.Success)
            throw new Exception("Terminal read leaked foreign scope/evidence");
        if ((await Read(conclude: true)).Status == RoomTypeBatchStatus.Success) throw new Exception("Terminal read reopened mutation authority");
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityType == "REINSPECTION" && a.EntityId == inspection.ToString() && a.Action == "UPDATE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values=N'{{}}' WHERE audit_id={audit.AuditId}", ct);
        try { if ((await Read()).Status == RoomTypeBatchStatus.Success || (await Read(photos[0])).Status == RoomTypeBatchStatus.Success
            || (await Read(original: true)).Status == RoomTypeBatchStatus.Success || (await Read(originalSource.Evidence[0].Id, original: true)).Status == RoomTypeBatchStatus.Success) throw new Exception("Tampered conclusion admitted to terminal read/evidence"); }
        finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values={audit.NewValues} WHERE audit_id={audit.AuditId}", CancellationToken.None); }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={ticket.UserId} AND branch_id={branch}", ct);
        try { if ((await Read()).Status == RoomTypeBatchStatus.Success || (await Read(photos[0])).Status == RoomTypeBatchStatus.Success
            || (await Read(original: true)).Status == RoomTypeBatchStatus.Success || (await Read(originalSource.Evidence[0].Id, original: true)).Status == RoomTypeBatchStatus.Success) throw new Exception("Revoked inspector retained terminal source"); }
        finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={ticket.UserId} AND branch_id={branch}", CancellationToken.None); }
        var blocks = await db.RoomBlocks.AsNoTracking().Where(b => b.RoomId == terminal.RoomId && b.ReleasedAt == null).Select(b => b.BlockId).ToArrayAsync(ct);
        var currentReadiness = expectedUnblockedReadiness ?? (terminal.State == "PASSED" ? "READY" : "REWORK_REQUIRED");
        if (blocks.Length == 0 && terminal.EffectiveReadiness != currentReadiness)
            throw new Exception("Historical inspection did not expose current room readiness");
        if (blocks.Length > 0)
        {
            if (terminal.EffectiveReadiness != "BLOCKED" || terminal.Conclusion.EffectiveReadiness != "BLOCKED") throw new Exception("Expected blocked terminal source");
            await db.RoomBlocks.Where(b => blocks.Contains(b.BlockId)).ExecuteUpdateAsync(s => s.SetProperty(b => b.ReleasedAt, DateTime.UtcNow), ct);
            try
            {
                var released = await Read();
                if (released.Value is not ReinspectionExecutionDto current || current.EffectiveReadiness != currentReadiness
                    || current.Conclusion != terminal.Conclusion || current.SourceETag != terminal.SourceETag || current.ReworkETag != terminal.ReworkETag)
                    throw new Exception("Terminal read reused historical readiness after block release");
            }
            finally { await db.RoomBlocks.Where(b => blocks.Contains(b.BlockId)).ExecuteUpdateAsync(s => s.SetProperty(b => b.ReleasedAt, (DateTime?)null), CancellationToken.None); }
        }
        var after = await Read();
        if (after.Value is not ReinspectionExecutionDto unchanged || unchanged.SourceETag != terminal.SourceETag
            || unchanged.BindingETag != terminal.BindingETag || unchanged.ReworkETag != terminal.ReworkETag
            || await db.AuditLogs.CountAsync(a => a.EntityType == "REINSPECTION" && a.EntityId == inspection.ToString() && a.Action == "UPDATE", ct) != 1)
            throw new Exception("Terminal read mutated source or audit");
    }
    private sealed class Identity(CurrentUserInfo value) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = value; return true; } }
}
