using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

// Read-only bridge from a delivered outbox event to the supervisor's existing monitor.
// Neither a stored ActionUrl nor the notification's task ID is sufficient source proof.
internal static class CleaningClosureNotificationSource
{
    internal sealed class Proof
    {
        public Guid PublicationId { get; set; }
        public Guid OperationId { get; set; }
        public long ActorId { get; set; }
        public long AssignmentId { get; set; }
        public string EventName { get; set; } = "";
        public byte[] Revision { get; set; } = [];
        public DateTime At { get; set; }
        public string ResultJson { get; set; } = "";
    }

    internal static CleaningClosureResult? Read(Proof proof, string kind, long task, long actor)
    {
        if (kind is not ("CLEANING_TASK_COMPLETED" or "CLEANING_BLOCKER_REPORTED")
            || task <= 0 || actor <= 0 || proof.ActorId != actor || proof.EventName != kind
            || proof.PublicationId == Guid.Empty || proof.OperationId == Guid.Empty
            || proof.AssignmentId <= 0 || proof.Revision is not { Length: 8 }) return null;
        try
        {
            var receipt = JsonSerializer.Deserialize<CleaningClosureResult>(proof.ResultJson, ProvisionalCleaningNotification.Json);
            return receipt != null && receipt.BranchId > 0
                && RoomTypeCreationService.ValidClosureReceipt(receipt, receipt.BranchId, task,
                    proof.AssignmentId, proof.OperationId, kind == "CLEANING_TASK_COMPLETED")
                && receipt.PublicationId == proof.PublicationId && receipt.OccurredAtUtc == proof.At
                && receipt.SourceETag == '"' + Convert.ToHexString(proof.Revision) + '"'
                ? receipt : null;
        }
        catch (JsonException) { return null; }
    }

    internal static async Task<string?> ResolveAsync(HotelOpsDbContext db, long notification, long user,
        string kind, long task, long actor, DateTime now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Caller SERIALIZABLE transaction required.");
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.notification_operation_reservations','U') IS NOT NULL
            AND OBJECT_ID('dbo.notification_publications','U') IS NOT NULL
            AND COL_LENGTH('dbo.notification_delivery_intents','inbox_notification_id') IS NOT NULL
            THEN 1 ELSE 0 END AS [Value]
            """).SingleAsync(ct) != 1) return null;
        var proofs = await db.Database.SqlQuery<Proof>($"""
            SELECT TOP(2) p.publication_id AS PublicationId,p.operation_key AS OperationId,
                p.actor_user_id AS ActorId,p.assignment_id AS AssignmentId,p.event_type AS EventName,
                p.source_revision AS Revision,p.created_at AS At,o.result_json AS ResultJson
            FROM dbo.notification_delivery_intents i
            JOIN dbo.notification_publications p ON p.publication_id=i.publication_id
            JOIN dbo.notification_operation_reservations o ON o.actor_user_id=p.actor_user_id AND o.operation_key=p.operation_key
            WHERE i.inbox_notification_id={notification} AND i.user_id={user}
              AND i.status='DELIVERED' AND i.delivery_channel='IN_APP'
              AND p.event_type={kind} AND p.schema_version=1 AND p.actor_user_id={actor}
              AND o.completed=1 AND o.fingerprint_version=1 AND DATALENGTH(o.fingerprint)=32
            """).ToArrayAsync(ct);
        var receipt = proofs.Length == 1 ? Read(proofs[0], kind, task, actor) : null;
        if (receipt == null || receipt.OccurredAtUtc > now
            || !(await ProvisionalCleaningNotification.ClosureRecipientsAsync(db, receipt.BranchId, now, ct)).Contains(user)) return null;
        if (!await db.CleaningProgressLogs.AsNoTracking().AnyAsync(p => p.ProgressId == receipt.ProgressId
            && p.CleaningTaskId == task && p.LoggedBy == actor && p.LoggedAt == receipt.OccurredAtUtc
            && p.Status == (kind == "CLEANING_TASK_COMPLETED" ? "COMPLETED" : "BLOCKED")
            && p.CleaningTask.Room.BranchId == receipt.BranchId, ct)
            || !await db.CleaningAssignments.AsNoTracking().AnyAsync(a => a.AssignmentId == receipt.AssignmentId
                && a.CleaningTaskId == task && a.AssigneeUserId == actor
                && a.AssignedAt <= receipt.OccurredAtUtc && (a.EndedAt == null || a.EndedAt >= receipt.OccurredAtUtc), ct)) return null;
        var action = kind == "CLEANING_TASK_COMPLETED" ? "COMPLETE_CLEANING" : "REPORT_CLEANING_BLOCKER";
        var audits = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM dbo.audit_logs
            WHERE actor_user_id={actor} AND branch_id={receipt.BranchId} AND entity_type='cleaning_tasks'
              AND entity_id={task.ToString(System.Globalization.CultureInfo.InvariantCulture)} AND action='UPDATE'
              AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.operationId'))={receipt.OperationId}
              AND JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.action')={action}
              AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.progressId'))={receipt.ProgressId}
            """).SingleAsync(ct);
        return audits == 1 ? $"/cleaning/tasks/{task}?branchId={receipt.BranchId}&monitor=1" : null;
    }
}
