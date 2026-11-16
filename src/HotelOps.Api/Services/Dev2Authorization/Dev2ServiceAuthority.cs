using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Dev2Authorization;

/// <summary>Service command capability. A client actor or confirmed user alone cannot create a permit.</summary>
public static class Dev2ServiceAuthority
{
    private static readonly object RequestGrantKey = new();

    public static CurrentUserInfo CurrentIdentity(IHttpContextAccessor? accessor)
    {
        if (accessor?.HttpContext is null) throw new Dev2AccessDenied(401);
        try
        {
            var current = accessor.HttpContext.RequestServices.GetService<ICurrentUserAccessor>()
                ?? throw new Dev2AccessDenied(503);
            if (!current.TryGetCurrentUser(out var actor) || actor.UserId <= 0 || actor.SessionId <= 0)
                throw new Dev2AccessDenied(401);
            if (actor.IsRestricted) throw new Dev2AccessDenied(403);
            return actor;
        }
        catch (Exception error) when (error is TimeoutException or InvalidOperationException)
        { throw new Dev2AccessDenied(503); }
    }

    public static long ActorId(IHttpContextAccessor? accessor)
    {
        var actor = CurrentIdentity(accessor);
        RequireGrant(accessor, actor);
        return actor.UserId;
    }

    public static void RequireClaim(IHttpContextAccessor? accessor, long claimedActor)
    {
        if (ActorId(accessor) != claimedActor) throw new Dev2AccessDenied(403);
    }

    public static long RequireEntry(IHttpContextAccessor? accessor, string controller, string[] allowedActions,
        long claimedActor, long? rootId, object? input)
    {
        var actor = CurrentIdentity(accessor);
        if (claimedActor != actor.UserId) throw new Dev2AccessDenied(403);
        var grant = RequireGrant(accessor, actor);
        var identity = grant.Action.Identity;
        if (identity.HttpMethod == "GET" || identity.ControllerType.Name != controller + "Controller"
            || !allowedActions.Contains(identity.ActionName, StringComparer.Ordinal)
            || !grant.Contract.MatchesServiceInvocation(grant.Action,
                new(controller, identity.ActionName, rootId, input)))
            throw new Dev2AccessDenied(403);
        return actor.UserId;
    }

    public static async Task RevalidateAsync(HotelOpsDbContext db, IHttpContextAccessor? accessor,
        Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        var actor = CurrentIdentity(accessor);
        var grant = RequireGrant(accessor, actor);
        if (grant.Action.Identity.HttpMethod == "GET"
            || !ReferenceEquals(db, accessor!.HttpContext!.RequestServices.GetService<HotelOpsDbContext>())
            || db.Database.CurrentTransaction is null)
            throw new Dev2AccessDenied(403);
        await grant.RevalidateAsync(phase, ct);
    }

    public static async Task<int> SaveChangesAsync(HotelOpsDbContext db, IHttpContextAccessor? accessor, CancellationToken ct)
    {
        await RevalidateAsync(db, accessor, Dev2AuthorizationPhase.BeforePersistence, ct);
        return await db.SaveChangesAsync(ct);
    }

