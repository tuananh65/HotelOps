using System.ComponentModel.DataAnnotations;
using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public enum TurnoverHistoryStatus { Success, SessionInvalid, Forbidden, NotFound, Inactive, SchemaRequired, Unavailable }
public sealed record TurnoverHistoryResult(TurnoverHistoryStatus Status, object? Value = null);

/// <summary>UC038 read-only extraction. Owns only its read transaction; no business writers.</summary>
public sealed partial class TurnoverHistoryService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, ISqlSessionService sessions)
{
    private const string Permission = "CLEANING_VIEW";
    private async Task<TurnoverHistoryResult> ReadAsync(SessionReference ticket, long sessionId, long? branchId,
        Func<CancellationToken, Task<TurnoverHistoryResult>> read, CancellationToken ct, bool allowScopedList = true)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if (branchId <= 0) throw new ValidationException("BranchId must be positive.");
        ct.ThrowIfCancellationRequested();
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sessionId
            || actor.IsRestricted || ticket.IsRestricted) return new(TurnoverHistoryStatus.SessionInvalid);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || System.Transactions.Transaction.Current != null) throw new InvalidOperationException("A clean local SQL context is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var locked = await new UserEligibilityLock(db).LockAsync([actor.UserId], ct);
            if (!locked.TryGetValue(actor.UserId, out var user) || user is null || user.DeletedAt != null
                || user.AccountStatus != "ACTIVE" || user.EmploymentStatus != "ACTIVE") return new(TurnoverHistoryStatus.SessionInvalid);
            if (await Gate() is { } denied) return new(denied);
            if (branchId is { } branch)
            {
                var parent = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={branch}")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                if (parent == null) return new(TurnoverHistoryStatus.NotFound);
                if (!parent.IsActive) return new(TurnoverHistoryStatus.Inactive);
            }
            var locks = await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; SELECT @r AS [Value];").ToArrayAsync(ct);
            if (locks.Length != 1 || locks[0] < 0) return new(TurnoverHistoryStatus.Unavailable);

            var result = await read(ct);
            if (result.Status != TurnoverHistoryStatus.Success) return result;
            if (await Gate() is { } final) return new(final);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception error) when (!ct.IsCancellationRequested && SqlNumber(error) is 1205 or 1222 or -2 or 207 or 208)
        { return new(TurnoverHistoryStatus.Unavailable); }
        catch (Exception error) when (ct.IsCancellationRequested && error is System.Data.Common.DbException or DbUpdateException or SessionStoreUnavailableException)
        { throw new OperationCanceledException("Turnover history cancelled.", error, ct); }

        async Task<TurnoverHistoryStatus?> Gate()
        {
            var valid = await sessions.ValidateSessionAsync(ticket, ct);
            if (!valid.IsValid || valid.Session?.SessionId != sessionId || valid.Session.IsRestricted) return TurnoverHistoryStatus.SessionInvalid;
            var access = branchId is { } branch ? await permissions.EvaluateBranchAsync(Permission, branch, ct)
                : await permissions.EvaluateGlobalAsync(Permission, ct);
            ct.ThrowIfCancellationRequested();
            if (access.IsGranted) return null;
            if (access.Decision == PermissionDecision.Unavailable) return TurnoverHistoryStatus.Unavailable;
            if (branchId == null && allowScopedList)
                foreach (var id in await db.Branches.AsNoTracking().Select(x => x.BranchId).ToArrayAsync(ct))
                {
                    var scoped = await permissions.EvaluateBranchAsync(Permission, id, ct);
                    if (scoped.Decision == PermissionDecision.Unavailable) return TurnoverHistoryStatus.Unavailable;
                    if (scoped.IsGranted) return null;
                }
            return TurnoverHistoryStatus.Forbidden;
        }
    }

    private static int? SqlNumber(Exception error)
    { for (Exception? e = error; e != null; e = e.InnerException) if (e is SqlException sql) return sql.Number; return null; }
}
