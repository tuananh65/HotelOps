using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Housekeeping;

// Read projection over the existing FS013 graph. No ledger, writer or generated mapping.
public sealed class CleaningAssignmentNotificationSnapshot
{
    public long AssignmentId { get; set; }
    public long TaskId { get; set; }
    public long RoomId { get; set; }
    public long BranchId { get; set; }
    public long ActorId { get; set; }
    public long RecipientId { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string AssignmentStatus { get; set; } = "";
    public string WorkflowState { get; set; } = "";
    public DateTime? DueAt { get; set; }
    public long CurrentCycleId { get; set; }
    public byte[] CurrentRevision { get; set; } = [];
    public Guid OperationId { get; set; }
    public Guid PublicationId { get; set; }
    public byte[] Fingerprint { get; set; } = [];
    public int FingerprintVersion { get; set; }
    public bool Completed { get; set; }
    public string ResultJson { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public byte[] SourceRevision { get; set; } = [];
    public string EventType { get; set; } = "";
    public int SchemaVersion { get; set; }
    public bool HasAudit { get; set; }
    public bool HasIntent { get; set; }
    public bool RoomActive { get; set; }
}

public sealed record CleaningAssignmentNotificationContent(long OccurrenceId, CandidateAssignmentEvent Facts,
    string ReadPolicy, string Title, string Message, string ActionPath);

public static class CleaningAssignmentNotificationRules
{
    public const string EventType = "CLEANING_TASK_ASSIGNED";
    public const short Version = 1;
    // Policy identifier, not a new permission. The evaluator uses existing CLEANING_VIEW.
    public const string ReadPolicy = "cleaning.assignment.view.v1";

    public static CleaningAssignmentNotificationContent Resolve(CleaningAssignmentNotificationSnapshot s, bool staging)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var f = JsonSerializer.Deserialize<CandidateAssignmentEvent>(s.PayloadJson, json)
            ?? throw new InvalidOperationException("UC029_PAYLOAD_REQUIRED");
        var r = ProvisionalCleaningNotification.ReadResult(s.ResultJson);
        if (s.AssignmentId <= 0 || s.TaskId <= 0 || s.RoomId <= 0 || s.BranchId <= 0 || s.ActorId <= 0 || s.RecipientId <= 0 ||
            s.OperationId == Guid.Empty || s.PublicationId == Guid.Empty || s.Fingerprint.Length != 32 ||
            s.FingerprintVersion != 1 || !s.Completed || !s.HasAudit || !s.HasIntent ||
            s.EventType != EventType || s.SchemaVersion != Version || s.SourceRevision.Length != 8 ||
            f.OperationId != s.OperationId || f.PublicationId != s.PublicationId || f.ActorId != s.ActorId ||
            f.AssignmentId != s.AssignmentId || f.TaskId != s.TaskId || f.TaskCycleId != s.TaskId ||
            f.RoomId != s.RoomId || f.BranchId != s.BranchId || f.AssigneeUserId != s.RecipientId ||
            f.AssignedAtUtc.Ticks != s.AssignedAt.Ticks || !CleaningLifecycleRules.StrongTag(f.SourceRevision) ||
            f.SourceRevision != $"\"{Convert.ToHexString(s.SourceRevision)}\"" ||
            r.Id != s.TaskId || r.AssignmentId != s.AssignmentId || r.BranchId != s.BranchId ||
            r.AssigneeUserId != s.RecipientId || r.OperationId != s.OperationId || r.PublicationId != s.PublicationId ||
            r.SourceETag != f.SourceRevision || r.WorkflowState != "ASSIGNED" || r.Notification != "PENDING_DELIVERY" ||
            r.Contract != "PROVISIONAL_NOT_ACCEPTED")
            throw new InvalidOperationException("UC029_PERSISTED_GRAPH_MISMATCH");
        if (!s.RoomActive || s.CurrentCycleId != s.TaskId || s.AssignmentStatus != "ACTIVE" || s.EndedAt != null ||
            s.WorkflowState == "CANCELLED") throw new InvalidOperationException("UC029_SOURCE_INVALIDATED");
        if (staging && (s.WorkflowState != "ASSIGNED" || !s.CurrentRevision.SequenceEqual(s.SourceRevision) ||
            s.DueAt?.Ticks != f.DueAtUtc.Ticks)) throw new InvalidOperationException("UC029_SOURCE_REVISION_CHANGED");
        // Occurrence is the persisted assignment identity. Operation/publication remain full GUIDs.
        return new(s.AssignmentId, f, ReadPolicy, "Cleaning task assigned", "A cleaning task has been assigned to you.",
            $"/cleaning/tasks/{s.TaskId}?branchId={s.BranchId}&assignmentId={s.AssignmentId}");
    }

    public static void RequireCommand(CleaningAssignmentNotificationSnapshot s, long actor, Guid operation, byte[] fingerprint)
    {
        if (actor != s.ActorId || operation != s.OperationId || fingerprint.Length != 32 ||
            !CryptographicOperations.FixedTimeEquals(fingerprint, s.Fingerprint))
            throw new InvalidOperationException("UC029_OPERATION_FINGERPRINT_MISMATCH");
    }
}

public sealed class CleaningAssignmentNotificationSource(HotelOpsDbContext db, RoomTypeCreationService source, TimeProvider clock)
{
    public async Task<CleaningAssignmentNotificationSnapshot> LoadAsync(long assignmentId, CancellationToken ct)
    {
        if (assignmentId <= 0) throw new ArgumentOutOfRangeException(nameof(assignmentId));
        // Existing lifecycle columns are intentionally projected rather than added to generated entities.
        return await db.Database.SqlQuery<CleaningAssignmentNotificationSnapshot>($"""
            SELECT a.assignment_id AS AssignmentId, a.cleaning_task_id AS TaskId, t.room_id AS RoomId,
              r.branch_id AS BranchId, a.assigned_by AS ActorId, a.assignee_user_id AS RecipientId,
              a.assigned_at AS AssignedAt, a.ended_at AS EndedAt, a.assignment_status AS AssignmentStatus,
              t.workflow_status AS WorkflowState, t.due_at AS DueAt, c.cleaning_task_id AS CurrentCycleId,
              CONVERT(varbinary(8),t.row_version) AS CurrentRevision, p.operation_key AS OperationId,
              p.publication_id AS PublicationId, o.fingerprint AS Fingerprint, o.fingerprint_version AS FingerprintVersion,
              o.completed AS Completed, o.result_json AS ResultJson, p.payload_json AS PayloadJson,
              p.source_revision AS SourceRevision, p.event_type AS EventType, p.schema_version AS SchemaVersion,
              r.is_active AS RoomActive,
              CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.audit_logs l
                WHERE l.entity_type='cleaning_tasks' AND l.entity_id=CONVERT(nvarchar(30),t.cleaning_task_id)
                  AND l.actor_user_id=a.assigned_by AND l.branch_id=r.branch_id
                  AND TRY_CONVERT(bigint,JSON_VALUE(l.new_values,'$.assignmentId'))=a.assignment_id
                  AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(l.new_values,'$.operationId'))=p.operation_key)
                THEN 1 ELSE 0 END) AS HasAudit,
              CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.notification_delivery_intents i
                WHERE i.publication_id=p.publication_id AND i.user_id=a.assignee_user_id AND i.delivery_channel='IN_APP')
                THEN 1 ELSE 0 END) AS HasIntent
            FROM dbo.cleaning_assignments a
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id
            JOIN dbo.rooms r ON r.room_id=t.room_id
            JOIN dbo.room_cleaning_cycle_state c ON c.room_id=r.room_id
            JOIN dbo.notification_publications p ON p.assignment_id=a.assignment_id AND p.actor_user_id=a.assigned_by
            JOIN dbo.notification_operation_reservations o ON o.actor_user_id=p.actor_user_id AND o.operation_key=p.operation_key
            WHERE a.assignment_id={assignmentId}
            """).SingleAsync(ct);
    }

    public async Task<CleaningAssignmentNotificationContent> ResolveAsync(long assignmentId, bool staging, CancellationToken ct)
    {
        var s = await LoadAsync(assignmentId, ct);
        var content = CleaningAssignmentNotificationRules.Resolve(s, staging);
        if (s.AssignedAt > clock.GetUtcNow().UtcDateTime ||
            !await source.CanReadAssignedCleaningAsync(s.RecipientId, s.BranchId, s.TaskId, s.AssignmentId, ct))
            throw new InvalidOperationException("UC029_SOURCE_READ_DENIED");
        return content;
    }
}