    public static async Task<IDbContextTransaction> BeginTransactionAsync(HotelOpsDbContext db, IHttpContextAccessor? accessor, CancellationToken ct)
    {
        var actor = CurrentIdentity(accessor);
        var grant = RequireGrant(accessor, actor);
        if (db.Database.CurrentTransaction is not null)
        {
            if (ReferenceEquals(db.Database.CurrentTransaction, grant.ActionTransaction))
                return new ActionTransactionLease(grant.ActionTransaction!);
            throw new Dev2AccessDenied(503);
        }
        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            if (Dev2DemoBindings.DemoReference(grant.Contract.AcceptedSourceReference))
                await Dev2DemoBindings.LockAuthorityAsync(grant.Action, ct);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    public static Task CommitAsync(HotelOpsDbContext db, IHttpContextAccessor? accessor,
        IDbContextTransaction transaction, CancellationToken ct) => CommitVerifiedAsync(transaction, async token =>
    {
        // Reuse the existing contract callback after all saves and returned-detail reads.
        // This hook cannot supply a missing action policy or transaction authority contract.
        await RevalidateAsync(db, accessor, Dev2AuthorizationPhase.BeforePersistence, token);
        var actual = transaction is ActionTransactionLease lease ? lease.Owner : transaction;
        if (!ReferenceEquals(db.Database.CurrentTransaction, actual)) throw new Dev2AccessDenied(403);
        if (transaction is not ActionTransactionLease)
            foreach (var files in RequireGrant(accessor, CurrentIdentity(accessor)).EvidenceFiles)
                await files.BeforeCommitAsync(token);
    }, ct);

    internal static async Task CommitVerifiedAsync(IDbContextTransaction transaction,
        Func<CancellationToken, Task> revalidate, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await revalidate(ct);
        ct.ThrowIfCancellationRequested();
        if (transaction is not ActionTransactionLease) await transaction.CommitAsync(ct);
    }

    internal static async Task<IDbContextTransaction> BeginActionAsync(HotelOpsDbContext db, IHttpContextAccessor accessor, CancellationToken ct)
    {
        var transaction = await BeginTransactionAsync(db, accessor, ct);
        RequireGrant(accessor, CurrentIdentity(accessor)).ActionTransaction = transaction;
        return transaction;
    }

    internal static void Install(HttpContext context, Dev2ActionGrant grant) => context.Items[RequestGrantKey] = grant;
    internal static HotelOps.Api.Services.Housekeeping.EvidenceFileLifetime OwnEvidence(IHttpContextAccessor? accessor)
    {
        var grant = RequireGrant(accessor, CurrentIdentity(accessor));
        var files = new HotelOps.Api.Services.Housekeeping.EvidenceFileLifetime();
        grant.EvidenceFiles.Add(files);
        return files;
    }

    internal static void Remove(HttpContext context)
    {
        try
        {
            if (context.Items[RequestGrantKey] is Dev2ActionGrant grant)
                foreach (var files in grant.EvidenceFiles) files.Dispose();
        }
        finally { context.Items.Remove(RequestGrantKey); }
    }
    private static Dev2ActionGrant RequireGrant(IHttpContextAccessor? accessor, CurrentUserInfo actor)
    {
        if (accessor?.HttpContext?.Items[RequestGrantKey] is not Dev2ActionGrant grant
            || grant.Action.Actor != actor || !ReferenceEquals(grant.Action.Http, accessor.HttpContext))
            throw new Dev2AccessDenied(403);
        return grant;
    }
}

internal sealed class Dev2ActionGrant(Dev2BoundAction action, Dev2ActionContract contract,
    Func<Dev2AuthorizationPhase, CancellationToken, Task> revalidate)
{
    internal Dev2BoundAction Action { get; } = action;
    internal Dev2ActionContract Contract { get; } = contract;
    internal IDbContextTransaction? ActionTransaction { get; set; }
    internal List<HotelOps.Api.Services.Housekeeping.EvidenceFileLifetime> EvidenceFiles { get; } = [];
    private readonly byte[] inputHash = Hash(action);
    internal async Task RevalidateAsync(Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (!CryptographicOperations.FixedTimeEquals(inputHash, Hash(Action))) throw new Dev2AccessDenied(403);
        await revalidate(phase, ct);
    }
    private static byte[] Hash(Dev2BoundAction action) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        action.Arguments.Where(x => x.Value is not CancellationToken).OrderBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.Value)));
}

// Services retain their existing save/rollback boundaries. Expanded MVC owns the final commit
// through the fresh disclosure check, so grant revocation cannot slip between commit and that check.
internal sealed class ActionTransactionLease(IDbContextTransaction owner) : IDbContextTransaction
{
    internal IDbContextTransaction Owner { get; } = owner;
    public Guid TransactionId => Owner.TransactionId;
    public bool SupportsSavepoints => Owner.SupportsSavepoints;
    public void Commit() => throw new InvalidOperationException("Expanded action owns final commit");
    public Task CommitAsync(CancellationToken ct=default) => throw new InvalidOperationException("Expanded action owns final commit");
    public void Rollback() => Owner.Rollback();
    public Task RollbackAsync(CancellationToken ct=default) => Owner.RollbackAsync(ct);
    public void CreateSavepoint(string name) => Owner.CreateSavepoint(name);
    public Task CreateSavepointAsync(string name,CancellationToken ct=default) => Owner.CreateSavepointAsync(name,ct);
    public void RollbackToSavepoint(string name) => Owner.RollbackToSavepoint(name);
    public Task RollbackToSavepointAsync(string name,CancellationToken ct=default) => Owner.RollbackToSavepointAsync(name,ct);
    public void ReleaseSavepoint(string name) => Owner.ReleaseSavepoint(name);
    public Task ReleaseSavepointAsync(string name,CancellationToken ct=default) => Owner.ReleaseSavepointAsync(name,ct);
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
