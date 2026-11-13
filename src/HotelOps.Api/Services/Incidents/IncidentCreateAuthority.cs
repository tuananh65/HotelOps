using HotelOps.Api.Data;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

public sealed class IncidentAccessException(int status) : Exception("PLACEMENT_AUTHORIZATION_REQUIRED")
{
    public int StatusCode { get; } = status;
}

// DEV2 writer boundary only. Reuses existing cookie/session policy and permission catalog.
// Does not grant permissions, change shared security, own a transaction or touch sessions.
public sealed class IncidentCreateAuthority(HotelOpsDbContext db, ICurrentUserAccessor current,
    IHttpContextAccessor http, IPermissionService permissions, TimeProvider clock)
{
    public async Task<bool> CanCreateAsync(long branch, CancellationToken ct)
    {
        Actor(null);
        var result = await permissions.EvaluateBranchAsync("INCIDENT_CREATE", branch, ct);
        if (result.Decision == PermissionDecision.Unavailable) throw new IncidentAccessException(503);
        return result.IsGranted;
    }

    public long Actor(long? supplied)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId <= 0 || actor.SessionId <= 0
            || !HotelOpsCookieTicket.TryRead(http.HttpContext?.User, out var ticket, out var sid)
            || ticket is null || ticket.UserId != actor.UserId || sid != actor.SessionId)
            throw new IncidentAccessException(401);
        if (actor.IsRestricted || ticket.IsRestricted || supplied.HasValue && supplied != actor.UserId)
            throw new IncidentAccessException(403);
        return actor.UserId;
    }

    // Users are locked first by the writer. Reacquires only the SAME actor/session locks
    // at final verification; no new User after feature locks. No tracked stale identity.
    public async Task RequireAsync(long actorId, long branch, string permission, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Actor(actorId);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is null || branch <= 0)
            throw new InvalidOperationException("Placement caller transaction and branch required.");
        HotelOpsCookieTicket.TryRead(http.HttpContext!.User, out var ticket, out var sid);
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM dbo.users WITH(UPDLOCK,HOLDLOCK) WHERE user_id={actorId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var session = await db.UserSessions.FromSqlInterpolated($"SELECT * FROM dbo.user_sessions WITH(UPDLOCK,HOLDLOCK) WHERE user_id={actorId} AND session_key_hash={ticket!.KeyHash}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (user is null || user.EmploymentStatus != "ACTIVE" || session is null || session.SessionId != sid
            || SqlSessionPolicy.Validate(user, session, ticket!, clock.GetUtcNow().UtcDateTime) is null)
            throw new IncidentAccessException(401);
        if (!(await permissions.EvaluateBranchAsync(permission, branch, ct)).IsGranted)
            throw new IncidentAccessException(403);
        ct.ThrowIfCancellationRequested();
    }
}
