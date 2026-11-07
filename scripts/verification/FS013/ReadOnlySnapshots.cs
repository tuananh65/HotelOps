using Microsoft.Data.SqlClient;
using System.Text.Json;

// Developer-only snapshots of the same SQL rows/projections as Program's EF paths.
// Avoid rebuilding the full EF model for each read-only child process. Never handles API writes.
internal static class ReadOnlySnapshots
{
    internal static bool Handles(string mode) => mode is "lifecycle-inspect" or "candidate-inspect" or "candidate-lifecycle-inspect";
    internal static async Task<object> ReadAsync(string connection, string mode, CancellationToken ct)
    {
        await using var sql = new SqlConnection(connection);
        await sql.OpenAsync(ct);
        await using (var guard = sql.CreateCommand())
        {
            guard.CommandText = """
                SELECT CASE WHEN DB_NAME()='HotelOps_Canonical_20261106_f130001_Fresh'
                AND CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=HOST_NAME()
                AND CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))='SQLEXPRESS'
                AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS013.Owner' AND CONVERT(nvarchar(100),value)='DEV1_FS013')
                THEN 1 ELSE 0 END
                """;
            if (Convert.ToInt32(await guard.ExecuteScalarAsync(ct)) != 1) throw new InvalidOperationException("FS013_SNAPSHOT_SCOPE_REQUIRED");
            if (mode.StartsWith("candidate-", StringComparison.Ordinal))
            {
                guard.CommandText = """
                    SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS013.ExecutableCandidate' AND CONVERT(nvarchar(100),value)='V1')
                    AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS013.SqlBoundary' AND CONVERT(nvarchar(100),value)='V1')
                    AND OBJECT_ID('dbo.fs013_assign_with_evidence','P') IS NOT NULL AND OBJECT_ID('dbo.fs009_verify_assignment_guard','P') IS NOT NULL
                    AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_fs009_publication_immutable','TR_fs009_operation_immutable','TR_fs009_publication_source') AND is_disabled=0)=3
                    AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_publications') AND name='UX_fs009_publication_operation' AND is_unique=1 AND is_disabled=0)
                    THEN 1 ELSE 0 END
                    """;
                if (Convert.ToInt32(await guard.ExecuteScalarAsync(ct)) != 1) throw new InvalidOperationException("FS013_SNAPSHOT_CANDIDATE_REQUIRED");
            }
        }
        async Task<object[]> Rows(string text)
        {
            await using var command = sql.CreateCommand(); command.CommandText = text;
            await using var reader = await command.ExecuteReaderAsync(ct);
            var rows = new List<object>();
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++) row.Add(reader.GetName(i), await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i));
                rows.Add(row);
            }
            return rows.ToArray();
        }
        if (mode == "candidate-inspect") return new
        {
            operations = await Rows("SELECT actor_user_id AS actorUserId,operation_key AS operationKey,completed AS completed,result_json AS resultJson FROM dbo.notification_operation_reservations"),
            publications = await Rows("SELECT publication_id AS publicationId,actor_user_id AS actorUserId,operation_key AS operationKey,assignment_id AS assignmentId,payload_json AS payloadJson FROM dbo.notification_publications"),
            intents = await Rows("SELECT intent_id AS intentId,publication_id AS publicationId,user_id AS userId,status AS status,attempt_count AS attemptCount,failure_code AS failureCode,claim_id AS claimId,claimed_until AS claimedUntil,inbox_notification_id AS inboxNotificationId FROM dbo.notification_delivery_intents"),
            inbox = await Rows("SELECT r.notification_id AS notificationId,r.user_id AS userId,r.delivery_channel AS deliveryChannel,r.delivery_status AS deliveryStatus,n.action_url AS actionUrl FROM dbo.notification_recipients r JOIN dbo.notifications n ON n.notification_id=r.notification_id WHERE n.notification_type='CLEANING_TASK_ASSIGNED'")
        };
        if (mode == "lifecycle-inspect") return new
        {
            target = "HotelOps_Canonical_20261106_f130001_Fresh",
            rooms = await Rows("SELECT room_id AS roomId,room_number AS roomNumber,branch_id AS branchId,operational_status AS operationalStatus FROM dbo.rooms WHERE room_number LIKE N'FS013-B1-%' ORDER BY room_id"),
            tasks = await Rows("SELECT t.cleaning_task_id AS cleaningTaskId,t.room_id AS roomId,t.task_no AS taskNo,t.status AS status,t.due_at AS dueAt FROM dbo.cleaning_tasks t JOIN dbo.rooms r ON r.room_id=t.room_id WHERE r.room_number LIKE N'FS013-B1-%'"),
            cycles = await Rows("SELECT room_id AS roomId,cleaning_task_id AS taskId FROM dbo.room_cleaning_cycle_state"),
            events = await Rows("SELECT room_id AS roomId,cleaning_task_id AS taskId,readiness AS readiness,has_effective_block AS blocked FROM dbo.room_readiness_events"),
            audits = await Rows("SELECT entity_id AS entityId,actor_user_id AS actorUserId,branch_id AS branchId FROM dbo.audit_logs WHERE entity_type='cleaning_tasks'"),
            assignments = await Rows("SELECT assignment_id AS assignmentId,cleaning_task_id AS cleaningTaskId,assignee_user_id AS assigneeUserId FROM dbo.cleaning_assignments")
        };
        await using var count = sql.CreateCommand(); count.CommandText = "SELECT COUNT(*) FROM dbo.audit_logs";
        var auditCount = Convert.ToInt32(await count.ExecuteScalarAsync(ct));
        return new
        {
            commands = await Rows("SELECT actor_user_id AS actor,operation_key AS operation,result_json AS resultJson FROM dbo.fs013_reassignment_operations ORDER BY occurred_at,actor_user_id,operation_key"),
            notices = await Rows("SELECT intent_id AS id,operation_key AS operation,user_id AS userId,recipient_kind AS recipientKind,status AS status,inbox_notification_id AS inboxId,failure_code AS failureCode FROM dbo.fs013_reassignment_intents ORDER BY intent_id"),
            intervals = await Rows("SELECT interval_id AS intervalId,work_item_id AS workItemId,user_id AS userId,opened_at AS openedAt,closed_at AS closedAt,closed_by AS closedBy FROM dbo.work_intervals ORDER BY interval_id"),
            tasks = await Rows("SELECT cleaning_task_id AS cleaningTaskId,status AS status,actual_start_at AS actualStartAt FROM dbo.cleaning_tasks ORDER BY cleaning_task_id"),
            assignments = await Rows("SELECT assignment_id AS assignmentId,cleaning_task_id AS cleaningTaskId,assignee_user_id AS assigneeUserId,assigned_at AS assignedAt,ended_at AS endedAt FROM dbo.cleaning_assignments ORDER BY assignment_id"),
            delivered = await Rows("SELECT i.operation_key AS operation,i.recipient_kind AS recipientKind,n.notification_id AS notificationId,n.action_url AS actionUrl,n.source_id AS sourceId FROM dbo.fs013_reassignment_intents i JOIN dbo.notifications n ON n.notification_id=i.inbox_notification_id ORDER BY i.intent_id"),
            auditCount
        };
    }
}
