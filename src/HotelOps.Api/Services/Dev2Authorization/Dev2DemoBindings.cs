using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.Data;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Dev2Authorization;

/// <summary>Owner-approved demo slice. Never enables a production connection or changes shared RBAC.</summary>
public static partial class Dev2DemoBindings
{
    public static string PermissionCode(string decisionId) => ExpandedEnabled ? PermissionCodes(decisionId)[0] : decisionId switch
    {
        "A004A-016" => "ASSET_MANAGE", "A004A-017" => "ASSET_TRANSFER", "A004A-021" => "INCIDENT_CREATE",
        _ => throw new Dev2AccessDenied(403)
    };

    public static Task<PermissionEvaluation> EvaluateAsync(IPermissionService permissions, string decisionId,
        long branch, CancellationToken ct) => new Dev2PermissionGate(permissions).EvaluateAsync(
            (ExpandedEnabled ? PermissionCodes(decisionId) : new[] { PermissionCode(decisionId) }).Select(code => new Dev2PermissionRequirement(code, new(Dev2PermissionTargetKind.Branch, branch))).ToArray(), Dev2PermissionCombination.All, ct);

    internal static bool Eligible(HttpContext http)
    {
        if (!ExpandedEnabled && !string.Equals(Environment.GetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1"), Dev2DemoPolicyIdentity.Sha256, StringComparison.Ordinal)) return false;
        try
        {
            var db = http.RequestServices.GetService<HotelOpsDbContext>();
            if (db is null) return false;
            var connection = db.Database.GetDbConnection();
            if (ExpandedEnabled && connection.DataSource is not ("KIENPC" or "lpc:KIENPC")) return false;
            return string.Equals(connection.Database, Dev2DemoPolicyIdentity.Database, StringComparison.Ordinal)
                && connection.DataSource is "KIENPC" or "KIENPC\\SQLEXPRESS" or "lpc:KIENPC" or "lpc:KIENPC\\SQLEXPRESS";
        }
        catch (InvalidOperationException) { return false; }
    }

    internal static Dev2ActionContract? Find(Dev2KnownAction identity, HttpContext http)
    {
        if (ExpandedEnabled) return FindExpanded(identity, http);
        if (identity.DecisionId is not ("A004A-016" or "A004A-017" or "A004A-021") || !Eligible(http)) return null;
        return new(identity.DecisionId, Dev2DemoPolicyIdentity.Version, Dev2DemoPolicyIdentity.Sha256,
            async (action, ct) => [new(PermissionCode(identity.DecisionId), new(Dev2PermissionTargetKind.Branch, await BranchAsync(action, ct)))],
            Dev2PermissionCombination.All, ValidateAsync,
            (action, invocation) => invocation.Controller + "Controller" == identity.ControllerType.Name
                && invocation.Action == identity.ActionName
                && invocation.RootId == (identity.DecisionId == "A004A-021" ? null : ArgumentId(action))
                && ReferenceEquals(invocation.Input, Input(action)),
            null, null, TransactionAsync);
    }

    private static object Input(Dev2BoundAction action) => action.Arguments.TryGetValue("request", out var input)
        && input is not null ? input : throw new Dev2AccessDenied(403);
    private static long? Number(object input, string property) => input.GetType().GetProperty(property)?.GetValue(input) is long n ? n : null;
    private static long ArgumentId(Dev2BoundAction action) => action.Arguments.TryGetValue("id", out var id)
        && id is long n && n > 0 ? n : throw new Dev2AccessDenied(403);
    private static HotelOpsDbContext Db(Dev2BoundAction action) => action.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();

    private static async Task VerifyDatabaseAsync(HotelOpsDbContext db, CancellationToken ct)
    {
        var verified = await db.Database.SqlQueryRaw<int>("SELECT CAST(CASE WHEN DB_NAME()=N'HotelOps_DEV2_FullStack_Trial' AND CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=N'KIENPC' THEN 1 ELSE 0 END AS int) AS [Value]").SingleAsync(ct);
        if (verified != 1) throw new Dev2AccessDenied(503);
        if (ExpandedEnabled)
        {
            var identity = await db.Database.SqlQueryRaw<int>("SELECT CAST(CASE WHEN SERVERPROPERTY('InstanceName') IS NULL AND database_guid=CONVERT(uniqueidentifier,'6E68F686-D7C1-4002-94D9-A58FA73175DA') THEN 1 ELSE 0 END AS int) AS [Value] FROM sys.database_recovery_status WHERE database_id=DB_ID()").SingleAsync(ct);
            if (identity != 1) throw new Dev2AccessDenied(503);
        }
    }

    internal static async Task PreBindAsync(Dev2KnownAction identity, HttpContext http, CancellationToken ct)
    {
        if (!Eligible(http)) throw new Dev2AccessDenied(403);
        var db = http.RequestServices.GetRequiredService<HotelOpsDbContext>();
        await VerifyDatabaseAsync(db, ct);
        var current = http.RequestServices.GetRequiredService<ICurrentUserAccessor>();
        if (!current.TryGetCurrentUser(out var actor) || actor.IsRestricted) throw new Dev2AccessDenied(401);
        var branches = await db.UserBranchAccesses.AsNoTracking().Where(x => x.UserId == actor.UserId
            && x.RevokedAt == null && x.Branch.IsActive).Select(x => x.BranchId).Distinct().ToListAsync(ct);
        var permissions = http.RequestServices.GetRequiredService<IPermissionService>();
        foreach (var branch in branches)
        {
            var result = await EvaluateAsync(permissions, identity.DecisionId, branch, ct);
            if (result.Decision == PermissionDecision.Unavailable) throw new Dev2AccessDenied(503);
            if (result.IsGranted) return;
        }
        throw new Dev2AccessDenied(403);
    }

    private static async Task<long> BranchAsync(Dev2BoundAction action, CancellationToken ct)
    {
        if (ExpandedEnabled) return await ExpandedBranchAsync(action, ct);
        if (action.Identity.DecisionId == "A004A-021") return Number(Input(action), "BranchId") is > 0 and var branch ? branch : throw new Dev2AccessDenied(403);
        return await Db(action).Assets.AsNoTracking().Where(x => x.AssetId == ArgumentId(action)).Select(x => (long?)x.BranchId).SingleOrDefaultAsync(ct)
            ?? throw new Dev2AccessDenied(403);
    }

    private static async Task<bool> ValidateAsync(Dev2BoundAction action, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (!Eligible(action.Http)) return false;
        var db = Db(action); await VerifyDatabaseAsync(db, ct);
        var input = Input(action);
        var claim = Number(input, action.Identity.DecisionId == "A004A-021" ? "ReportedBy" : "ActorUserId");
        if (claim != action.Actor.UserId) return false;
        var branch = await BranchAsync(action, ct);
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(x => x.UserId == action.Actor.UserId && x.BranchId == branch
            && x.RevokedAt == null && x.Branch.IsActive, ct)) return false;
        var roomId = Number(input, "RoomId"); var areaId = Number(input, "AreaId");
        if (roomId is long room && !await db.Rooms.AsNoTracking().AnyAsync(x => x.RoomId == room && x.IsActive
            && x.BranchId == branch && x.Floor.IsActive && x.Floor.BranchId == branch
            && (x.AreaId == null || x.Area != null && x.Area.IsActive && x.Area.BranchId == branch)
            && (areaId == null || x.AreaId == areaId), ct)) return false;
        if (areaId is long area && !await db.Areas.AsNoTracking().AnyAsync(x => x.AreaId == area && x.IsActive && x.BranchId == branch, ct)) return false;
        if (Number(input, "AssetId") is long asset && !await db.Assets.AsNoTracking().AnyAsync(x => x.AssetId == asset && x.BranchId == branch
            && (x.LifecycleStatus == "IN_USE" || x.LifecycleStatus == "UNDER_REPAIR") && (roomId == null || x.CurrentRoomId == roomId) && (areaId == null || x.CurrentAreaId == areaId), ct)) return false;
        return true; // Existing service retains physical-source/state/concurrency validation.
    }

    internal static async Task LockAuthorityAsync(Dev2BoundAction action, CancellationToken ct)
    {
        var db = Db(action);
        if (db.Database.CurrentTransaction is null || !Eligible(action.Http)) throw new Dev2AccessDenied(403);
        if (!HotelOpsCookieTicket.TryRead(action.Http.User, out var ticket, out var sid) || ticket is null
            || ticket.UserId != action.Actor.UserId || sid != action.Actor.SessionId) throw new Dev2AccessDenied(401);
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM dbo.users WITH(UPDLOCK,HOLDLOCK) WHERE user_id={action.Actor.UserId}").AsNoTracking().SingleOrDefaultAsync(ct);
        var session = await db.UserSessions.FromSqlInterpolated($"SELECT * FROM dbo.user_sessions WITH(UPDLOCK,HOLDLOCK) WHERE user_id={action.Actor.UserId} AND session_key_hash={ticket.KeyHash}").AsNoTracking().SingleOrDefaultAsync(ct);
        if (user is null || session is null || session.SessionId != sid
            || SqlSessionPolicy.Validate(user, session, ticket, DateTime.UtcNow) is null) throw new Dev2AccessDenied(401);
        // Hold the actor's actual grant paths, role/permission activity and branch assignment until commit.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT user_role_id FROM dbo.user_roles WITH(UPDLOCK,HOLDLOCK) WHERE user_id={action.Actor.UserId}; SELECT role_id FROM dbo.roles WITH(UPDLOCK,HOLDLOCK) WHERE role_id IN(SELECT role_id FROM dbo.user_roles WHERE user_id={action.Actor.UserId}); SELECT role_permission_id FROM dbo.role_permissions WITH(UPDLOCK,HOLDLOCK) WHERE role_id IN(SELECT role_id FROM dbo.user_roles WHERE user_id={action.Actor.UserId}); SELECT permission_id FROM dbo.permissions WITH(UPDLOCK,HOLDLOCK) WHERE permission_id IN(SELECT permission_id FROM dbo.role_permissions WHERE role_id IN(SELECT role_id FROM dbo.user_roles WHERE user_id={action.Actor.UserId})); SELECT access_id FROM dbo.user_branch_access WITH(UPDLOCK,HOLDLOCK) WHERE user_id={action.Actor.UserId};", ct);
        var branch = await BranchAsync(action, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT branch_id FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={branch};", ct);
    }

    private static async Task<bool> TransactionAsync(Dev2BoundAction action, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        await LockAuthorityAsync(action, ct);
        if (ExpandedEnabled)
            return await ValidateExpandedAsync(action, phase, ct)
                && (await EvaluateAsync(action.Http.RequestServices.GetRequiredService<IPermissionService>(), action.Identity.DecisionId, await BranchAsync(action, ct), ct)).IsGranted;
        return await ValidateAsync(action, phase, ct)
            && (await EvaluateAsync(action.Http.RequestServices.GetRequiredService<IPermissionService>(), action.Identity.DecisionId, await BranchAsync(action, ct), ct)).IsGranted;
    }
}
