using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class PhysicalGuardProof
{
    internal static async Task<object> RunAsync(HotelOpsDbContext db, CancellationToken ct)
    {
        var operation = await db.CandidateOperations.FromSqlRaw("SELECT o.* FROM dbo.notification_operation_reservations o JOIN dbo.notification_publications p ON p.actor_user_id=o.actor_user_id AND p.operation_key=o.operation_key JOIN dbo.cleaning_assignments a ON a.assignment_id=p.assignment_id JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id WHERE o.completed=1 AND a.ended_at IS NULL AND a.assignment_status='ACTIVE' AND t.workflow_status='ASSIGNED' AND t.row_version=p.source_revision").AsNoTracking().FirstAsync(ct);
        var assignmentId = await db.CandidatePublications.Where(x => x.ActorUserId == operation.ActorUserId && x.OperationKey == operation.OperationKey).Select(x => x.AssignmentId).SingleAsync(ct);
        var taskId = await db.CleaningAssignments.Where(x => x.AssignmentId == assignmentId).Select(x => x.CleaningTaskId).SingleAsync(ct);
        var results = new List<object>();
        foreach (var name in new[] { "valid-async", "sync-rejected", "raw-missing-intent", "execute-delete-missing-audit", "execute-update-source-revision", "mutation-after-validation", "savepoint-restore", "cancel-before-commit", "raw-provider-bypass" })
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var physical = tx.GetDbTransaction();
            var guardedKey = name == "raw-provider-bypass" ? Guid.NewGuid() : operation.OperationKey;
            CandidateCommitGuard.Instance.Arm(physical, operation.ActorUserId, guardedKey, mutation: true);
            var rejected = false;
            try
            {
                if (name == "savepoint-restore") await tx.CreateSavepointAsync("proof", ct);
                if (name is "raw-missing-intent" or "savepoint-restore")
                    await db.Database.ExecuteSqlInterpolatedAsync($"DELETE i FROM dbo.notification_delivery_intents i JOIN dbo.notification_publications p ON p.publication_id=i.publication_id WHERE p.actor_user_id={operation.ActorUserId} AND p.operation_key={operation.OperationKey}", ct);
                if (name == "execute-delete-missing-audit")
                    await db.AuditLogs.Where(x => x.NewValues != null && x.NewValues.Contains(operation.OperationKey.ToString())).ExecuteDeleteAsync(ct);
                if (name == "execute-update-source-revision")
                    await db.CleaningTasks.Where(x => x.CleaningTaskId == taskId).ExecuteUpdateAsync(x => x.SetProperty(t => t.DueAt, DateTime.UtcNow.AddHours(2)), ct);
                if (name == "mutation-after-validation")
                {
                    await CandidateCommitGuard.VerifyAsync(physical, operation.ActorUserId, operation.OperationKey, ct, mutation: true);
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_assignments SET ended_at=SYSUTCDATETIME() WHERE assignment_id={assignmentId}", ct);
                }
                if (name == "savepoint-restore") await tx.RollbackToSavepointAsync("proof", ct);
                if (name == "sync-rejected") tx.Commit();
                else if (name == "cancel-before-commit") { using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); await tx.CommitAsync(cancelled.Token); }
                else if (name == "raw-provider-bypass")
                {
                    // Dedicated synthetic audit canary: persist a raw provider write, retain evidence.
                    // The shared operation/assignment evidence is untouched; no repair/reset makes this pass.
                    var canary = new HotelOps.Api.Entities.AuditLog { EntityType = "FS012_BYPASS_CANARY", EntityId = Guid.NewGuid().ToString(), Action = "CREATE", ActorUserId = operation.ActorUserId, OccurredAt = DateTime.UtcNow };
                    db.AuditLogs.Add(canary); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
                    await db.Database.ExecuteSqlRawAsync("SET XACT_ABORT OFF", ct);
                    var guardWouldReject = false;
                    try { await CandidateCommitGuard.VerifyAsync(physical, operation.ActorUserId, guardedKey, ct); }
                    catch (SqlException error) when (error.Number == 51911) { guardWouldReject = true; }
                    if (!guardWouldReject) throw new InvalidOperationException("INVALID_COMMAND_WAS_NOT_REJECTED");
                    await physical.CommitAsync(ct); // Provider API bypasses all EF interceptors.
                    await tx.DisposeAsync();
                    if (!await db.AuditLogs.AsNoTracking().AnyAsync(x => x.AuditId == canary.AuditId, ct)) throw new InvalidOperationException("BYPASS_CANARY_NOT_PERSISTED");
                }
                else await tx.CommitAsync(ct);
            }
            catch (SqlException error) when ((name is "raw-missing-intent" or "execute-delete-missing-audit" && error.Number == 51911) || (name is "execute-update-source-revision" or "mutation-after-validation" && error.Number == 51917)) { rejected = true; }
            catch (InvalidOperationException error) when (name == "sync-rejected" && error.Message == "CANDIDATE_REQUIRES_ASYNC_COMMIT") { rejected = true; }
            catch (OperationCanceledException) when (name == "cancel-before-commit") { rejected = true; }
            finally
            {
                if (physical.Connection != null) await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
            }
            if (rejected != (name is "sync-rejected" or "raw-missing-intent" or "execute-delete-missing-audit" or "execute-update-source-revision" or "mutation-after-validation" or "cancel-before-commit")) throw new InvalidOperationException("UNEXPECTED_GUARD_RESULT_" + name);
            results.Add(new { name, rejected, status = name == "raw-provider-bypass" ? "UNSUPPORTED_BYPASS_CONFIRMED" : "PASS" });
        }
        return new { level = "COMPONENT_REAL_SQL", results, universalPhysicalGuard = false };
    }
}
