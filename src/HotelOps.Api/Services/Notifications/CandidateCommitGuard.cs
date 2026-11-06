using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HotelOps.Api.Services.Notifications;

// UC029 only. This intercepts EF commit, not direct provider Commit or external SQL.
// The weak table identifies the command; it never serves as proof of persistence.
public sealed class CandidateCommitGuard : DbTransactionInterceptor
{
    public static CandidateCommitGuard Instance { get; } = new();
    private readonly ConditionalWeakTable<DbTransaction, CommandIdentity> commands = new();
    private sealed record CommandIdentity(long Actor, Guid Operation, bool Mutation);

    internal bool IsArmed(DbTransaction? transaction) => transaction != null && commands.TryGetValue(transaction, out _);

    public void Arm(DbTransaction transaction, long actor, Guid operation, bool mutation = false)
    {
        if (actor <= 0 || operation == Guid.Empty || transaction.Connection == null)
            throw new InvalidOperationException("INVALID_GUARDED_TRANSACTION");
        commands.Add(transaction, new(actor, operation, mutation));
    }

    public override InterceptionResult TransactionCommitting(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result)
    {
        if (commands.TryGetValue(transaction, out _))
            throw new InvalidOperationException("CANDIDATE_REQUIRES_ASYNC_COMMIT");
        return result;
    }

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (commands.TryGetValue(transaction, out var identity))
            await VerifyAsync(transaction, identity.Actor, identity.Operation, cancellationToken, identity.Mutation);
        return result;
    }

    public static async Task VerifyAsync(DbTransaction transaction, long actor, Guid operation, CancellationToken ct, bool mutation = false)
    {
        ct.ThrowIfCancellationRequested();
        var connection = transaction.Connection ?? throw new InvalidOperationException("TRANSACTION_DISPOSED");
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "EXEC dbo.fs009_verify_assignment_guard @actor,@operation";
        if (mutation)
            command.CommandText += "; IF NOT EXISTS (SELECT 1 FROM dbo.notification_publications p JOIN dbo.cleaning_assignments a ON a.assignment_id=p.assignment_id JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id JOIN dbo.room_cleaning_cycle_state c ON c.cleaning_task_id=t.cleaning_task_id AND c.room_id=t.room_id WHERE p.actor_user_id=@actor AND p.operation_key=@operation AND a.assignment_status='ACTIVE' AND a.ended_at IS NULL AND t.workflow_status='ASSIGNED' AND t.status='PENDING' AND t.row_version=p.source_revision AND t.due_at=TRY_CONVERT(datetime2(7),JSON_VALUE(p.payload_json,'$.dueAtUtc'))) THROW 51917,'Current assignment mutation proof missing',1";
        var actorParameter = command.CreateParameter();
        actorParameter.ParameterName = "@actor"; actorParameter.Value = actor;
        command.Parameters.Add(actorParameter);
        var operationParameter = command.CreateParameter();
        operationParameter.ParameterName = "@operation"; operationParameter.Value = operation;
        command.Parameters.Add(operationParameter);
        await command.ExecuteNonQueryAsync(ct);
    }
}
