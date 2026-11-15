using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

internal sealed record InspectionFailedSource(long InspectionId, long BranchId, long ActorId, InspectionFailureIntent Intent);

internal static class InspectionFailedNotificationSource
{
    internal const string Kind = "INSPECTION_FAILED";
    internal static InspectionFailureIntent? Read(string? json)
    {
        if (json == null) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("notification", out var value)) return null;
            var intent = value.Deserialize<InspectionFailureIntent>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return intent is { SchemaVersion: 1, Kind: Kind, SupervisorIds: not null, OriginalCleaner: not null }
                && intent.SupervisorIds.All(id => id > 0) && intent.OriginalCleaner.CleanerUserId > 0
                && intent.OriginalCleaner.CompletionRevision is { Length: 8 } ? intent : null;
        }
        catch (JsonException) { return null; }
    }

    internal static async Task<(string Status, InspectionFailedSource? Source)> VerifyAsync(HotelOpsDbContext db, AuditLog? audit, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.Serializable)
            throw new InvalidOperationException("INSPECTION_SOURCE_SERIALIZABLE_TRANSACTION_REQUIRED");
        var intent = Read(audit?.NewValues);
        if (intent == null || audit is not { EntityType: "INSPECTION", Action: "UPDATE", BranchId: > 0, ActorUserId: > 0 }
            || !long.TryParse(audit.EntityId, out var inspection) || inspection <= 0)
            return ("SOURCE_DENIED", null);
        var cleaner = intent.OriginalCleaner;
        if (cleaner.BranchId != audit.BranchId) return ("SOURCE_DENIED", null);
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.fe04_cleaner_attributions','U') IS NOT NULL
              AND OBJECT_ID('dbo.fe04_capture_cleaner_attribution','P') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_fe04_cleaner_immutable','TR')
                AND parent_id=OBJECT_ID('dbo.fe04_cleaner_attributions','U') AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
                AND name='HotelOps.FE04.CleanerAttribution' AND CONVERT(nvarchar(128),value)='R04_V1')
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1) return ("SOURCE_UNAVAILABLE", null);
        var valid = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.room_inspections i WITH(HOLDLOCK)
            JOIN dbo.rooms r ON r.room_id=i.room_id AND r.branch_id={audit.BranchId}
            JOIN dbo.fe04_cleaner_attributions a ON a.cleaning_task_id=i.cleaning_task_id AND a.room_id=i.room_id AND a.branch_id=r.branch_id
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id AND t.room_id=a.room_id AND t.actual_end_at=a.completed_at
            JOIN dbo.cleaning_assignments ca ON ca.assignment_id=a.assignment_id AND ca.cleaning_task_id=a.cleaning_task_id
              AND ca.assignee_user_id=a.cleaner_user_id AND ca.assigned_at<=a.completed_at AND (ca.ended_at IS NULL OR ca.ended_at>=a.completed_at)
            JOIN dbo.cleaning_progress_logs p ON p.progress_id=a.completion_progress_id AND p.cleaning_task_id=a.cleaning_task_id
              AND p.logged_by=a.cleaner_user_id AND p.status='COMPLETED' AND p.progress_percent=100 AND p.logged_at=a.completed_at
            WHERE i.inspection_id={inspection} AND i.inspector_user_id={audit.ActorUserId}
              AND i.workflow_status='FAILED' AND i.status='COMPLETED' AND i.result='FAIL' AND i.created_at>=a.completed_at
              AND a.cleaning_task_id={cleaner.CleaningTaskId} AND a.room_id={cleaner.RoomId}
              AND a.cleaner_user_id={cleaner.CleanerUserId} AND a.assignment_id={cleaner.AssignmentId}
              AND a.completion_progress_id={cleaner.CompletionProgressId} AND a.completion_revision={cleaner.CompletionRevision}
              AND a.completed_at={cleaner.CompletedAtUtc} AND a.writer_transaction_id={cleaner.WriterTransactionId}
            """).SingleAsync(ct);
        if (valid != 1) return ("SOURCE_DENIED", null);
        return ("VERIFIED", new(inspection, audit.BranchId.Value, audit.ActorUserId.Value, intent));
    }
}
