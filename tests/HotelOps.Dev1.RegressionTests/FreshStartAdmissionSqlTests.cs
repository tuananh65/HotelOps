using Microsoft.Extensions.Configuration;
using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class DevelopmentRecoveryFactAttribute : FactAttribute
{
    public DevelopmentRecoveryFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEV3_RECOVERY_RECEIPT")))
            Skip = "Requires an explicitly selected, identity-verified development recovery clone.";
    }
}

[Collection("HotelOps isolated SQL")]
public sealed class FreshStartAdmissionSqlTests
{
    [DevelopmentRecoveryFact]
    public async Task RequiresTransactionLocksAndAuthorityAndUsesTemporalBlockRangeWithRollback()
    {
        using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(Environment.GetEnvironmentVariable("DEV3_RECOVERY_RECEIPT")!));
        var root = receipt.RootElement;
        Assert.Equal("RECOVERY_VERIFIED", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("developmentOwnershipConfirmed").GetBoolean());
        var target = root.GetProperty("recoveryDatabase").GetString()!;
        Assert.Matches("^HotelOps_DEV3_142A_Recovery_[a-f0-9]{8}$", target);
        var guid = Guid.Parse(root.GetProperty("recoveryGuid").GetString()!);
        Assert.NotEqual(Guid.Empty, guid);
        var server = Environment.GetEnvironmentVariable("DEV3_RECOVERY_SERVER") ?? "localhost\\MSSQLSERVER01";
        if (server != "localhost\\MSSQLSERVER01")
            Assert.Matches("^tcp:127[.]0[.]0[.]1,[0-9]{1,5}$", server);
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer($"Server={server};Database={target};Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        Assert.Equal(root.GetProperty("expectedServer").GetString(), await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS [Value]").SingleAsync());
        Assert.Equal(target, await db.Database.SqlQuery<string>($"SELECT DB_NAME() AS [Value]").SingleAsync());
        Assert.Equal(guid, await db.Database.SqlQuery<Guid>($"SELECT database_guid AS [Value] FROM sys.database_recovery_status WHERE database_id=DB_ID()").SingleAsync());
        Assert.True(await CleaningStartSchema.InstalledAsync(db, default));
        Assert.True(await CleaningResumeSchema.InstalledAsync(db, default));
        var task = await db.CleaningTasks.AsNoTracking().Where(t => t.Room.IsActive && !db.RoomBlocks.Any(b => b.RoomId == t.RoomId))
            .Where(t => !db.CleaningAssignments.Any(a => a.CleaningTaskId == t.CleaningTaskId && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null))
            .Select(t => new { t.CleaningTaskId, t.RoomId, t.Room.BranchId, Actor = t.CreatedBy }).FirstAsync();
        var ticket = SessionReference.FromProtectedTicket(task.Actor, new string('A', 43), false, DateTime.UtcNow);
        var admission = new SqlFreshStartCleaningAdmission(db, ticket, 1, _ => Task.FromResult(false));
        var noTransaction = await Assert.ThrowsAsync<RoomStatusTransitionException>(() => admission.AcquireAsync(task.BranchId, task.CleaningTaskId, default));
        Assert.Equal("ROOM_STATUS_CALLER_TRANSACTION_REQUIRED", noTransaction.Code);
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var missingLock = await Assert.ThrowsAsync<RoomStatusTransitionException>(() => admission.AcquireAsync(task.BranchId, task.CleaningTaskId, default));
            Assert.Equal("ROOM_STATUS_GLOBAL_LOCK_ORDER_REQUIRED", missingLock.Code);
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([task.Actor], default);
            var resource = "HotelOps:WorkTime:User:" + task.Actor;
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=1000; IF @r<0 THROW 51990,'Test time lock failed',1;");
            Assert.True(await admission.AcquireAsync(task.BranchId, task.CleaningTaskId, default));
            var connection = db.Database.GetDbConnection(); var transaction = tx.GetDbTransaction();
            await admission.VerifyHeldAsync(db, connection, transaction, task.Actor, task.RoomId, task.CleaningTaskId, default);
            var denied = await Assert.ThrowsAsync<RoomStatusTransitionException>(() => admission.VerifyAsync(db, connection, transaction,
                ticket, 1, task.Actor, task.BranchId, task.RoomId, task.CleaningTaskId, 1, default));
            Assert.Equal("ROOM_STATUS_FINAL_AUTHORITY_REQUIRED", denied.Code);
            // Component proof only: injected true does not certify real session/role authorization.
            // A synthetic current-cycle pointer is rolled back with the rest of this fixture.
            Assert.Equal(0, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.room_cleaning_cycle_state WHERE room_id={task.RoomId}").SingleAsync());
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES({task.RoomId},{task.CleaningTaskId})");
            // Only exercise the admission port's ownership proof; task lifecycle validation belongs to StartCleaningAsync.
            var syntheticAssignment = new CleaningAssignment { CleaningTaskId=task.CleaningTaskId,
                AssigneeUserId=task.Actor, AssignedBy=task.Actor, AssignedAt=DateTime.UtcNow.AddMinutes(-1),
                AssignmentStatus="ACTIVE", Reason="DEV3 rollback admission fixture" };
            db.CleaningAssignments.Add(syntheticAssignment);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var admitted = new SqlFreshStartCleaningAdmission(db, ticket, 1, _ => Task.FromResult(true));
            Assert.True(await admitted.AcquireAsync(task.BranchId, task.CleaningTaskId, default));
            await admitted.VerifyAsync(db, connection, transaction, ticket, 1, task.Actor, task.BranchId,
                task.RoomId, task.CleaningTaskId, syntheticAssignment.AssignmentId, default);
            var stale = await Assert.ThrowsAsync<RoomStatusTransitionException>(() => admitted.VerifyAsync(db, connection, transaction,
                ticket, 1, task.Actor, task.BranchId, task.RoomId, task.CleaningTaskId, long.MaxValue, default));
            Assert.Equal("ROOM_STATUS_ASSIGNMENT_INVALID", stale.Code);
            var now = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(), DateTimeKind.Utc);
            Assert.Equal(RoomStartBlockEvidence.Clear, await admission.VerifyAsync(db, connection, transaction, task.BranchId, task.RoomId, now, default));
            // Synthetic rows only, rolled back; existing room/task/user data is read-only.
            db.RoomBlocks.AddRange(
                new RoomBlock { RoomId=task.RoomId, BlockedBy=task.Actor, BlockedAt=now.AddMinutes(1), Reason="DEV3 future block" },
                new RoomBlock { RoomId=task.RoomId, BlockedBy=task.Actor, BlockedAt=now.AddMinutes(-1), ReleasedAt=now, ReleasedBy=task.Actor, Reason="DEV3 released boundary" });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.Equal(RoomStartBlockEvidence.Clear, await admission.VerifyAsync(db, connection, transaction, task.BranchId, task.RoomId, now, default));
            db.RoomBlocks.Add(new RoomBlock { RoomId=task.RoomId, BlockedBy=task.Actor, BlockedAt=now, ReleasedAt=now.AddMinutes(1), ReleasedBy=task.Actor, Reason="DEV3 effective block" });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.Equal(RoomStartBlockEvidence.Effective, await admission.VerifyAsync(db, connection, transaction, task.BranchId, task.RoomId, now, default));
            await tx.RollbackAsync();
        }
        Assert.False(await db.RoomBlocks.AnyAsync(b => b.RoomId == task.RoomId));
        await VerifyCanonicalStart(db, task.Actor, true);
        await VerifyCanonicalStart(db, task.Actor, false);
        await RejectUnprovenClosure(db, task.Actor);
        Assert.Equal(0, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.room_cleaning_cycle_state WHERE room_id={task.RoomId}").SingleAsync());
    }
    private static async Task VerifyCanonicalStart(HotelOpsDbContext db, long actor, bool blocked)
    {
        var floor = await db.Floors.AsNoTracking().Where(f => f.IsActive && f.Branch.IsActive).FirstAsync();
        var checklist = await db.Database.SqlQuery<long>($"SELECT TOP(1) checklist_id AS [Value] FROM dbo.inspection_checklists WHERE branch_id IS NULL").SingleAsync();
        var cleaningType = await db.ReferenceValues.AsNoTracking().Where(r => r.GroupCode == "CLEANING_TYPE" && r.IsActive).Select(r => r.ReferenceId).FirstAsync();
        var code = "DEV3-" + Guid.NewGuid().ToString("N")[..12];
        long roomId, taskId;
        Guid operation;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([actor], default);
            var resource = "HotelOps:WorkTime:User:" + actor;
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=1000; IF @r<0 THROW 51990,'Test time lock failed',1;");
            // All mutated business rows are synthetic. Existing actor/floor/legacy checklist are FK references only.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_types(type_code,type_name,standard_capacity,is_active,branch_id,standard_cleaning_minutes,default_checklist_id) VALUES({code},{code},2,1,{floor.BranchId},30,{checklist})");
            var type = await db.BranchRoomTypes.AsNoTracking().Where(t => t.TypeCode == code).Select(t => t.RoomTypeId).SingleAsync();
            var room = new Room { BranchId=floor.BranchId, FloorId=floor.FloorId, RoomTypeId=type,
                RoomNumber=code, OperationalStatus="DIRTY", IsActive=true };
            db.Rooms.Add(room); await db.SaveChangesAsync(); roomId=room.RoomId; db.ChangeTracker.Clear();
            var task = new CleaningTask { RoomId=roomId, TaskNo=code, CleaningTypeId=cleaningType,
                Status="PENDING", CreatedBy=actor, CreatedAt=DateTime.UtcNow };
            db.CleaningTasks.Add(task); await db.SaveChangesAsync(); taskId=task.CleaningTaskId; db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='ASSIGNED' WHERE cleaning_task_id={taskId}; INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES({roomId},{taskId});");
            var assignment = new CleaningAssignment { CleaningTaskId=taskId, AssigneeUserId=actor,
                AssignedBy=actor, AssignedAt=DateTime.UtcNow.AddMinutes(-1), AssignmentStatus="ACTIVE" };
            db.CleaningAssignments.Add(assignment); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            if (blocked)
            {
                db.RoomBlocks.Add(new RoomBlock { RoomId=roomId, BlockedBy=actor, BlockedAt=DateTime.UtcNow.AddMinutes(-1), Reason="DEV3 synthetic effective block" });
                await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            }
            var ticket=SessionReference.FromProtectedTicket(actor,new string('A',43),false,DateTime.UtcNow);
            var admission=new SqlFreshStartCleaningAdmission(db,ticket,1,_=>Task.FromResult(true));
            Assert.True(await admission.AcquireAsync(floor.BranchId,taskId,default));
            var evidence=new FreshStartCleaningTransactionEvidence(ticket,1,admission,admission,admission);
            Assert.Equal(blocked ? RoomStartBlockEvidence.Effective : RoomStartBlockEvidence.Clear,
                await evidence.RequireAdmissionAsync(db,actor,floor.BranchId,roomId,taskId,assignment.AssignmentId,default));
            var before=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS [Value] FROM dbo.cleaning_tasks WHERE cleaning_task_id={taskId}").SingleAsync();
            var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(),DateTimeKind.Utc);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='IN_PROGRESS',status='IN_PROGRESS',actual_start_at={now} WHERE cleaning_task_id={taskId}");
            var after=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS [Value] FROM dbo.cleaning_tasks WHERE cleaning_task_id={taskId}").SingleAsync();
            var writer=await db.Database.SqlQuery<long>($"SELECT CURRENT_TRANSACTION_ID() AS [Value]").SingleAsync();
            operation=Guid.NewGuid();
            var beforeTag='"'+Convert.ToHexString(before)+'"';
            evidence.BindSource(new(actor,floor.BranchId,roomId,taskId,assignment.AssignmentId,taskId,beforeTag,
                '"'+Convert.ToHexString(after)+'"',operation,writer,now,db.Database.GetDbConnection(),tx.GetDbTransaction()));
            var result=await new RoomOperationalStatusTransitionParticipant(db,evidence).ApplyFreshStartCleaningAsync(
                db.Database.GetDbConnection(),tx.GetDbTransaction(),new("START_CLEANING",actor,floor.BranchId,roomId,taskId,assignment.AssignmentId,beforeTag,"DEV3 rollback fixture"),default);
            Assert.Equal(blocked,result.HasEffectiveBlock);
            Assert.Equal(blocked ? "DIRTY" : "CLEANING",await db.Rooms.Where(r=>r.RoomId==roomId).Select(r=>r.OperationalStatus).SingleAsync());
            Assert.Equal(blocked ? 0 : 1,await db.RoomStatusHistories.CountAsync(h=>h.RoomId==roomId));
            var audit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.RequestId==operation.ToString("D"));
            using var data=JsonDocument.Parse(audit.NewValues!);
            Assert.Equal(blocked ? "BLOCKED" : "CLEANING_IN_PROGRESS",data.RootElement.GetProperty("effectiveReadiness").GetString());
            // Resume persistence contract: blocker resolution must change the parent task ETag.
            var blockerReason=await db.ReferenceValues.AsNoTracking().Where(r=>r.GroupCode=="BLOCKER" && r.IsActive)
                .Select(r=>r.ReferenceId).FirstAsync();
            var progress=new CleaningProgressLog { CleaningTaskId=taskId, Status="BLOCKED", ProgressPercent=50,
                BlockerTypeId=blockerReason, Note="DEV3 synthetic resume blocker", LoggedBy=actor, LoggedAt=now };
            db.CleaningProgressLogs.Add(progress); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.fs014_cleaning_blocker_resolutions(progress_id) VALUES({progress.ProgressId})");
            var blockedVersion=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS [Value] FROM dbo.cleaning_tasks WHERE cleaning_task_id={taskId}").SingleAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.fs014_cleaning_blocker_resolutions SET resolved_by={actor},resolved_at={now},resolution_note=N'Synthetic obstacle removed' WHERE progress_id={progress.ProgressId}");
            var resolvedVersion=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS [Value] FROM dbo.cleaning_tasks WHERE cleaning_task_id={taskId}").SingleAsync();
            Assert.False(blockedVersion.SequenceEqual(resolvedVersion));
            Assert.Equal(1,await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.fs014_cleaning_blocker_resolutions WHERE progress_id={progress.ProgressId} AND resolved_by={actor} AND resolved_at={now} AND resolution_note=N'Synthetic obstacle removed'").SingleAsync());
            // Real durable outbox staging, with synthetic source/interval/audit in this same transaction.
            var closureState = blocked ? "ON_HOLD" : "PENDING_INSPECTION";
            if (!blocked)
            {
                progress = new CleaningProgressLog { CleaningTaskId=taskId, Status="COMPLETED", ProgressPercent=100,
                    Note="Synthetic completed cleaning", LoggedBy=actor, LoggedAt=now };
                db.CleaningProgressLogs.Add(progress); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='PENDING_INSPECTION',actual_end_at={now} WHERE cleaning_task_id={taskId}");
                Assert.True(await HotelOps.Api.Services.HotelStructure.RoomTypeCreationService.CleanerCaptureWriterAvailableAsync(db, default));
                await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.fe04_capture_cleaner_attribution @task={taskId},@assignment={assignment.AssignmentId},@cleaner={actor},@progress={progress.ProgressId}");
                Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={taskId} AND cleaner_user_id={actor} AND completion_progress_id={progress.ProgressId} AND completed_at={now} AND writer_transaction_id=CURRENT_TRANSACTION_ID()").SingleAsync());
            }
            else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='ON_HOLD' WHERE cleaning_task_id={taskId}");
            var department=await db.Departments.AsNoTracking().Where(d=>d.BranchId==floor.BranchId && d.IsActive).Select(d=>d.DepartmentId).FirstAsync();
            var day=new Workday { UserId=actor,BranchId=floor.BranchId,DepartmentId=department,
                WorkDate=DateOnly.FromDateTime(now),IsUnscheduled=true,UnscheduledReason="Synthetic rollback fixture",Status="WORKING",StartedAt=now.AddMinutes(-1) };
            db.Workdays.Add(day);await db.SaveChangesAsync();db.ChangeTracker.Clear();
            var interval=new WorkInterval { WorkdayId=day.WorkdayId,UserId=actor,BranchId=floor.BranchId,
                WorkItemKind="CLEANING",WorkItemId=taskId,OpenedBy=actor,OpenedAt=now,EffectiveStartedAt=now,
                ClosedBy=actor,ClosedAt=now,EffectiveEndedAt=now };
            db.WorkIntervals.Add(interval);await db.SaveChangesAsync();db.ChangeTracker.Clear();
            var closureOperation=Guid.NewGuid();
            var revision=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS [Value] FROM dbo.cleaning_tasks WHERE cleaning_task_id={taskId}").SingleAsync();
            var closure=new HotelOps.Api.DTOs.Housekeeping.CleaningClosureResult(taskId,floor.BranchId,assignment.AssignmentId,
                closureOperation,Guid.NewGuid(),progress.ProgressId,interval.IntervalId,closureState,now,blocked ? "PENDING_CLEANING" : "PENDING_INSPECTION",blocked,'"'+Convert.ToHexString(revision)+'"');
            db.AuditLogs.Add(new AuditLog { BranchId=floor.BranchId,ActorUserId=actor,EntityType="cleaning_tasks",
                EntityId=taskId.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
                NewValues=JsonSerializer.Serialize(new { operationId=closureOperation,action=blocked ? "REPORT_CLEANING_BLOCKER" : "COMPLETE_CLEANING",progressId=progress.ProgressId }) });
            await db.SaveChangesAsync();db.ChangeTracker.Clear();
            var config=new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string,string?> { ["CleaningClosure:Capability"]="VERIFIED_V1" }).Build();
            var publisher=new HotelOps.Api.Services.Notifications.ProvisionalCleaningNotification(db,config,new ClosureEnvironment());
            Assert.True(await publisher.ClosureAvailableAsync(default));
            await publisher.StageClosureAsync(actor,new byte[32],roomId,[actor],closure,default);
            await publisher.VerifyClosureAsync(actor,closureOperation,1,default);
            Assert.Equal(1,await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_delivery_intents WHERE publication_id={closure.PublicationId} AND status='PENDING' AND user_id={actor}").SingleAsync());
            await tx.RollbackAsync();
        }
        Assert.False(await db.Rooms.AnyAsync(r=>r.RoomId==roomId));
        Assert.False(await db.CleaningTasks.AnyAsync(t=>t.CleaningTaskId==taskId));
        Assert.Equal(0, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={taskId}").SingleAsync());
        Assert.False(await db.AuditLogs.AnyAsync(a=>a.RequestId==operation.ToString("D")));
    }
    private static async Task RejectUnprovenClosure(HotelOpsDbContext db, long actor)
    {
        var assignment=await db.CleaningAssignments.AsNoTracking().Select(a=>a.AssignmentId).FirstAsync();
        var operation=Guid.NewGuid();var publication=Guid.NewGuid();var fingerprint=new byte[32];
        await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var error=await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"""
                SET XACT_ABORT ON;
                INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint,completed,result_json)
                VALUES({actor},{operation},1,{fingerprint},1,N'[]');
                INSERT dbo.notification_publications(publication_id,actor_user_id,operation_key,event_type,schema_version,assignment_id,source_revision,payload_json,occurrence_key)
                SELECT {publication},{actor},{operation},'CLEANING_BLOCKER_REPORTED',1,a.assignment_id,t.row_version,N'[]',{publication.ToString("D")}
                FROM dbo.cleaning_assignments a JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id WHERE a.assignment_id={assignment};
                """));
            Assert.Equal(51915,error.Number);
        }
        Assert.Equal(0,await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_operation_reservations WHERE actor_user_id={actor} AND operation_key={operation}").SingleAsync());
    }
    private sealed class ClosureEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; }="Development";
        public string ApplicationName { get; set; }="HotelOps.Api";
        public string ContentRootPath { get; set; }=".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }=new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string WebRootPath { get; set; }=".";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; }=new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
