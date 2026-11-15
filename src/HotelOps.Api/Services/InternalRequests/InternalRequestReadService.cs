using System.ComponentModel.DataAnnotations;
using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

public sealed class InternalRequestReadException(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed record InternalRequestFilterValue(long Id, string Name);
public sealed record InternalRequestFilterOptions(string[] Statuses, InternalRequestFilterValue[] Types,
    InternalRequestFilterValue[] Priorities, InternalRequestFilterValue[] Departments);

public sealed class InternalRequestReadService(HotelOpsDbContext db, InternalRequestReadAccess access,
    ISqlSessionService sessions, ICurrentUserAccessor current, TimeProvider clock)
{
    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
        var valid = await sessions.ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
    }

    private async Task<T> ReadAsync<T>(SessionReference ticket, long sid, long? branch,
        Func<IQueryable<InternalRequest>, DateTime, Task<T>> read, CancellationToken ct)
    {
        if (branch is <= 0) throw new ValidationException("Invalid branch identifier.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var scope = await access.ReadAsync(ticket.UserId, now, ct);
        if (!scope.Global && scope.RelatedBranches.Length == 0)
            throw new InternalRequestReadException(403, "IR_VIEW_REQUIRED");
        if (branch is {} branchId && (!scope.Global && !scope.RelatedBranches.Contains(branchId)
            || !await db.Branches.AsNoTracking().AnyAsync(b => b.BranchId == branchId && b.IsActive, ct)))
            throw new InternalRequestReadException(403, "BRANCH_SCOPE");
        var query = scope.Apply(db.InternalRequests.AsNoTracking(), now);
        if (branch is {} selected) query = query.Where(r => r.BranchId == selected);
        var result = await read(query, now);
        await SessionAsync(ticket, sid, ct);
        var finalScope = await access.ReadAsync(ticket.UserId, clock.GetUtcNow().UtcDateTime, ct);
        if (scope.Global != finalScope.Global || !scope.Branches.SequenceEqual(finalScope.Branches)
            || !scope.RelatedBranches.SequenceEqual(finalScope.RelatedBranches))
            throw new InternalRequestReadException(403, "IR_SCOPE_CHANGED");
        await tx.CommitAsync(ct);
        return result;
    }

    public Task<InternalRequestHistoryPage> ListAsync(SessionReference ticket, long sid, long? branch,
        InternalRequestHistoryFilter filter, CancellationToken ct) => ReadAsync(ticket, sid, branch,
            (query, now) => InternalRequestHistoryQueries.ListAsync(query, filter, now, ct), ct);

    public Task<InternalRequestFilterOptions> OptionsAsync(SessionReference ticket, long sid, CancellationToken ct) =>
        ReadAsync(ticket, sid, null, async (query, _) => new InternalRequestFilterOptions(
            await query.Select(r => r.Status).Distinct().OrderBy(s => s).ToArrayAsync(ct),
            await query.Select(r => new { Id=r.RequestTypeId, Name=r.RequestType.TypeName })
                .Distinct().OrderBy(v => v.Name).ThenBy(v => v.Id).Select(v => new InternalRequestFilterValue(v.Id,v.Name)).ToArrayAsync(ct),
            await query.Where(r => r.PriorityId != null).Select(r => new { Id=r.PriorityId!.Value, Name=r.Priority!.DisplayName })
                .Distinct().OrderBy(v => v.Name).ThenBy(v => v.Id).Select(v => new InternalRequestFilterValue(v.Id,v.Name)).ToArrayAsync(ct),
            await query.Where(r => r.ReceivingDepartmentId != null).Select(r => new { Id=r.ReceivingDepartmentId!.Value, Name=r.ReceivingDepartment!.DepartmentName })
                .Distinct().OrderBy(v => v.Name).ThenBy(v => v.Id).Select(v => new InternalRequestFilterValue(v.Id,v.Name)).ToArrayAsync(ct)), ct);

    public Task<InternalRequestTimeline?> TimelineAsync(SessionReference ticket, long sid, long id,
        InternalRequestTimelineQuery pagination, CancellationToken ct) => ReadAsync(ticket, sid, null,
            (query, _) => InternalRequestHistoryQueries.TimelineAsync(query, id, pagination, ct), ct);
}
