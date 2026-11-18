using System.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed record OperationalTaskBranchOption(long Id, string Name, bool IsPrimary, bool CanCreate);

public sealed partial class RoomTypeCreationService
{
    public async Task<RoomTypeBatchResult> OperationalTaskBranchesAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid || actor.IsRestricted)
            return new(RoomTypeBatchStatus.SessionInvalid);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([actor.UserId], ct);
        async Task<bool> Valid() => (await sessions.ValidateSessionAsync(ticket, ct)) is { IsValid: true, Session: { IsRestricted: false } s } && s.SessionId == sid;
        if (!await Valid()) return new(RoomTypeBatchStatus.SessionInvalid);
        var primary = await db.Users.AsNoTracking().Where(u => u.UserId == actor.UserId).Select(u => u.PrimaryBranchId).SingleAsync(ct);
        var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchName)
            .Select(b => new { b.BranchId, b.BranchName }).ToArrayAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var paths = await new EfPermissionDataSource(db).ReadPathsAsync(actor.UserId, "TASK_VIEW", now, ct);
        var result = new List<OperationalTaskBranchOption>();
        foreach (var b in branches)
        {
            var wide = await permissions.EvaluateBranchAsync("TASK_VIEW", b.BranchId, ct);
            if (wide.Decision == PermissionDecision.Unavailable) return new(RoomTypeBatchStatus.Unavailable);
            if (!wide.IsGranted && !paths.Any(p => p.PermissionCode == "TASK_VIEW" && p.BranchId == b.BranchId &&
                PermissionGrantRules.IsEligible(p, now) && PermissionGrantRules.EffectiveScope(p) == "OWN")) continue;
            var create = await permissions.EvaluateBranchAsync("TASK_CREATE", b.BranchId, ct);
            if (create.Decision == PermissionDecision.Unavailable) return new(RoomTypeBatchStatus.Unavailable);
            result.Add(new(b.BranchId, b.BranchName, b.BranchId == primary, create.IsGranted));
        }
        if (!await Valid()) return new(RoomTypeBatchStatus.SessionInvalid);
        await tx.CommitAsync(ct);
        return new(RoomTypeBatchStatus.Success, result);
    }
}
