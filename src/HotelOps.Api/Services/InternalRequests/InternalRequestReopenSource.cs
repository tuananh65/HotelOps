using System.Data;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.InternalRequests;

// UC121 prerequisite only: not a read grant or mutation authorization. The caller
// must authorize the actor and retain this transaction through the eventual write.
public static class InternalRequestReopenSource
{
    public sealed record Closure(long EventId, long AssignmentId, long HandlerId, DateTime ClosedAt, string Result);

    internal static bool WithinWindow(DateTime closedAt, DateTime now, int windowDays) =>
        windowDays > 0 && closedAt <= now && (now - closedAt).TotalDays <= windowDays;

    // windowDays must come from the caller's resolved SYS-PAR-18 policy.
    public static Task<Closure> ReadAsync(HotelOpsDbContext db, long requestId, int windowDays, CancellationToken ct)
        => ReadCoreAsync(db, requestId, windowDays, true, ct);

    // A denied/expired preview still shows the proven prior result and configured window.
    internal static Task<Closure> PreviewAsync(HotelOpsDbContext db, long requestId, int windowDays, CancellationToken ct)
        => ReadCoreAsync(db, requestId, windowDays, false, ct);

    private static async Task<Closure> ReadCoreAsync(HotelOpsDbContext db, long requestId, int windowDays, bool enforceWindow, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Reopen source requires the caller's serializable transaction.");
        if (!await InternalRequestCompletionService.InstalledAsync(db, ct))
            throw new InternalRequestReadException(503, "IR_COMPLETION_SCHEMA_REQUIRED");
        var request = await db.InternalRequests.FromSqlInterpolated($"SELECT * FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={requestId}")
            .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new InternalRequestReadException(404, "IR_NOT_FOUND");
        if (request.Status != "CLOSED") throw new InternalRequestReadException(409, "IR_NOT_CLOSED");
        if (request.ClosedAt == null || request.CompletedAt != request.ClosedAt)
            throw new InternalRequestReadException(409, "IR_CLOSURE_INCONSISTENT");
        if (windowDays <= 0) throw new InternalRequestReadException(503, "IR_REOPEN_WINDOW_UNAVAILABLE");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (enforceWindow && !WithinWindow(request.ClosedAt.Value, now, windowDays))
            throw new InternalRequestReadException(409, "IR_REOPEN_WINDOW_EXPIRED");
        // Bind to the current closure, not MAX(assignment_id) or a mutable handler field.
        // A legacy closed row without its receipt needs reconciliation, never guessing.
        var rows = await db.Database.SqlQuery<ClosureRow>($"""
            SELECT c.event_id AS EventId,c.assignment_id AS AssignmentId,a.assignee_id AS HandlerId,
                   c.closed_at AS ClosedAt,c.result_note AS Result
            FROM dbo.internal_request_closures c
            JOIN dbo.internal_request_events e ON e.event_id=c.event_id AND e.internal_request_id=c.internal_request_id
            JOIN dbo.internal_request_assignments a ON a.assignment_id=c.assignment_id AND a.internal_request_id=c.internal_request_id
            JOIN dbo.audit_logs audit ON audit.audit_id=c.audit_id
            WHERE c.internal_request_id={requestId} AND c.closed_at={request.ClosedAt}
              AND e.event_type='COMPLETE' AND e.old_status='IN_PROGRESS' AND e.new_status='CLOSED'
              AND e.occurred_at=c.closed_at AND e.event_note=c.result_note
              AND a.status='COMPLETED' AND a.ended_at=c.closed_at AND a.assigned_at<=c.closed_at
              AND audit.entity_type='internal_requests' AND audit.entity_id=CONVERT(nvarchar(30),c.internal_request_id)
              AND audit.branch_id={request.BranchId} AND audit.actor_user_id=e.actor_id
              AND audit.action='UPDATE' AND audit.occurred_at=c.closed_at
            """).ToArrayAsync(ct);
        if (rows.Length != 1 || string.IsNullOrWhiteSpace(rows[0].Result)
            || await db.InternalRequestAssignments.AsNoTracking().AnyAsync(a => a.InternalRequestId == requestId && a.Status == "ACTIVE", ct))
            throw new InternalRequestReadException(409, "IR_CLOSURE_INCONSISTENT");
        var row = rows[0];
        return new(row.EventId, row.AssignmentId, row.HandlerId, DateTime.SpecifyKind(row.ClosedAt, DateTimeKind.Utc), row.Result);
    }

    private sealed class ClosureRow
    {
        public long EventId { get; set; }
        public long AssignmentId { get; set; }
        public long HandlerId { get; set; }
        public DateTime ClosedAt { get; set; }
        public string Result { get; set; } = "";
    }
}
