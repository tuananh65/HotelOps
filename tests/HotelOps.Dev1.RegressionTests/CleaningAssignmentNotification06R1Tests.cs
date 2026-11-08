#if HOTELOPS_NOTIFICATION_06R1
using System.Data;
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Entities.Notifications;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

// Runs against byte-identical DEV2 source in the disposable composition only.
public sealed class CleaningAssignmentNotification06R1Tests
{
    [Fact]
    public async Task Actual_06R1_publisher_rejects_cleaning_action_and_poisoned_transaction_cannot_verify()
    {
        using var connection = new NoSqlConnection();
        var guard = new NotificationCommitGuard();
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection).AddInterceptors(guard).Options);
        // The path guard fails before source policy or SQL. Neither dependency is invoked.
        var publisher = new NotificationPublisher(db, [new Profile()], null!, guard, TimeProvider.System);
        var participant = new NotificationTransactionParticipant(db, publisher, guard);
        await using var tx = await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<NotificationContractUnavailableException>(() => participant.PublishRequiredIntentsAsync(
            connection, tx.GetDbTransaction(), new(CleaningAssignmentNotificationRules.EventType, 1, 47), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.VerifyBeforeCommitAsync(connection, tx.GetDbTransaction(), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync());
        Assert.False(connection.Transaction!.Committed);
        await tx.RollbackAsync();
        Assert.True(connection.Transaction.RolledBack);
    }

    [Fact]
    public async Task Actual_source_policy_rejects_cleaning_read_policy_before_any_sql()
    {
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=unused.invalid;Database=NoConnection;Integrated Security=true").Options);
        var policy = new NotificationSourcePolicy(db, null!, TimeProvider.System);
        Assert.False(await policy.AllowsAsync(new NotificationIntent { ReadPolicy = CleaningAssignmentNotificationRules.ReadPolicy }, default));
    }

    [Fact]
    public async Task Actual_participant_rejects_foreign_connection_and_prevents_commit()
    {
        using var connection = new NoSqlConnection();
        using var foreign = new NoSqlConnection();
        var guard = new NotificationCommitGuard();
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection).AddInterceptors(guard).Options);
        var participant = new NotificationTransactionParticipant(db, null!, guard);
        await using var tx = await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.PublishRequiredIntentsAsync(
            foreign, tx.GetDbTransaction(), new(CleaningAssignmentNotificationRules.EventType, 1, 47), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync());
        Assert.False(connection.Transaction!.Committed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dev1_adapter_rejects_missing_or_foreign_transaction_before_loading_source(bool hasTransaction)
    {
        using var connection = new NoSqlConnection();
        using var foreign = new NoSqlConnection();
        var guard = new NotificationCommitGuard();
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection).AddInterceptors(guard).Options);
        var adapter = new CleaningAssignmentNotification06R1Adapter(db, null!, null!, guard);
        await using var tx = hasTransaction ? await db.Database.BeginTransactionAsync() : null;
        using var foreignTransaction = foreign.BeginTransaction();
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.StageAsync(foreign, foreignTransaction,
            47, 7, Guid.NewGuid(), new byte[32], default));
        if (tx != null)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync());
            Assert.False(connection.Transaction!.Committed);
        }
    }

    [Fact]
    public async Task Cancellation_in_actual_publisher_poisoned_caller_transaction()
    {
        using var connection = new NoSqlConnection();
        var guard = new NotificationCommitGuard();
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection).AddInterceptors(guard).Options);
        var publisher = new NotificationPublisher(db, [new Profile()], null!, guard, TimeProvider.System);
        var participant = new NotificationTransactionParticipant(db, publisher, guard);
        await using var tx = await db.Database.BeginTransactionAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => participant.PublishRequiredIntentsAsync(
            connection, tx.GetDbTransaction(), new(CleaningAssignmentNotificationRules.EventType, 1, 47), cancellation.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync());
        Assert.False(connection.Transaction!.Committed);
    }

    private sealed class Profile : INotificationEventProfile
    {
        public string EventType => CleaningAssignmentNotificationRules.EventType;
        public short Version => 1;
        public Task<ResolvedNotification> ResolveAsync(long occurrenceId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var c = CleaningAssignmentNotificationRules.Resolve(CleaningAssignmentNotificationTests.Snapshot(), true);
            Assert.Equal(c.OccurrenceId, occurrenceId);
            return Task.FromResult(new ResolvedNotification(c.Facts.TaskId, c.Facts.BranchId, c.Facts.AssigneeUserId,
                c.ReadPolicy, c.Title, c.Message, c.ActionPath));
        }
    }

    private sealed class NoSqlConnection : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull] public override string ConnectionString { get; set; } = "";
        public override string Database => "FS020_NoSql";
        public override string DataSource => "NoSql";
        public override string ServerVersion => "16.0";
        public override ConnectionState State => ConnectionState.Open;
        public NoSqlTransaction? Transaction { get; private set; }
        public override void Open() { }
        public override void Close() { }
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new InvalidOperationException("SQL forbidden in focused test.");
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => Transaction = new(this, isolationLevel);
    }

    private sealed class NoSqlTransaction(DbConnection connection, IsolationLevel isolation) : DbTransaction
    {
        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }
        public override IsolationLevel IsolationLevel => isolation;
        protected override DbConnection DbConnection => connection;
        public override void Commit() => Committed = true;
        public override void Rollback() => RolledBack = true;
    }
}
#endif
