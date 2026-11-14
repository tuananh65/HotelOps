using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Housekeeping.Acceptance;

// Persisted fixtures must be prepared through separately authorized owner flows. This reader never seeds them.
public sealed record Fe04OwnedFixtureManifest(string Owner, string Prefix, Guid DatabaseGuid, Guid LeaseId,
    long[] BranchIds, long[] TaskIds, long[] ActorIds, long[] AssigneeIds);

public sealed class Fe04CleaningSourceAuthority : IFe04AcceptanceSourceAuthority
{
    private readonly HotelOpsDbContext db;
    private readonly ICleaningActorAuthority actorAuthority;
    private readonly Fe04OwnedFixtureManifest fixture;
    private readonly byte[] fixtureBytes;
    private readonly Fe04NotificationTrialContract contract;
    private readonly string fixturePath;
    private readonly TimeProvider clock;
    public string ApprovalPayloadSha256 { get; }

    private Fe04CleaningSourceAuthority(HotelOpsDbContext context, ICleaningActorAuthority authority,
        Fe04OwnedFixtureManifest owned, byte[] bytes, Fe04NotificationTrialContract c, string path, string digest, TimeProvider time)
        => (db, actorAuthority, fixture, fixtureBytes, contract, fixturePath, ApprovalPayloadSha256, clock) =
            (context, authority, owned, bytes, c, path, digest, time);

    public static async Task<Fe04CleaningSourceAuthority> CreateAsync(HotelOpsDbContext db,
        ICleaningActorAuthority actorAuthority, Fe04SignedApproval approval, IReadOnlyDictionary<string, string> ownerPins,
        string fixturePath, TimeProvider clock, CancellationToken ct)
    {
        var contract = Fe04NotificationApproval.Verify(approval, ownerPins, clock.GetUtcNow());
        var bytes = await File.ReadAllBytesAsync(fixturePath, ct);
        if (Fe04NotificationApproval.Hash(bytes) != contract.FixtureManifestSha256)
            throw new InvalidOperationException("ACTUAL_FIXTURE_RECEIPT_HASH_MISMATCH");
        var fixture = JsonSerializer.Deserialize<Fe04OwnedFixtureManifest>(bytes)
            ?? throw new InvalidOperationException("OWNER_FIXTURE_MANIFEST_REQUIRED");
        if (fixture.Owner != contract.FixtureOwner || fixture.DatabaseGuid != contract.DatabaseGuid || fixture.LeaseId != contract.LeaseId ||
            fixture.Prefix is null || fixture.Prefix.Length is < 13 or > 20 || !fixture.Prefix.StartsWith("F04P0-", StringComparison.Ordinal) ||
            fixture.Prefix.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') ||
            new[] { fixture.BranchIds, fixture.TaskIds, fixture.ActorIds, fixture.AssigneeIds }.Any(ids => ids is null || ids.Length == 0 || ids.Any(id => id <= 0)))
            throw new InvalidOperationException("EXACT_APPROVED_FIXTURE_OWNERSHIP_REQUIRED");
        return new(db, actorAuthority, fixture, bytes, contract, fixturePath, Fe04NotificationApproval.Hash(approval.Payload), clock);
    }

    public async Task RequireCurrentSessionSourceAndFixtureOwnershipAsync(SqlConnection connection, SqlTransaction transaction,
        CleaningAssignmentCommand command, CancellationToken ct)
    {
        Fe04NotificationApproval.Require(contract, clock.GetUtcNow());
        if (!ReferenceEquals(db.Database.GetDbConnection(), connection) ||
            !ReferenceEquals(db.Database.CurrentTransaction?.GetDbTransaction(), transaction))
            throw new InvalidOperationException("EXACT_DOMAIN_CONTEXT_TRANSACTION_REQUIRED");
        Fe04AcceptanceTransaction.Require(connection, transaction);
        var currentFixtureBytes = await File.ReadAllBytesAsync(fixturePath, ct);
        if (!CryptographicOperations.FixedTimeEquals(fixtureBytes, currentFixtureBytes) ||
            !fixture.BranchIds.Contains(command.BranchId) || !fixture.TaskIds.Contains(command.TaskId) ||
            !fixture.ActorIds.Contains(command.ActorId) || !fixture.AssigneeIds.Contains(command.AssigneeId))
            throw new InvalidOperationException("OWNED_FIXTURE_SCOPE_REQUIRED");
        // Reuse the real session ticket, revocation/eligibility, CLEANING_ASSIGN and branch locks.
        await actorAuthority.RequireWriterAsync(command, ct);
        var prefix = fixture.Prefix;
        if (!await db.Branches.AsNoTracking().AnyAsync(b => b.BranchId == command.BranchId && b.BranchCode.StartsWith(prefix), ct) ||
            !await db.CleaningTasks.AsNoTracking().AnyAsync(t => t.CleaningTaskId == command.TaskId && t.TaskNo.StartsWith(prefix) &&
                t.Room.BranchId == command.BranchId && t.Room.RoomNumber.StartsWith(prefix), ct))
            throw new InvalidOperationException("PERSISTED_OWNED_TASK_ROOM_BRANCH_REQUIRED");
        foreach (var user in new[] { command.ActorId, command.AssigneeId })
            if (!await db.Users.AsNoTracking().AnyAsync(u => u.UserId == user && u.Username.StartsWith(prefix), ct))
                throw new InvalidOperationException("PERSISTED_OWNED_ACTORS_REQUIRED");
        // Full workflow/rowversion/assignee/deadline checks remain in the unchanged authoritative caller.
    }
}
