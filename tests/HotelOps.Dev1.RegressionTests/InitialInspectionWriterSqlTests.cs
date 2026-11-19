using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

[Collection("Transfer access database")]
public sealed class InitialInspectionWriterSqlTests
{
    private sealed class Actor(long id, long sid) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(id, sid, false); return true; } }

    [HousekeepingWriterSqlFact]
    public async Task RestrictedWriterChecksAuthorityVersionsReplayAndAtomicNotificationIntent()
    {
        var primary = Environment.GetEnvironmentVariable("TRANSFER_REQUEST_TEST_CONNECTION")!;
        var restricted = Environment.GetEnvironmentVariable("INSPECTION_WRITER_TEST_CONNECTION")!;
        Assert.Matches("^HotelOps_TransferTest_[a-f0-9]{32}$", new SqlConnectionStringBuilder(primary).InitialCatalog);
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(primary).Options);
        var mai = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "mai.tran");
        var branch = mai.PrimaryBranchId!.Value;
        var now = DateTime.UtcNow; var raw = SessionCredential.Generate();
        var session = new UserSession { UserId = mai.UserId, SessionKeyHash = SessionCredential.Hash(raw), CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddMinutes(20) };
        db.UserSessions.Add(session); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var ticket = SessionReference.FromProtectedTicket(mai.UserId, raw, false, now);
        var actor = new Actor(mai.UserId, session.SessionId);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:HotelOpsDb"] = primary, ["ConnectionStrings:InspectionAssignmentDb"] = restricted }).Build();
        var writer = new InitialInspectionWriter(config, actor, TimeProvider.System);
        var token = Guid.NewGuid().ToString("N")[..10];
        var task = await db.Database.SqlQuery<long>($"""
            SET NOCOUNT ON;
            DECLARE @list bigint,@version bigint,@type bigint,@room bigint,@task bigint;
            INSERT dbo.inspection_checklists(checklist_code,checklist_name) VALUES({token},N'Writer isolated checklist'); SET @list=SCOPE_IDENTITY();
            INSERT dbo.inspection_checklist_versions(checklist_id,version_no,status) VALUES(@list,1,'DRAFT'); SET @version=SCOPE_IDENTITY();
            INSERT dbo.inspection_checklist_items(version_id,criterion,is_required,sort_order) VALUES(@version,N'Isolated criterion',1,1);
            UPDATE dbo.inspection_checklist_versions SET status='PUBLISHED',published_by={mai.UserId},effective_at=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE version_id=@version;
            INSERT dbo.room_types(type_code,type_name,standard_capacity,branch_id,standard_cleaning_minutes,default_checklist_id)
            VALUES({token},N'Writer isolated type',1,{branch},30,@list); SET @type=SCOPE_IDENTITY();
            INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status)
            SELECT TOP(1) {branch},floor_id,@type,{token},'INSPECTION' FROM dbo.floors WHERE branch_id={branch} AND is_active=1; SET @room=SCOPE_IDENTITY();
            INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,created_by,workflow_status,created_at)
            SELECT TOP(1) {token},@room,cleaning_type_id,'IN_PROGRESS',{mai.UserId},'PENDING_INSPECTION',SYSUTCDATETIME() FROM dbo.cleaning_tasks; SET @task=SCOPE_IDENTITY();
            INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES(@room,@task);
            SET NOCOUNT OFF; SELECT @task AS Value;
            """).ToArrayAsync();
        var id = Assert.Single(task);
        var read = await writer.ExecuteAsync(s => s.ReadInitialInspectionOptionsAsync(ticket, session.SessionId, branch, id, default), default);
        Assert.Equal(RoomTypeBatchStatus.Success, read.Status);
        var options = Assert.IsType<InitialInspectionOptions>(read.Value);
        Assert.True(options.CanAssign); Assert.Contains(options.Inspectors, u => u.Id == mai.UserId);
        var input = new InitialInspectionAssignmentRequest(mai.UserId, options.ChecklistVersionId!.Value, options.CycleETag, DateTime.UtcNow.AddHours(1), "Isolated writer verification");
        Task<RoomTypeBatchResult> Call(Guid op, InitialInspectionAssignmentRequest? body = null, string? tag = null, long? scope = null)
            => writer.ExecuteAsync(s => s.AssignInitialInspectionAsync(ticket, session.SessionId, scope ?? branch, id, tag ?? read.ETag, op.ToString(), body ?? input, default), default);
        async Task NoMutation()
        { Assert.False(await db.RoomInspections.AnyAsync(i => i.CleaningTaskId == id)); }

        await using (var limited = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(restricted).Options))
        {
            foreach (var table in new[] { "notification_operation_reservations", "fe04_rework_authority_issuers", "audit_logs", "room_inspections", "user_roles" })
                foreach (var permission in new[] { "INSERT", "UPDATE", "DELETE" })
                    Assert.Equal(0, await limited.Database.SqlQuery<int>($"SELECT HAS_PERMS_BY_NAME({"dbo." + table},'OBJECT',{permission}) AS Value").SingleAsync());
        }
        Assert.Equal(RoomTypeBatchStatus.Stale, (await Call(Guid.NewGuid(), tag: "\"FFFFFFFFFFFFFFFF\"")).Status);
        Assert.Equal(RoomTypeBatchStatus.Stale, (await Call(Guid.NewGuid(), input with { CycleETag = "\"FFFFFFFFFFFFFFFF\"" })).Status);
        Assert.Equal(RoomTypeBatchStatus.Forbidden, (await Call(Guid.NewGuid(), scope: branch + 100000)).Status);
        Assert.Equal(RoomTypeBatchStatus.InvalidReference, (await Call(Guid.NewGuid(), input with { ChecklistVersionId = long.MaxValue })).Status);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={mai.UserId} AND branch_id={branch}");
        Assert.NotEqual(RoomTypeBatchStatus.Success, (await Call(Guid.NewGuid())).Status);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=1 WHERE user_id={mai.UserId} AND branch_id={branch}");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status) VALUES({id},{mai.UserId},{mai.UserId},SYSUTCDATETIME(),'ACTIVE')");
        Assert.Equal(RoomTypeBatchStatus.InvalidReference, (await Call(Guid.NewGuid())).Status);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.cleaning_assignments WHERE cleaning_task_id={id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_checklist_versions SET status='RETIRED' WHERE version_id={input.ChecklistVersionId}");
        Assert.Equal(RoomTypeBatchStatus.InvalidReference, (await Call(Guid.NewGuid())).Status);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_checklist_versions SET status='PUBLISHED' WHERE version_id={input.ChecklistVersionId}");
        await NoMutation();
        var opFault = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs ADD CONSTRAINT CK_WriterTest_Fault CHECK(entity_type<>'INSPECTION_ASSIGNMENT')");
        try { Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => Call(opFault))).Number); }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs DROP CONSTRAINT CK_WriterTest_Fault"); }
        await NoMutation();
        Assert.Equal(0, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WHERE operation_key={opFault}").SingleAsync());

        var operation = Guid.NewGuid(); var results = await Task.WhenAll(Call(operation), Call(operation));
        Assert.All(results, r => Assert.Equal(RoomTypeBatchStatus.Success, r.Status));
        var result = Assert.IsType<InitialInspectionAssignmentResult>(results[0].Value);
        Assert.Equal(result.Id, Assert.IsType<InitialInspectionAssignmentResult>(results[1].Value).Id);
        Assert.Equal(1, await db.RoomInspections.CountAsync(i => i.CleaningTaskId == id));
        Assert.Equal(RoomTypeBatchStatus.Duplicate, (await Call(operation, input with { Reason = "Changed body" })).Status);
        Assert.Equal(RoomTypeBatchStatus.Duplicate, (await Call(Guid.NewGuid())).Status);
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityType == "INSPECTION_ASSIGNMENT" && a.EntityId == result.Id.ToString());
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        { Assert.NotNull(await InitialInspectionAssignmentSource.VerifyAsync(db, audit, default)); await tx.RollbackAsync(); }
        // The existing delivery service consumes the committed receipt once.
        var delivery = new InspectionPassedNotificationDelivery(db, TimeProvider.System);
        Assert.Equal("DELIVERED", await delivery.DeliverAsync(audit.AuditId, -5, default));
        Assert.Equal("DELIVERED", await delivery.DeliverAsync(audit.AuditId, -5, default));
        var notice = await db.Notifications.SingleAsync(n => n.NotificationType == "INSPECTION_ASSIGNED" && n.SourceId == result.Id);
        Assert.Equal(mai.UserId, (await db.NotificationRecipients.SingleAsync(r => r.NotificationId == notice.NotificationId)).UserId);
        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={mai.UserId} AND branch_id={branch}");
        Assert.NotEqual(RoomTypeBatchStatus.Success, (await Call(operation)).Status);
    }
}
