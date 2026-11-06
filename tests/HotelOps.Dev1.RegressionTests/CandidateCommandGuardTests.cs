using System.Data;
using System.Data.Common;
using HotelOps.Api.Services.Notifications;
using Microsoft.Data.SqlClient;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class CandidateCommandGuardTests
{
    [Theory]
    [InlineData("COMMIT TRANSACTION")]
    [InlineData("SELECT 1; ROLLBACK")]
    [InlineData("/* comment */ BEGIN DISTRIBUTED TRANSACTION")]
    [InlineData("SAVE TRANSACTION hidden")]
    public async Task Armed_EF_transaction_control_is_rejected_before_execution(string sql)
    {
        using var command = new Command(sql);
        CandidateCommitGuard.Instance.Arm(command.Transaction!, 1, Guid.NewGuid());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await CandidateCommandGuard.Instance.NonQueryExecutingAsync(command, null!, default));
        Assert.Equal("CANDIDATE_TRANSACTION_CONTROL_SQL_UNSUPPORTED", error.Message);
    }

    [Fact]
    public void Armed_sync_command_is_rejected()
    {
        using var command = new Command("SELECT 1");
        CandidateCommitGuard.Instance.Arm(command.Transaction!, 1, Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => CandidateCommandGuard.Instance.ScalarExecuting(command, null!, default));
    }

    [Fact]
    public async Task Unarmed_commands_and_parameterized_business_SQL_are_unchanged()
    {
        using var command = new Command("COMMIT");
        Assert.False((await CandidateCommandGuard.Instance.NonQueryExecutingAsync(command, null!, default)).HasResult);
        CandidateCommitGuard.Instance.Arm(command.Transaction!, 1, Guid.NewGuid());
        command.CommandText = "UPDATE dbo.cleaning_tasks SET due_at=@due WHERE cleaning_task_id=@id";
        Assert.False((await CandidateCommandGuard.Instance.NonQueryExecutingAsync(command, null!, default)).HasResult);
    }

    private sealed class Transaction : DbTransaction
    {
        private readonly SqlConnection connection = new();
        protected override DbConnection DbConnection => connection;
        public override IsolationLevel IsolationLevel => IsolationLevel.Serializable;
        public override void Commit() => throw new NotSupportedException();
        public override void Rollback() => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) connection.Dispose(); base.Dispose(disposing); }
    }

    private sealed class Command(string sql) : DbCommand
    {
        private readonly SqlCommand parameters = new();
        [System.Diagnostics.CodeAnalysis.AllowNull] public override string CommandText { get; set; } = sql;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbTransaction? DbTransaction { get; set; } = new Transaction();
        protected override DbParameterCollection DbParameterCollection => parameters.Parameters;
        public override void Cancel() => throw new NotSupportedException();
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object? ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare() => throw new NotSupportedException();
        protected override DbParameter CreateDbParameter() => new SqlParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) { DbTransaction?.Dispose(); parameters.Dispose(); } base.Dispose(disposing); }
    }
}
