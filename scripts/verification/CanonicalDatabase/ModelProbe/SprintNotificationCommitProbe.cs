using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.EntityFrameworkCore;

internal static class SprintNotificationCommitProbe
{
    internal static async Task CheckAsync(HotelOpsDbContext db, AuditLog audit, long receiver, long inspection, CancellationToken ct)
    {
        var type = typeof(RoomTypeCreationService).Assembly.GetType("HotelOps.Api.Services.Notifications.InspectionPassedNotificationDelivery")!;
        var delivery = Activator.CreateInstance(type, db, TimeProvider.System)!;
        async Task<Array> Pending(long after) => (Array)(await Result(type.GetMethod("ReadPendingAsync")!.Invoke(delivery,[100,after,ct])!))!;
        async Task<string> Send(HotelOpsDbContext context) => await (Task<string>)type.GetMethod("DeliverAsync")!.Invoke(
            Activator.CreateInstance(type,context,TimeProvider.System),[audit.AuditId,receiver,ct])!;
        var tx = db.Database.CurrentTransaction!;
        // Prove cleanup, including removal of only the freshly created candidate schema,
        // before committing any synthetic setup. Rollback restores the entire test graph.
        await tx.CreateSavepointAsync("notification_cleanup", ct);
        await CleanupAsync(db, audit.ActorUserId!.Value, receiver, audit.BranchId!.Value, inspection, ct);
        await tx.RollbackToSavepointAsync("notification_cleanup", ct);
        try
        {
            await tx.CommitAsync(ct);
            await tx.DisposeAsync();
            if ((await Pending(0)).Length != 1 || (await Pending(audit.AuditId)).Length != 0)
                throw new Exception("Durable event discovery/cursor mismatch");
            // Failure occurs after business commit, in a genuinely separate delivery transaction.
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients ADD CONSTRAINT CK_DEV3_142A_NotificationFault CHECK(user_id<0)", ct);
            try { await Send(db); throw new Exception("Committed delivery fault ignored"); }
            catch(DbUpdateException e) when(e.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number==547 && sql.Message.Contains("CK_DEV3_142A_NotificationFault")) { }
            db.ChangeTracker.Clear();
            if (!await db.RoomInspections.AnyAsync(i=>i.InspectionId==inspection&&i.Result=="PASS",ct)
                || !await db.AuditLogs.AnyAsync(a=>a.AuditId==audit.AuditId,ct)
                || await db.Notifications.AnyAsync(ct) || await db.NotificationRecipients.AnyAsync(ct)
                || (await Pending(0)).Length != 1)
                throw new Exception("Delivery failure changed committed approval or lost retry event");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients DROP CONSTRAINT CK_DEV3_142A_NotificationFault", ct);

            // Two physical connections race the same persisted event. No HTTP mocks or shared DB.
            var options = new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options;
            await using var first = new HotelOpsDbContext(options);
            await using var second = new HotelOpsDbContext(options);
            var outcomes = await Task.WhenAll(Send(first), Send(second));
            if (!outcomes.Order().SequenceEqual(new[]{"ALREADY_DELIVERED","DELIVERED"})
                || await db.Notifications.CountAsync(ct)!=1 || await db.NotificationRecipients.CountAsync(ct)!=1
                || (await Pending(0)).Length!=0 || await Send(db)!="ALREADY_DELIVERED")
                throw new Exception("Committed concurrent retry duplicated notification or lost receipt");
            Console.WriteLine("PASS committed notification: durable discovery, delivery fault preserves business, retry, two-connection race, replay");
        }
        finally
        {
            db.ChangeTracker.Clear();
            if (db.Database.CurrentTransaction != null) await db.Database.CurrentTransaction.DisposeAsync();
            // Guarded fixture IDs, no broad user/data delete and no existing shared object replacement.
            if (await db.Users.AnyAsync(u=>u.UserId==audit.ActorUserId&&u.Username=="D3-EVIDENCE",CancellationToken.None))
            {
                await using var cleanup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,CancellationToken.None);
                await CleanupAsync(db,audit.ActorUserId!.Value,receiver,audit.BranchId!.Value,inspection,CancellationToken.None);
                await cleanup.CommitAsync(CancellationToken.None);
            }
        }
    }

    private static async Task<object?> Result(object value)
    {
        var task=(Task)value; await task;
        return task.GetType().GetProperty("Result")!.GetValue(task);
    }

    internal static Task CleanupAsync(HotelOpsDbContext db,long actor,long receiver,long branch,long inspection,CancellationToken ct)
        => db.Database.ExecuteSqlInterpolatedAsync($"""
            IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
              OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
              OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
              OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
              THROW 51995,'Exact owned disposable cleanup required',1;
            DECLARE @task bigint=(SELECT cleaning_task_id FROM dbo.room_inspections WHERE inspection_id={inspection});
            DECLARE @room bigint=(SELECT room_id FROM dbo.room_inspections WHERE inspection_id={inspection});
            IF @task IS NULL OR EXISTS(SELECT 1 FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id<>@task OR cleaner_user_id<>{receiver})
              THROW 51995,'Unexpected attribution ownership',1;
            -- This schema was created fresh by SprintCleanerAttribution.sql in this very trial.
            -- Remove the isolated candidate table as a whole; never disable its immutability trigger.
            DROP PROCEDURE dbo.fe04_capture_cleaner_attribution;
            DROP TABLE dbo.fe04_cleaner_attributions;
            EXEC sys.sp_dropextendedproperty @name=N'HotelOps.FE04.CleanerAttribution';
            IF OBJECT_ID('dbo.CK_DEV3_142A_NotificationFault','C') IS NOT NULL ALTER TABLE dbo.notification_recipients DROP CONSTRAINT CK_DEV3_142A_NotificationFault;
            DELETE r FROM dbo.notification_recipients r JOIN dbo.notifications n ON n.notification_id=r.notification_id
              WHERE n.source_type='INSPECTION' AND n.source_id={inspection} AND n.created_by={actor};
            DELETE dbo.notifications WHERE source_type='INSPECTION' AND source_id={inspection} AND created_by={actor};
            DECLARE @version bigint=(SELECT version_id FROM dbo.room_inspections WHERE inspection_id={inspection});
            DECLARE @list bigint=(SELECT checklist_id FROM dbo.inspection_checklist_versions WHERE version_id=@version);
            DECLARE @type bigint=(SELECT room_type_id FROM dbo.rooms WHERE room_id=@room);
            DECLARE @floor bigint=(SELECT floor_id FROM dbo.rooms WHERE room_id=@room);
            DECLARE @cleanType bigint=(SELECT cleaning_type_id FROM dbo.cleaning_tasks WHERE cleaning_task_id=@task);
            UPDATE dbo.room_inspections SET workflow_status='INSPECTING',status='IN_PROGRESS',result=NULL WHERE inspection_id={inspection};
            DELETE dbo.inspection_results WHERE inspection_id={inspection};
            DELETE dbo.room_inspections WHERE inspection_id={inspection};
            DELETE dbo.inspection_checklist_items WHERE version_id=@version;
            DELETE dbo.inspection_checklist_versions WHERE version_id=@version;
            DELETE dbo.inspection_checklists WHERE checklist_id=@list;
            DELETE dbo.room_cleaning_cycle_state WHERE room_id=@room AND cleaning_task_id=@task;
            DELETE dbo.cleaning_progress_logs WHERE cleaning_task_id=@task;
            DELETE dbo.cleaning_assignments WHERE cleaning_task_id=@task;
            DELETE dbo.cleaning_tasks WHERE cleaning_task_id=@task;
            DELETE dbo.room_status_history WHERE room_id=@room AND trigger_type='INSPECTION' AND trigger_id={inspection} AND changed_by={actor};
            DELETE dbo.rooms WHERE room_id=@room;
            DELETE dbo.room_types WHERE room_type_id=@type;
            DELETE dbo.floors WHERE floor_id=@floor;
            DELETE dbo.reference_values WHERE reference_id=@cleanType AND value_code='D3-SYNTHETIC';
            DELETE dbo.user_roles WHERE user_id IN({actor},{receiver});
            DELETE dbo.user_branch_access WHERE user_id IN({actor},{receiver});
            DELETE dbo.user_sessions WHERE user_id IN({actor},{receiver});
            DELETE dbo.audit_logs WHERE actor_user_id IN({actor},{receiver}) OR(entity_type='users' AND entity_id IN(CONVERT(nvarchar(50),{actor}),CONVERT(nvarchar(50),{receiver})));
            DELETE dbo.users WHERE user_id={receiver} AND username='D3-CLEANER';
            DELETE dbo.users WHERE user_id={actor} AND username='D3-EVIDENCE';
            DELETE dbo.branches WHERE branch_id={branch} AND branch_code='D3-EVIDENCE';
            """,ct);
}
