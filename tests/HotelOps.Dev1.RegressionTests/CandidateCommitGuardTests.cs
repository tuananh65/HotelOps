using System.Data;
using System.Data.Common;
using HotelOps.Api.Services.Notifications;
using Microsoft.Data.SqlClient;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class CandidateCommitGuardTests
{
    [Fact]
    public void Armed_sync_commit_is_rejected_before_provider_commit()
    {
        using var tx = new TestTransaction();
        var guard = new CandidateCommitGuard();
        guard.Arm(tx, 1, Guid.NewGuid());
        var error = Assert.Throws<InvalidOperationException>(() => guard.TransactionCommitting(tx, null!, default));
        Assert.Equal("CANDIDATE_REQUIRES_ASYNC_COMMIT", error.Message);
    }

    [Fact]
    public void Unrelated_sync_commit_remains_unchanged()
    {
        using var tx = new TestTransaction();
        Assert.False(new CandidateCommitGuard().TransactionCommitting(tx, null!, default).IsSuppressed);
    }

    [Fact]
    public async Task Unrelated_async_commit_remains_unchanged()
    {
        using var tx = new TestTransaction();
        Assert.False((await new CandidateCommitGuard().TransactionCommittingAsync(tx, null!, default)).IsSuppressed);
    }

    [Fact]
    public async Task Armed_cancelled_commit_is_rejected_before_sql()
    {
        using var tx = new TestTransaction();
        var guard = new CandidateCommitGuard();
        guard.Arm(tx, 1, Guid.NewGuid());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await guard.TransactionCommittingAsync(tx, null!, default, cancellation.Token));
    }

    [Fact]
    public void Arming_same_transaction_twice_cannot_replace_command_identity()
    {
        using var tx = new TestTransaction();
        var guard = new CandidateCommitGuard();
        guard.Arm(tx, 1, Guid.NewGuid());
        Assert.Throws<ArgumentException>(() => guard.Arm(tx, 2, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Invalid_command_identity_cannot_arm_transaction(long actor, bool emptyKey)
    {
        using var tx = new TestTransaction();
        Assert.Throws<InvalidOperationException>(() => new CandidateCommitGuard().Arm(tx, actor, emptyKey ? Guid.Empty : Guid.NewGuid()));
    }

    [Fact]
    public async Task Disconnected_transaction_cannot_prove_persisted_state()
    {
        using var tx = new TestTransaction { Disconnected = true };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CandidateCommitGuard.VerifyAsync(tx, 1, Guid.NewGuid(), default));
        Assert.Equal("TRANSACTION_DISPOSED", error.Message);
    }

    private sealed class TestTransaction : DbTransaction
    {
        private readonly SqlConnection connection = new();
        public bool Disconnected { get; init; }
        protected override DbConnection? DbConnection => Disconnected ? null : connection;
        public override IsolationLevel IsolationLevel => IsolationLevel.Serializable;
        public override void Commit() => throw new NotSupportedException();
        public override void Rollback() => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) connection.Dispose(); base.Dispose(disposing); }
    }
}
