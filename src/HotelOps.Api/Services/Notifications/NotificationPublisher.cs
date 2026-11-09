using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HotelOps.Api.Services.Notifications;

public sealed class NotificationPublisher(HotelOpsDbContext db, IEnumerable<INotificationEventProfile> profiles,
    NotificationSourcePolicy policy, NotificationCommitGuard guard, TimeProvider clock) : INotificationPublisher
{
    public Task<NotificationStaged> PublishRequiredIntentsAsync(NotificationEvent sourceEvent, CancellationToken ct)
        => StageAsync(sourceEvent, ct);

    internal static byte[] Digest(NotificationIntent n) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
        n.EventType, n.EventVersion, n.OccurrenceId, n.SourceId, n.BranchId, n.RecipientUserId,
        n.ReadPolicy, n.Title, n.Message, n.ActionPath }));

    public async Task<NotificationStaged> StageAsync(NotificationEvent sourceEvent, CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Notification staging requires a caller-owned local transaction.");
        try
        {
        guard.RequireAttached(db, tx.TransactionId);
        ct.ThrowIfCancellationRequested();
        if (sourceEvent is null || string.IsNullOrWhiteSpace(sourceEvent.EventType) || sourceEvent.EventType.Length > 60 ||
            sourceEvent.Version <= 0 || sourceEvent.OccurrenceId <= 0)
            throw new NotificationContractUnavailableException();
        var profile = profiles.SingleOrDefault(p => p.EventType == sourceEvent.EventType && p.Version == sourceEvent.Version)
            ?? throw new NotificationContractUnavailableException();
        var resolved = await profile.ResolveAsync(sourceEvent.OccurrenceId, ct);
        var intent = new NotificationIntent { EventType = sourceEvent.EventType, EventVersion = sourceEvent.Version,
            OccurrenceId = sourceEvent.OccurrenceId, SourceId = resolved.SourceId, BranchId = resolved.BranchId,
            RecipientUserId = resolved.RecipientId, ReadPolicy = resolved.ReadPolicy, Title = resolved.Title,
            Message = resolved.Message, ActionPath = resolved.ActionPath,
            CreatedUtc = clock.GetUtcNow().UtcDateTime, NextAttemptUtc = clock.GetUtcNow().UtcDateTime };
        var source = NotificationSourceReference.Resolve(intent);
        if (intent.OccurrenceId <= 0 || intent.SourceId <= 0 || intent.BranchId <= 0 || intent.RecipientUserId <= 0 ||
            intent.Title.Length is 0 or > 200 || intent.Message.Length is 0 or > 2000 ||
            source is null)
            throw new NotificationContractUnavailableException();
        // Recognize the genuine Cleaning contract, but never add a C04 queue beside its FS013
        // graph. A separately reviewed schema/graph reconciliation is required before staging.
        if (source.RequiresOperationGraphReconciliation || !await policy.AllowsAsync(intent, ct))
            throw new NotificationContractUnavailableException();
        intent.PayloadHash = Digest(intent);
        // One transaction-owned key range: concurrent duplicates serialize before unique insertion.
        // C04 profiles resolve exactly one recipient. Replay cannot retarget an occurrence.
        var existing = db.NotificationIntents.Local.SingleOrDefault(x => x.EventType == intent.EventType &&
            x.EventVersion == intent.EventVersion && x.OccurrenceId == intent.OccurrenceId)
            ?? await db.NotificationIntents.FromSqlInterpolated($"SELECT * FROM dbo.notification_intents_c04 WITH(UPDLOCK,HOLDLOCK) WHERE EventType={intent.EventType} AND EventVersion={intent.EventVersion} AND OccurrenceId={intent.OccurrenceId}")
                .SingleOrDefaultAsync(ct);
        if (existing is not null)
        {
            if (!Digest(existing).SequenceEqual(intent.PayloadHash) || !existing.PayloadHash.SequenceEqual(intent.PayloadHash))
                throw new NotificationConflictException();
            intent = existing;
        }
        else db.NotificationIntents.Add(intent);
        guard.Bind(db, tx.TransactionId, intent);
        return new(1);
        }
        catch
        {
            // Catching a required staging error cannot turn the business commit into success.
            guard.Fail(db, tx.TransactionId);
            throw;
        }
    }
}

