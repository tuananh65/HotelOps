using HotelOps.Api.Data;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;
public enum CleaningNotificationCapability { Staging,Inbox,ContainedDelivery }
public interface ICleaningNotificationActivation { Task RequireAsync(CleaningNotificationCapability capability,CancellationToken ct); }
public sealed class CleaningNotificationDisabled:ICleaningNotificationActivation
{public Task RequireAsync(CleaningNotificationCapability capability,CancellationToken ct){ct.ThrowIfCancellationRequested();throw new NotificationContractUnavailableException();}}
public interface ICleaningActorAuthority
{
    Task RequireWriterAsync(CleaningAssignmentCommand command,CancellationToken ct);
    Task RequireViewerAsync(long userId,CancellationToken ct);
}
public sealed class CleaningActorAuthority(HotelOpsDbContext db,ICurrentUserAccessor current,IHttpContextAccessor http,
    ISqlSessionService sessions,IPermissionService permissions):ICleaningActorAuthority
{
    public async Task RequireWriterAsync(CleaningAssignmentCommand c,CancellationToken ct)
    {
        Fe05CallerTransaction.Require(db);CleaningAssignmentProfile.ValidateCommand(c);
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=c.ActorId||actor.SessionId<=0||actor.IsRestricted)
            throw new UnauthorizedAccessException();
        // Owner calls this before domain locks/mutation; subsequent calls reacquire retained locks.
        var users=await new UserEligibilityLock(db).LockAsync([c.ActorId,c.AssigneeId],ct);
        UserEligibilityLock.RequireEligible(c.ActorId,users);
        await RequireViewerAsync(c.ActorId,ct);
        if(!(await permissions.EvaluateBranchAsync("CLEANING_ASSIGN",c.BranchId,ct)).IsGranted)throw new UnauthorizedAccessException();
        if(!await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={c.BranchId}")
            .AsNoTracking().AnyAsync(b=>b.IsActive,ct))throw new UnauthorizedAccessException();
        await LockCatalogAsync(db,ct);
    }
    public async Task RequireViewerAsync(long userId,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=userId||actor.SessionId<=0||actor.IsRestricted||
            http.HttpContext is not {} context||!HotelOpsCookieTicket.TryRead(context.User,out var ticket,out var sid)||ticket is null||sid!=actor.SessionId)
            throw new UnauthorizedAccessException();
        var valid=await sessions.ValidateSessionAsync(ticket,ct);
        if(!valid.IsValid||valid.Session?.SessionId!=sid||valid.Session.UserId!=userId||valid.Session.IsRestricted)throw new UnauthorizedAccessException();
    }
    internal static Task LockCatalogAsync(HotelOpsDbContext db,CancellationToken ct)=>db.Database.ExecuteSqlRawAsync(
        "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:RoomTypes:v1',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=5000; IF @r<0 THROW 51961,'Cleaning notification source lock unavailable',1;",ct);
}
