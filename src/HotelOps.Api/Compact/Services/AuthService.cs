using System.Security.Cryptography;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed class AuthService(HotelOpsCompactDbContext db,Atomic atomic,Actor actor,Audit audit,
    IPasswordHasher<User> passwords,TimeProvider clock)
{
    private static readonly User Dummy=new(){Username="unavailable"};
    private static readonly string DummyHash=new PasswordHasher<User>().HashPassword(Dummy,Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    public async Task<object> Login(LoginRequest request,CancellationToken ct)
    {
        var outcome=await atomic.Run(async()=>{
            var now=clock.GetUtcNow().UtcDateTime;
            var identifier=request.Username.Trim();
            // Owner decision 25 Nov: login uses Username only, never EmployeeCode aliases.
            var user=await db.Users.SingleOrDefaultAsync(u=>u.Username==identifier,ct);
            var result=passwords.VerifyHashedPassword(user??Dummy,user?.PasswordHash??DummyHash,request.Password);
            if(user==null || user.AccountStatus!="ACTIVE" || user.EmploymentStatus!="ACTIVE" || user.DeletedAt!=null)
                return (object)new LoginFailure(401,"INVALID_CREDENTIALS");
            if(user.LockedUntil>now)return new LoginFailure(423,"ACCOUNT_LOCKED");
            if(result==PasswordVerificationResult.Failed){
                user.FailedLoginCount++;
                if(user.FailedLoginCount>=5){user.LockedUntil=now.AddMinutes(15);user.FailedLoginCount=0;}
                return new LoginFailure(401,"INVALID_CREDENTIALS");
            }
            if(user.TemporaryPasswordExpiresAt<=now)return new LoginFailure(403,"TEMPORARY_PASSWORD_EXPIRED");
            user.FailedLoginCount=0;user.LockedUntil=null;user.LastLoginAt=now;
            if(result==PasswordVerificationResult.SuccessRehashNeeded)user.PasswordHash=passwords.HashPassword(user,request.Password);
            var token=RandomNumberGenerator.GetBytes(32);
            var session=new UserSession{UserId=user.UserId,TokenHash=SHA256.HashData(token),SecurityStamp=user.SecurityStamp,
                CreatedAt=now,ExpiresAt=now.AddHours(8)};
            db.UserSessions.Add(session);await db.SaveChangesAsync(ct);
            return new {accessToken=Convert.ToHexString(token).ToLowerInvariant(),tokenType="Bearer",session.ExpiresAt,
                requiresPasswordChange=user.TemporaryPasswordExpiresAt!=null,user=Output.Record(user)};
        },ct);
        // Commit lockout counters even when authentication fails.
        if(outcome is LoginFailure failure)throw new ApiException(failure.Status,failure.Code);
        return outcome;
    }
    public async Task<object> Me(CancellationToken ct)
    {
        var user=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==actor.Id,ct);
        var now=clock.GetUtcNow().UtcDateTime;
        var grants=await (from ur in db.UserRoles.AsNoTracking() join role in db.Roles on ur.RoleId equals role.RoleId
            join grant in db.RolePermissions on role.RoleId equals grant.RoleId join p in db.Permissions on grant.PermissionId equals p.PermissionId
            where ur.UserId==actor.Id && ur.AssignedAt<=now && ur.RevokedAt==null && (ur.ExpiresAt==null||ur.ExpiresAt>now) && role.IsActive && p.IsActive
                && !(role.Code=="HOUSEKEEPING_SUPERVISOR" && p.Code=="ROOM_VIEW")
            select new {p.Code,grant.DataScope,ur.BranchId}).Distinct().ToArrayAsync(ct);
        var global=grants.Any(g=>g.BranchId==null&&g.DataScope=="ALL");
        var branches=await db.Branches.AsNoTracking().Where(b=>b.IsActive&&(global||db.Memberships.Any(m=>m.UserId==actor.Id&&m.BranchId==b.BranchId&&m.Kind=="BRANCH_ACCESS"&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))))
            .OrderBy(b=>b.Name).Select(b=>new {b.BranchId,b.Code,b.Name,b.Timezone}).ToArrayAsync(ct);
        var memberships=await db.Memberships.AsNoTracking().Where(m=>m.UserId==actor.Id&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))
            .Select(m=>new {m.BranchId,m.DepartmentId,m.Kind}).ToArrayAsync(ct);
        var roles=await (from ur in db.UserRoles.AsNoTracking() join r in db.Roles on ur.RoleId equals r.RoleId where ur.UserId==actor.Id&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now)&&r.IsActive select new {r.Code,r.Name,ur.BranchId}).ToArrayAsync(ct);
        return new {user=Output.Record(user),grants=grants.Where(g=>g.BranchId==null||branches.Any(b=>b.BranchId==g.BranchId)),branches,roles,memberships,requiresPasswordChange=user.TemporaryPasswordExpiresAt!=null};
    }
    public Task<object> Logout(CancellationToken ct)=>atomic.Run(async()=>{
        var session=await db.UserSessions.SingleAsync(s=>s.SessionId==actor.SessionId && s.UserId==actor.Id,ct);
        session.RevokedAt=clock.GetUtcNow().UtcDateTime;return (object)new {revoked=true};},ct);
    public Task<object> ChangePassword(ChangeAccountPasswordRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        var user=await db.Users.SingleAsync(u=>u.UserId==actor.Id,ct);
        Output.Require(user.TemporaryPasswordExpiresAt==null||user.TemporaryPasswordExpiresAt>clock.GetUtcNow().UtcDateTime,"TEMPORARY_PASSWORD_EXPIRED",403);
        Output.Require(passwords.VerifyHashedPassword(user,user.PasswordHash!,request.CurrentPassword)!=PasswordVerificationResult.Failed,"INVALID_CURRENT_PASSWORD",400);
        Output.Require(request.CurrentPassword!=request.NewPassword,"PASSWORD_MUST_CHANGE",400);
        Output.Require(AccountPasswordPolicy.IsValid(request.NewPassword),"PASSWORD_POLICY_NOT_MET",400);
        user.PasswordHash=passwords.HashPassword(user,request.NewPassword);user.SecurityStamp=Guid.NewGuid();user.TemporaryPasswordExpiresAt=null;
        var now=clock.GetUtcNow().UtcDateTime;
        foreach(var s in await db.UserSessions.Where(s=>s.UserId==user.UserId && s.RevokedAt==null).ToArrayAsync(ct))s.RevokedAt=now;
        audit.Add("User",user.UserId,"PASSWORD_CHANGED");return (object)new {changed=true,signInRequired=true};},ct);
    private sealed record LoginFailure(int Status,string Code);
}
