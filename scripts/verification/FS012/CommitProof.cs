using System.Data.Common;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

// Real SQL component tests; gate doubles isolate commit behavior. Not authentication/API evidence.
internal static class CommitProof
{
    internal static async Task<object> RunAsync(string connection, CancellationToken ct)
    {
        var results = new List<object>();
        foreach (var fault in new[] { "before-commit", "after-commit", "cancel-before", "cancel-after" })
        {
            using var cancellation = new CancellationTokenSource();
            var injection = new CommitFault(fault, cancellation);
            var options = new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection)
                .AddInterceptors(CandidateCommitGuard.Instance, CandidateCommandGuard.Instance, injection).Options;
            await using var db = new HotelOpsDbContext(options);
            var actor = await db.Users.Where(x => x.Username == "fs012.admin").Select(x => x.UserId).SingleAsync(ct);
            var assignee = await db.Users.Where(x => x.Username == "fs012.owner").Select(x => x.UserId).SingleAsync(ct);
            var room = await db.Rooms.AsNoTracking().FirstAsync(x => x.RoomNumber.StartsWith("FS012-B1-") && !x.CleaningTasks.Any(), ct);
            var type = await db.ReferenceValues.Where(x => x.GroupCode == "CLEANING_TYPE" && x.IsActive).Select(x => x.ReferenceId).FirstAsync(ct);
            var priority = await db.ReferenceValues.Where(x => x.GroupCode == "PRIORITY" && x.IsActive).Select(x => x.ReferenceId).FirstAsync(ct);
            var ticket = SessionReference.FromProtectedTicket(actor, SessionCredential.Generate(), false, DateTime.UtcNow);
            var gate = new ComponentGate(actor);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["CleaningAssignment:Candidate"] = "FS012_PROVISIONAL" }).Build();
            var publisher = new ProvisionalCleaningNotification(db, config, new CandidateEnvironment());
            var service = new RoomTypeCreationService(db, gate, gate, gate, TimeProvider.System, publisher);
            var created = await service.CreateCleaningTaskAsync(ticket, 1, room.BranchId, new(room.RoomId, type, priority, DateTime.UtcNow.AddHours(1)), ct);
            if (created.Status != RoomTypeBatchStatus.Success) throw new InvalidOperationException("COMPONENT_CREATE_FAILED");
            var task = JsonSerializer.SerializeToElement(created.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("id").GetInt64();
            var command = new AssignCleaningRequest(assignee, DateTime.UtcNow.AddHours(1), "FS012 commit fault " + fault);
            var key = Guid.NewGuid();
            var before = await CountsAsync(db, task, ct);
            injection.Enabled = true;
            var result = await service.AssignCleaningCandidateAsync(ticket, 1, room.BranchId, task, created.ETag!, key.ToString("D"), command, cancellation.Token);
            var committed = fault is "after-commit" or "cancel-after";
            if (result.Status != (committed ? RoomTypeBatchStatus.Success : RoomTypeBatchStatus.Unavailable)) throw new InvalidOperationException("WRONG_RECOVERY_OUTCOME_" + fault);
            // Fresh context models restart; never consult the service's memory to decide commit outcome.
            await using var fresh = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).AddInterceptors(CandidateCommitGuard.Instance, CandidateCommandGuard.Instance).Options);
            var after = await CountsAsync(fresh, task, ct);
            var delta = after.Zip(before, (a, b) => a - b).ToArray();
            if (!delta.SequenceEqual(committed ? new[] { 1, 1, 1, 1, 1 } : new[] { 0, 0, 0, 0, 0 })) throw new InvalidOperationException("WRONG_DURABLE_COUNTS_" + fault);
            var retryService = new RoomTypeCreationService(fresh, gate, gate, gate, TimeProvider.System, new(fresh, config, new CandidateEnvironment()));
            var retry = await retryService.AssignCleaningCandidateAsync(ticket, 1, room.BranchId, task, created.ETag!, key.ToString("D"), command, ct);
            if (retry.Status != RoomTypeBatchStatus.Success) throw new InvalidOperationException("RESTART_REPLAY_FAILED");
            var final = await CountsAsync(fresh, task, ct);
            if (!final.Zip(before, (a, b) => a - b).SequenceEqual(new[] { 1, 1, 1, 1, 1 })) throw new InvalidOperationException("RETRY_DUPLICATED_RECORDS");
            results.Add(new { fault, result = result.Status.ToString(), delta, restartRetry = "SUCCESS_EXACTLY_ONCE", gate = "COMPONENT_DOUBLE_NOT_AUTH_EVIDENCE" });
        }
        return new { level = "COMPONENT_REAL_SQL", results, countOrder = new[] { "Assignment", "Audit", "Operation", "Publication", "Intent" }, actualNetworkDisconnect = false };
    }

    private static async Task<int[]> CountsAsync(HotelOpsDbContext db, long task, CancellationToken ct)
    {
        var id = task.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return [await db.CleaningAssignments.CountAsync(x => x.CleaningTaskId == task, ct),
            await db.AuditLogs.CountAsync(x => x.EntityType == "cleaning_tasks" && x.EntityId == id, ct),
            await db.CandidateOperations.CountAsync(x => x.Completed && EF.Functions.Like(x.ResultJson!, "%\"id\":" + id + ",%"), ct),
            await db.CandidatePublications.CountAsync(x => db.CleaningAssignments.Any(a => a.AssignmentId == x.AssignmentId && a.CleaningTaskId == task), ct),
            await db.CandidateIntents.CountAsync(x => db.CandidatePublications.Any(p => p.PublicationId == x.PublicationId && db.CleaningAssignments.Any(a => a.AssignmentId == p.AssignmentId && a.CleaningTaskId == task)), ct)];
    }

    private sealed class InjectedCommitException : DbException { }
    private sealed class CommitFault(string mode, CancellationTokenSource cancellation) : DbTransactionInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken ct = default)
        {
            if (Enabled && mode is "before-commit" or "cancel-before") { Enabled = false; if (mode == "cancel-before") { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); } throw new InjectedCommitException(); }
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken ct = default)
        {
            if (Enabled) { Enabled = false; if (mode == "cancel-after") { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); } throw new InjectedCommitException(); }
            return Task.CompletedTask;
        }
    }

    private sealed class ComponentGate(long actor) : ICurrentUserAccessor, IPermissionService, ISqlSessionService
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
