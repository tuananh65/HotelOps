using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

// Real SQL permission proof, NOT proof of a deployed dedicated login. The host administrator
// impersonates a fresh NOLOGIN database user on explicitly opened test connections only.
internal static class SqlBoundaryProof
{
    internal static async Task<object> RunAsync(string connection, CancellationToken ct)
    {
        var checks = new List<object>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["CleaningAssignment:Candidate"] = "FS012_PROVISIONAL" }).Build();
        await using var admin = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        var actor = await admin.Users.Where(x => x.Username == "fs012.admin").Select(x => x.UserId).SingleAsync(ct);
        var owner = await admin.Users.Where(x => x.Username == "fs012.owner").Select(x => x.UserId).SingleAsync(ct);
        var gate = new Gate(actor);
        RoomTypeCreationService Service(HotelOpsDbContext db) => new(db, gate, gate, gate, TimeProvider.System, new(db, config, new CandidateEnvironment()));
        async Task<HotelOpsDbContext> Restricted()
        {
            // Never return an impersonated connection to the administrator's pool.
            var isolated = new SqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString;
            var db = new ImpersonatedContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(isolated)
                .AddInterceptors(CandidateCommitGuard.Instance, CandidateCommandGuard.Instance).Options);
            await db.Database.OpenConnectionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("EXECUTE AS USER='fs012_app'", ct);
            if (await db.Database.SqlQueryRaw<string>("SELECT USER_NAME() AS [Value]").SingleAsync(ct) != "fs012_app") throw new InvalidOperationException("RESTRICTED_IDENTITY_REQUIRED");
            return db;
        }
        async Task<(long Task, long Branch, string Tag)> Create(HotelOpsDbContext db)
        {
            var room = await db.Rooms.AsNoTracking().OrderBy(x => x.RoomId).FirstAsync(x => x.RoomNumber.StartsWith("FS012-B1-") && !x.CleaningTasks.Any(), ct);
            var type = await db.ReferenceValues.Where(x => x.GroupCode == "CLEANING_TYPE" && x.IsActive).Select(x => x.ReferenceId).FirstAsync(ct);
            var priority = await db.ReferenceValues.Where(x => x.GroupCode == "PRIORITY" && x.IsActive).Select(x => x.ReferenceId).FirstAsync(ct);
            var result = await Service(db).CreateCleaningTaskAsync(SessionReference.FromProtectedTicket(actor, SessionCredential.Generate(), false, DateTime.UtcNow), 1, room.BranchId, new(room.RoomId, type, priority, DateTime.UtcNow.AddHours(1)), ct);
            if (result.Status != RoomTypeBatchStatus.Success) throw new InvalidOperationException("UC028_RESTRICTED_CREATE_FAILED");
            var task = JsonSerializer.SerializeToElement(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("id").GetInt64();
            var cycle = await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS [Value] FROM dbo.room_cleaning_cycle_state WHERE room_id={room.RoomId}").SingleAsync(ct);
            var readiness = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.room_readiness_events WHERE cleaning_task_id={task}").SingleAsync(ct);
            if (cycle != task || readiness != 1) throw new InvalidOperationException("UC028_CYCLE_OR_READINESS_MISSING");
            return (task, room.BranchId, result.ETag!);
        }
        async Task<int[]> Counts(long task)
        {
            var id = task.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return [await admin.CleaningAssignments.CountAsync(x => x.CleaningTaskId == task, ct),
                await admin.AuditLogs.CountAsync(x => x.EntityType == "cleaning_tasks" && x.EntityId == id && x.Action == "UPDATE", ct),
                await admin.CandidateOperations.CountAsync(x => EF.Functions.Like(x.ResultJson!, "%\"id\":" + id + ",%"), ct),
                await admin.CandidatePublications.CountAsync(p => admin.CleaningAssignments.Any(a => a.AssignmentId == p.AssignmentId && a.CleaningTaskId == task), ct),
                await admin.CandidateIntents.CountAsync(i => admin.CandidatePublications.Any(p => p.PublicationId == i.PublicationId && admin.CleaningAssignments.Any(a => a.AssignmentId == p.AssignmentId && a.CleaningTaskId == task)), ct)];
        }
        async Task ExpectDenied(string name, Func<Task> action, int[]? expected = null)
        {
            Console.Error.WriteLine("FS012_CHECK " + name);
            try { await action(); throw new InvalidOperationException("UNAUTHORIZED_WRITE_SUCCEEDED_" + name); }
            catch (Exception e) when ((expected ?? [229, 230]).Contains(Number(e) ?? 0)) { checks.Add(new { name, outcome = "DENIED_SQL_PERMISSION", sqlNumber = Number(e) }); }
        }
        async Task<string> Module(HotelOpsDbContext db, (long Task, long Branch, string Tag) source, Guid key)
        {
            var fingerprint = new byte[32]; var revision = Convert.FromHexString(source.Tag.Trim('"'));
            var now = DateTime.UtcNow; var due = now.AddHours(1);
            return (await db.Database.SqlQuery<string>($"EXEC dbo.fs012_assign_with_evidence @actor={actor},@branch={source.Branch},@task={source.Task},@assignee={owner},@operation={key},@fingerprint={fingerprint},@expected={revision},@assignedAt={now},@dueAt={due},@reason={"FS012 SQL boundary component"}").ToArrayAsync(ct)).Single();
        }
        await using (var db = await Restricted())
        {
            Console.Error.WriteLine("FS012_CHECK authorized_service");
            var source = await Create(db); checks.Add(new { name = "UC028_restricted_EF_create_cycle_readiness_audit", outcome = "PASS" });
            var input = new AssignCleaningRequest(owner, DateTime.UtcNow.AddHours(1), "FS012 restricted authorized service");
            var key = Guid.NewGuid();
            var result = await Service(db).AssignCleaningCandidateAsync(SessionReference.FromProtectedTicket(actor, SessionCredential.Generate(), false, DateTime.UtcNow), 1, source.Branch, source.Task, source.Tag, key.ToString("D"), input, ct);
            if (result.Status != RoomTypeBatchStatus.Success || !(await Counts(source.Task)).SequenceEqual(new[] { 1, 1, 1, 1, 1 })) throw new InvalidOperationException("AUTHORIZED_BUNDLE_FAILED");
            checks.Add(new { name = "authorized_service_same_caller_transaction", outcome = "PASS_FIVE_DURABLE_RECORDS" });
            var assignment = await db.CleaningAssignments.AsNoTracking().SingleAsync(x => x.CleaningTaskId == source.Task, ct);
            db.Add(new CleaningAssignment { CleaningTaskId = source.Task, AssigneeUserId = owner, AssignedBy = actor, AssignedAt = DateTime.UtcNow, AssignmentStatus = "ACTIVE", Reason = "DENIED" });
            await ExpectDenied("EF_SaveChanges_insert", async () => await db.SaveChangesAsync(ct)); db.ChangeTracker.Clear();
            await ExpectDenied("EF_ExecuteUpdate_critical_assignee", async () => await db.CleaningAssignments.Where(x => x.AssignmentId == assignment.AssignmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.AssigneeUserId, actor), ct));
            await ExpectDenied("EF_ExecuteDelete", async () => await db.CleaningAssignments.Where(x => x.AssignmentId == assignment.AssignmentId).ExecuteDeleteAsync(ct));
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                await ExpectDenied("ExecuteSql_explicit_transaction", async () => await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_assignments SET reason=N'UNAUTHORIZED' WHERE assignment_id={assignment.AssignmentId}", ct));
                await tx.RollbackAsync(ct);
            }
            await ExpectDenied("raw_ADO_same_principal", async () => { await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = "UPDATE dbo.cleaning_assignments SET reason=N'UNAUTHORIZED' WHERE assignment_id=@id"; command.Parameters.Add(new SqlParameter("@id", assignment.AssignmentId)); await command.ExecuteNonQueryAsync(ct); });
            await ExpectDenied("audit_removal", async () => await db.AuditLogs.Where(x => x.EntityId == source.Task.ToString() && x.Action == "UPDATE").ExecuteDeleteAsync(ct));
            await ExpectDenied("publication_removal", async () => await db.CandidatePublications.Where(x => x.AssignmentId == assignment.AssignmentId).ExecuteDeleteAsync(ct));
            await ExpectDenied("intent_removal", async () => await db.CandidateIntents.ExecuteDeleteAsync(ct));
            await ExpectDenied("intent_recipient_retarget", async () => await db.CandidateIntents.ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, actor), ct));
            await ExpectDenied("alternate_existing_writer_proc", async () => await db.Database.ExecuteSqlRawAsync("EXEC dbo.fs012_unapproved_writer_probe", ct));
            await ExpectDenied("DDL_alter_unapproved_proc", async () => await db.Database.ExecuteSqlRawAsync("ALTER PROCEDURE dbo.fs012_unapproved_writer_probe AS SELECT 1", ct), [229, 15151, 3701]);
            if (!(await Counts(source.Task)).SequenceEqual(new[] { 1, 1, 1, 1, 1 })) throw new InvalidOperationException("DENIED_WRITES_CHANGED_PROOF");
            await using var external = await Restricted();
            await ExpectDenied("independent_connection_same_principal", async () => await external.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_assignments SET ended_at=SYSUTCDATETIME() WHERE assignment_id={assignment.AssignmentId}", ct));
        }
        await using (var db = await Restricted())
        {
            Console.Error.WriteLine("FS012_CHECK UC032_restricted");
            var source = await db.CleaningTasks.AsNoTracking().SingleAsync(x => x.TaskNo == "FS012_ACTIVE", ct);
            var branch = await db.Rooms.Where(x => x.RoomId == source.RoomId).Select(x => x.BranchId).SingleAsync(ct);
            var ownerGate = new Gate(owner);
            var service = new RoomTypeCreationService(db, ownerGate, ownerGate, ownerGate, TimeProvider.System);
            var ticket = SessionReference.FromProtectedTicket(owner, SessionCredential.Generate(), false, DateTime.UtcNow);
            var read = await service.WorkProgressAsync(ticket, 1, branch, source.CleaningTaskId, true, null, null, ".private/unused-no-photos", ct);
            if (read.Status != RoomTypeBatchStatus.Success || read.Value is not WorkProgressDto dto) throw new InvalidOperationException("RESTRICTED_PROGRESS_READ_FAILED");
            var auditBefore = await db.AuditLogs.CountAsync(x => x.EntityType == "cleaning_tasks" && x.EntityId == source.CleaningTaskId.ToString(), ct);
            var update = await service.WorkProgressAsync(ticket, 1, branch, source.CleaningTaskId, true, read.ETag,
                new(100, dto.Items.Select(x => new ProgressItemInput(x.Id, true, Note: "FS012 permission compatibility")).ToArray(), "FS012 restricted UC032", null), ".private/unused-no-photos", ct);
            var status = await db.CleaningTasks.Where(x => x.CleaningTaskId == source.CleaningTaskId).Select(x => x.Status).SingleAsync(ct);
            if (update.Status != RoomTypeBatchStatus.Success || status != "IN_PROGRESS" || await db.AuditLogs.CountAsync(x => x.EntityType == "cleaning_tasks" && x.EntityId == source.CleaningTaskId.ToString(), ct) != auditBefore + 1) throw new InvalidOperationException("RESTRICTED_PROGRESS_WRITE_FAILED");
            checks.Add(new { name = "UC032_restricted_EF_save_items_audit_at_100", outcome = "PASS_RETAINS_IN_PROGRESS", photoUploadVerified = false });
        }
        await using (var db = await Restricted())
        {
            Console.Error.WriteLine("FS012_CHECK wrong_transaction_and_rollback");
            var source = await Create(db);
            try { await Module(db, source, Guid.NewGuid()); throw new InvalidOperationException("AUTOCOMMIT_MODULE_ACCEPTED"); }
            catch (SqlException e) when (e.Number == 51941) { checks.Add(new { name = "module_without_caller_transaction", outcome = "DENIED_51941" }); }
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                Console.Error.WriteLine("FS012_CHECK nested_transaction");
                await db.Database.ExecuteSqlRawAsync("BEGIN TRANSACTION", ct);
                try { await Module(db, source, Guid.NewGuid()); throw new InvalidOperationException("NESTED_MODULE_ACCEPTED"); }
                catch (SqlException e) when (e.Number == 51941) { checks.Add(new { name = "nested_wrong_transaction", outcome = "DENIED_51941" }); }
                await tx.RollbackAsync(ct);
            }
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                Console.Error.WriteLine("FS012_CHECK rollback_bundle");
                await Module(db, source, Guid.NewGuid()); await tx.RollbackAsync(ct);
            }
            if ((await Counts(source.Task)).Any(x => x != 0)) throw new InvalidOperationException("ROLLBACK_LEAKED_BUNDLE");
            checks.Add(new { name = "caller_rollback_complete_bundle", outcome = "PASS_ZERO_DURABLE_RECORDS" });
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                Console.Error.WriteLine("FS012_CHECK raw_commit_bundle");
                await Module(db, source, Guid.NewGuid());
                await tx.GetDbTransaction().CommitAsync(ct); // Deliberately bypass EF: SQL module already supplied the whole bundle.
            }
            if (!(await Counts(source.Task)).SequenceEqual(new[] { 1, 1, 1, 1, 1 })) throw new InvalidOperationException("RAW_COMMIT_BUNDLE_MISSING");
            checks.Add(new { name = "raw_provider_commit_after_approved_module", outcome = "PASS_FIVE_DURABLE_RECORDS_WITHOUT_EF_COMMIT_HOOK" });
            await using var wrong = await Restricted(); await using var otherTx = await wrong.Database.BeginTransactionAsync(ct);
            try { await new ProvisionalCleaningNotification(db, config, new CandidateEnvironment()).VerifyBeforeCommitAsync(otherTx.GetDbTransaction(), actor, Guid.NewGuid(), ct); throw new InvalidOperationException("WRONG_PHYSICAL_ACCEPTED"); }
            catch (InvalidOperationException e) when (e.Message == "WRONG_PHYSICAL_TRANSACTION") { checks.Add(new { name = "different_physical_transaction", outcome = "DENIED_BEFORE_PROOF_QUERY" }); }
            await otherTx.RollbackAsync(ct);
        }
        foreach (var table in new[] { "audit_logs", "notification_publications" })
        {
            Console.Error.WriteLine("FS012_CHECK missing_" + table);
            await using var db = await Restricted(); var source = await Create(db);
            // Controlled developer trial only; never run these faults on the Tester target.
            var installFault = table == "audit_logs"
                ? "ALTER TABLE dbo.audit_logs WITH NOCHECK ADD CONSTRAINT CK_FS012_BoundaryFault CHECK(1=0)"
                : "ALTER TABLE dbo.notification_publications WITH NOCHECK ADD CONSTRAINT CK_FS012_BoundaryFault CHECK(1=0)";
            var removeFault = table == "audit_logs"
                ? "ALTER TABLE dbo.audit_logs DROP CONSTRAINT CK_FS012_BoundaryFault"
                : "ALTER TABLE dbo.notification_publications DROP CONSTRAINT CK_FS012_BoundaryFault";
            await admin.Database.ExecuteSqlRawAsync(installFault, ct);
            var state = 1; var commitRejected = false;
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                try { await Module(db, source, Guid.NewGuid()); throw new InvalidOperationException("FAULT_NOT_RAISED"); }
                catch (SqlException e) when (e.Number == 547) { }
                state = await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int,XACT_STATE()) AS [Value]").SingleAsync(ct);
                if (state is not (-1 or 0)) throw new InvalidOperationException("PARTIAL_MODULE_TRANSACTION_STILL_COMMITTABLE");
                try { await tx.GetDbTransaction().CommitAsync(ct); }
                catch (SqlException e) when (e.Number is 3930 or 3902) { commitRejected = true; }
                catch (InvalidOperationException) when (state == 0) { commitRejected = true; }
                if (!commitRejected) throw new InvalidOperationException("PARTIAL_BUNDLE_COMMITTED");
                if (state == -1) await tx.RollbackAsync(ct);
            }
            finally { await admin.Database.ExecuteSqlRawAsync(removeFault, ct); }
            if ((await Counts(source.Task)).Any(x => x != 0)) throw new InvalidOperationException("MISSING_PROOF_LEFT_ASSIGNMENT");
            checks.Add(new { name = "missing_" + table, outcome = "PASS_SQL_547_COMMIT_REJECTED_ZERO_RECORDS", transactionState = state, commitRejected });
        }
        await using (var db = await Restricted())
        {
            Console.Error.WriteLine("FS012_CHECK concurrent_and_privileged");
            var source = await Create(db);
            async Task<string> Writer()
            {
                await using var writer = await Restricted(); await using var tx = await writer.Database.BeginTransactionAsync(ct);
                try { await Module(writer, source, Guid.NewGuid()); await tx.CommitAsync(ct); return "COMMITTED"; }
                catch (SqlException e) when (e.Number == 51944) { await tx.RollbackAsync(ct); return "STALE_REJECTED"; }
            }
            var outcomes = await Task.WhenAll(Writer(), Writer());
            if (outcomes.Count(x => x == "COMMITTED") != 1 || !(await Counts(source.Task)).SequenceEqual(new[] { 1, 1, 1, 1, 1 })) throw new InvalidOperationException("CONCURRENT_WRITER_DUPLICATE");
            checks.Add(new { name = "two_independent_concurrent_approved_writers", outcome = "PASS_ONE_COMMIT_ONE_STALE", outcomes });
            // Explicit residual: dbo/sysadmin can still mutate Assignment outside this permission boundary.
            var before = await Counts(source.Task); var marker = "FS012_PRIVILEGED_BYPASS_" + Guid.NewGuid().ToString("N");
            await admin.CleaningAssignments.Where(x => x.CleaningTaskId == source.Task).ExecuteUpdateAsync(s => s.SetProperty(x => x.Reason, marker), ct);
            if (await admin.CleaningAssignments.CountAsync(x => x.CleaningTaskId == source.Task && x.Reason == marker, ct) != 1 || !(await Counts(source.Task)).SequenceEqual(before)) throw new InvalidOperationException("PRIVILEGED_RESIDUAL_NOT_REPRODUCED");
            checks.Add(new { name = "privileged_external_reason_write", outcome = "EXPECTED_UNPROTECTED_PERSISTED_WITHOUT_ADDITIONAL_PROOF", task = source.Task, marker });
        }
        return new { level = "REAL_SQL_DATABASE_USER_IMPERSONATION", deploymentLoginVerified = false, hostIsPrivileged = true, countOrder = new[] { "Assignment", "AssignmentAudit", "Operation", "Publication", "Intent" }, checks, acceptedA01 = false };
    }
    private static int? Number(Exception e) => e is SqlException sql ? sql.Number : e.InnerException == null ? null : Number(e.InnerException);
    private sealed class ImpersonatedContext(DbContextOptions<HotelOpsDbContext> options) : HotelOpsDbContext(options)
    {
        public override async ValueTask DisposeAsync()
        {
            try
            {
                if (Database.GetDbConnection().State == ConnectionState.Open)
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await Database.ExecuteSqlRawAsync("REVERT", cleanup.Token);
                }
            }
            finally { await base.DisposeAsync(); }
        }
    }
    private sealed class Gate(long actor) : ICurrentUserAccessor, IPermissionService, ISqlSessionService
    {
        public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(actor, 1, false); return true; }
        public Task<PermissionEvaluation> EvaluateGlobalAsync(string code, CancellationToken ct) => Task.FromResult(new PermissionEvaluation(PermissionDecision.Granted));
        public Task<PermissionEvaluation> EvaluateBranchAsync(string code, long branch, CancellationToken ct) => EvaluateGlobalAsync(code, ct);
        public Task<PermissionEvaluation> EvaluateResourceAsync(string code, PermissionResource resource, CancellationToken ct) => EvaluateGlobalAsync(code, ct);
        public Task<SessionValidationResult> ValidateSessionAsync(SessionReference ticket, CancellationToken ct = default) => Task.FromResult(new SessionValidationResult(true, new(1, actor, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(1), false)));
        public Task<CreatedSqlSession> CreateSessionAsync(CredentialLockoutResult verified, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SessionValidationResult> TouchSessionAsync(SessionReference ticket, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> RevokeSessionAsync(SessionReference ticket, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> RevokeAllSessionsAsync(long user, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
