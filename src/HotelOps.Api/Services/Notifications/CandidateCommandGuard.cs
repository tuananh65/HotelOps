using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HotelOps.Api.Services.Notifications;

// Defense in depth for supported EF command paths only. This is deliberately not a SQL parser,
// provider wrapper or a security boundary against arbitrary SQL/dynamic stored procedures.
public sealed class CandidateCommandGuard : DbCommandInterceptor
{
    public static CandidateCommandGuard Instance { get; } = new();
    private static readonly Regex TransactionControl = new(@"\b(COMMIT|ROLLBACK|BEGIN\s+(DISTRIBUTED\s+)?TRAN(SACTION)?|SAVE\s+TRAN(SACTION)?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static void Verify(DbCommand command, bool asynchronous)
    {
        if (!CandidateCommitGuard.Instance.IsArmed(command.Transaction)) return;
        if (!asynchronous) throw new InvalidOperationException("CANDIDATE_REQUIRES_ASYNC_COMMAND");
        if (TransactionControl.IsMatch(command.CommandText)) throw new InvalidOperationException("CANDIDATE_TRANSACTION_CONTROL_SQL_UNSUPPORTED");
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) { Verify(command, false); return result; }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) { Verify(command, false); return result; }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result) { Verify(command, false); return result; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default) { Verify(command, true); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default) { Verify(command, true); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken ct = default) { Verify(command, true); return ValueTask.FromResult(result); }
}