// Every staged mandatory intent must have its exact canonical payload persisted before EF commit.
// Binding is per DbContext and transaction; no SaveChanges/commit is owned by the publisher.
public sealed class NotificationCommitGuard : DbTransactionInterceptor
{
    private sealed class Binding(Guid transactionId)
    {
        public Guid TransactionId { get; } = transactionId;
        public bool Failed { get; set; }
        public Dictionary<NotificationIntent, Proof> Intents { get; } = [];
        public Dictionary<string, Func<CancellationToken, Task>> GraphChecks { get; } = [];
    }
    private sealed record Proof(byte[] Hash, string State, long? NotificationId);
    private readonly ConditionalWeakTable<DbContext, Binding> bindings = new();
    private sealed record Lease(Guid TransactionId);
    private readonly ConditionalWeakTable<DbContext, Lease> leases = new();
    public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData data,
        DbTransaction result, CancellationToken ct = default)
    {
        if (data.Context is not null)
        {
            leases.Remove(data.Context);
            leases.Add(data.Context, new(data.TransactionId));
        }
        return ValueTask.FromResult(result);
    }
    internal void RequireAttached(HotelOpsDbContext db, Guid transactionId)
    {
        if (!leases.TryGetValue(db, out var lease) || lease.TransactionId != transactionId)
            throw new InvalidOperationException("Notification commit guard is not attached to this asynchronous EF transaction.");
    }
    internal void Bind(HotelOpsDbContext db, Guid transactionId, NotificationIntent intent)
    {
        if (bindings.TryGetValue(db, out var prior) && prior.TransactionId != transactionId) bindings.Remove(db);
        var binding = bindings.GetValue(db, _ => new Binding(transactionId));
        binding.Intents.TryAdd(intent, new(NotificationPublisher.Digest(intent), intent.State, intent.NotificationId));
    }
    internal void Fail(HotelOpsDbContext db, Guid transactionId)
    {
        if (bindings.TryGetValue(db, out var prior) && prior.TransactionId != transactionId) bindings.Remove(db);
        bindings.GetValue(db, _ => new Binding(transactionId)).Failed = true;
    }
    // Extends this guard for the existing FS013 graph; never creates a C04 intent.
    internal void BindGraph(HotelOpsDbContext db, Guid transactionId, string identity,
        Func<CancellationToken, Task> verify)
    {
        RequireAttached(db, transactionId);
        if (bindings.TryGetValue(db, out var prior) && prior.TransactionId != transactionId) bindings.Remove(db);
        bindings.GetValue(db, _ => new Binding(transactionId)).GraphChecks[identity] = verify;
    }
    private bool Bound(TransactionEventData data, out Binding? binding)
    {
        binding = null;
        return data.Context is not null && bindings.TryGetValue(data.Context, out binding) && binding.TransactionId == data.TransactionId;
    }

    public override InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData data, InterceptionResult result)
    {
        if (Bound(data, out _)) throw new InvalidOperationException("Notification transactions require asynchronous commit.");
        return result;
    }
    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData data, InterceptionResult result, CancellationToken ct = default)
    {
        if (!Bound(data, out var binding)) return result;
        var db = (HotelOpsDbContext)data.Context!;
        await VerifyAsync(db, binding!, ct);
        return result;
    }
    public Task VerifyBeforeCommitAsync(HotelOpsDbContext db, CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Caller transaction required.");
        RequireAttached(db, tx.TransactionId);
        return bindings.TryGetValue(db, out var binding) && binding.TransactionId == tx.TransactionId
            ? VerifyAsync(db, binding, ct) : throw new InvalidOperationException("No required publication staged.");
    }
    private static async Task VerifyAsync(HotelOpsDbContext db, Binding binding, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (binding.Failed) throw new InvalidOperationException("Required notification staging failed; roll back the caller transaction.");
        foreach (var verify in binding.GraphChecks.Values) await verify(ct);
        foreach (var (intent, expected) in binding!.Intents)
        {
            if (db.Entry(intent).State != EntityState.Unchanged || intent.IntentId <= 0)
                throw new InvalidOperationException("Mandatory notification intent was not flushed.");
            var stored = await db.NotificationIntents.AsNoTracking().SingleOrDefaultAsync(x => x.IntentId == intent.IntentId, ct);
            if (stored is null || !NotificationPublisher.Digest(stored).SequenceEqual(expected.Hash) ||
                !stored.PayloadHash.SequenceEqual(expected.Hash) || stored.State != expected.State || stored.NotificationId != expected.NotificationId)
                throw new InvalidOperationException("Mandatory notification graph changed before commit.");
        }
    }
}
