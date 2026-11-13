using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HotelOps.Dev1.RegressionTests;

public enum CleaningCompletionAcceptanceCase
{
    Authorized, ValidEvidence, Replay, ConflictingReplay, SameKeyConcurrent, DifferentKeyConcurrent,
    MissingAssignment, WrongAssignee, StaleTask, StaleProgress, ChecklistIncomplete,
    ActiveBlocker, NoInterval, InvalidEvidence, AttributionFailure, CompletionAuditFailure,
    ReadinessFailure, NotificationPublicationFailure, NotificationIntentFailure,
    BeforeCommitFailure, LostCommitAcknowledgement
}

// Test-only failure injection. Never registered in application DI.
public sealed class CleaningCompletionWriteFault(CleaningCompletionAcceptanceCase scenario) : DbCommandInterceptor
{
    public bool Fired { get; private set; }
    public bool Matches(DbCommand command)
    {
        var sql = command.CommandText;
        bool Has(string text) => sql.Contains(text, StringComparison.OrdinalIgnoreCase);
        return scenario switch
        {
            CleaningCompletionAcceptanceCase.AttributionFailure => Has("EXEC dbo.fe04_capture_cleaner_attribution"),
            // Exclude Work-Time's earlier interval audit: this fault must occur AFTER capture.
            CleaningCompletionAcceptanceCase.CompletionAuditFailure => Has("INSERT INTO [audit_logs]")
                && command.Parameters.Cast<DbParameter>().Any(p => p.Value is string value
                    && value.Contains("COMPLETE_CLEANING", StringComparison.Ordinal)),
            CleaningCompletionAcceptanceCase.ReadinessFailure => Has("INSERT dbo.room_readiness_events"),
            CleaningCompletionAcceptanceCase.NotificationPublicationFailure => Has("INSERT dbo.notification_publications"),
            CleaningCompletionAcceptanceCase.NotificationIntentFailure => Has("INSERT dbo.notification_delivery_intents"),
            _ => false
        };
    }
    private void Check(DbCommand command)
    {
        if (!Matches(command)) return;
        Fired = true;
        throw new CleaningCompletionInjectedFaultException();
    }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Check(command); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { Check(command); return ValueTask.FromResult(result); }
}

public sealed class CleaningCompletionInjectedFaultException() : InvalidOperationException("UC034_ACCEPTANCE_INJECTED_FAILURE");

public sealed class CleaningCompletionCommitFault(CleaningCompletionAcceptanceCase scenario) : DbTransactionInterceptor
{
    private int fired;
    public bool Fired => Volatile.Read(ref fired) != 0;
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (scenario == CleaningCompletionAcceptanceCase.BeforeCommitFailure && Interlocked.Exchange(ref fired, 1) == 0)
            throw new CleaningCompletionInjectedFaultException();
        return ValueTask.FromResult(result);
    }
    public override Task TransactionCommittedAsync(DbTransaction transaction,
        TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (scenario == CleaningCompletionAcceptanceCase.LostCommitAcknowledgement && Interlocked.Exchange(ref fired, 1) == 0)
            throw new OperationCanceledException("UC034_ACCEPTANCE_LOST_COMMIT_ACK");
        return Task.CompletedTask;
    }
}

// Revalidate externally granted lease immediately before every EF provider commit.
// This never creates/extends a lease and is not a Notification persistence guard.
public sealed class CleaningCompletionLeaseCommitGuard(Func<CancellationToken, Task> validateLease) : DbTransactionInterceptor
{
    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        await validateLease(cancellationToken);
        return result;
    }
}