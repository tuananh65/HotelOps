using System.Data;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class InternalRequestAlertObservation(HotelOpsDbContext db)
{
    internal async Task<WorkAlertObservation> ObserveAsync(long requestId, decimal thresholdPercent, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Independent clean alert observation context required.");
        if (!await WorkAlertEpisodeStore.InstalledAsync(db, ct))
            throw new InvalidOperationException("Work alert episode storage required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await db.InternalRequests.FromSqlInterpolated($"""
            SELECT * FROM dbo.internal_requests WITH(HOLDLOCK) WHERE internal_request_id={requestId}
            """).AsNoTracking().SingleOrDefaultAsync(ct);
        // Missing source is not proof of a healthy/closed business record.
        if (request == null) return new(null,WorkAlertCondition.Unknown);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var terminal = request.Status is "CLOSED" or "REJECTED" or "CANCELLED";
        var cycle = await WorkAlertCycle.ReadAsync(db,"INTERNAL_REQUEST",requestId,now,ct);
        var condition = request.Status == "DRAFT" ? WorkAlertCondition.Clear
            : WorkAlertWindow.Evaluate(terminal, request.DueAt, request.CreatedAt, now, thresholdPercent);
        var validStart=request.DueAt>request.CreatedAt?(DateTime?)request.CreatedAt:null;
        var episode = await WorkAlertEpisodeStore.ObserveAsync(db, "INTERNAL_REQUEST", requestId,
            request.BranchId, condition, request.DueAt, validStart, now, ct, cycle);
        await tx.CommitAsync(ct);
        return new(episode,condition);
    }
}
