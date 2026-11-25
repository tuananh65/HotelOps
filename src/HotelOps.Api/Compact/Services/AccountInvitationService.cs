using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed class AccountInvitationService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,
    IPasswordHasher<User> passwords,TimeProvider clock,IDataProtectionProvider protection)
{
    private IDataProtector Protector=>protection.CreateProtector("HotelOps.Compact.Invitation.v1");
    internal static byte[] Fingerprint(User user)=>SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash??""));
    internal async Task Queue(User user,CancellationToken ct)
    {
        Output.Require(db.Database.CurrentTransaction!=null,"INVITATION_TRANSACTION_REQUIRED");
        Output.Require(user.TemporaryPasswordExpiresAt!=null,"ACCOUNT_ALREADY_ACTIVATED",409);
        Output.Require(MailAddress.TryCreate(user.Email,out var email)&&email.Address==user.Email,"INVITATION_EMAIL_REQUIRED",400);
        var now=clock.GetUtcNow().UtcDateTime;
        foreach(var prior in await db.AccountInvitations.Where(x=>x.UserId==user.UserId&&x.UsedAt==null&&x.RevokedAt==null).ToArrayAsync(ct)){
            prior.RevokedAt=now;prior.ProtectedToken=null;
        }
        var raw=RandomNumberGenerator.GetBytes(32);
        var invitation=new AccountInvitation{UserId=user.UserId,CreatedBy=actor.Id,Recipient=user.Email!,TokenHash=SHA256.HashData(raw),
            PasswordFingerprint=Fingerprint(user),ProtectedToken=Protector.Protect(Convert.ToHexString(raw)),CreatedAt=now,ExpiresAt=now.AddHours(24),NextAttemptAt=now};
        db.AccountInvitations.Add(invitation);
        audit.Add("User",user.UserId,"INVITATION_QUEUED");await db.SaveChangesAsync(ct);
    }

    public async Task<object> Status(long userId,CancellationToken ct)
    {
        await access.Require("USER_VIEW",ct:ct);
        return await db.AccountInvitations.AsNoTracking().Where(x=>x.UserId==userId).OrderByDescending(x=>x.InvitationId).Take(10)
            .Select(x=>new{x.InvitationId,x.DeliveryStatus,x.Attempts,x.CreatedAt,x.ExpiresAt,x.SentAt,x.UsedAt,x.RevokedAt,x.LastError}).ToArrayAsync(ct);
    }
    public Task<object> Resend(long userId,ResendInvitationRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.Require("USER_MANAGE",ct:ct);
        var user=await db.Users.SingleOrDefaultAsync(x=>x.UserId==userId&&x.DeletedAt==null,ct)??throw new ApiException(404,"USER_NOT_FOUND");
        Output.Version(user.RowVersion,request.UserRowVersion);
        Output.Require(user.AccountStatus=="ACTIVE"&&user.EmploymentStatus=="ACTIVE","ACCOUNT_INACTIVE");
        Output.Require(await db.AccountInvitations.AnyAsync(x=>x.UserId==userId,ct),"INVITATION_NOT_PREVIOUSLY_ISSUED",409);
        await Queue(user,ct);db.Entry(user).Property(x=>x.FullName).IsModified=true;
        return (object)new{queued=true};
    },ct);

    public Task<object> Setup(SetupAccountPasswordRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        Output.Require(request.Token.Length==64&&request.Token.All(Uri.IsHexDigit),"INVITATION_INVALID_OR_EXPIRED",400);
        Output.Require(AccountPasswordPolicy.IsValid(request.NewPassword),"PASSWORD_POLICY_NOT_MET",400);
        var hash=SHA256.HashData(Convert.FromHexString(request.Token));var now=clock.GetUtcNow().UtcDateTime;
        var invitation=await db.AccountInvitations.SingleOrDefaultAsync(x=>x.TokenHash==hash&&x.UsedAt==null&&x.RevokedAt==null&&x.ExpiresAt>now,ct)
            ??throw new ApiException(400,"INVITATION_INVALID_OR_EXPIRED");
        var user=await db.Users.SingleAsync(x=>x.UserId==invitation.UserId,ct);
        Output.Require(user.AccountStatus=="ACTIVE"&&user.EmploymentStatus=="ACTIVE"&&user.DeletedAt==null
            &&CryptographicOperations.FixedTimeEquals(Fingerprint(user),invitation.PasswordFingerprint),"INVITATION_INVALID_OR_EXPIRED",400);
        Output.Require(passwords.VerifyHashedPassword(user,user.PasswordHash!,request.NewPassword)==PasswordVerificationResult.Failed,"PASSWORD_MUST_CHANGE",400);
        user.PasswordHash=passwords.HashPassword(user,request.NewPassword);user.TemporaryPasswordExpiresAt=null;user.SecurityStamp=Guid.NewGuid();user.FailedLoginCount=0;user.LockedUntil=null;
        invitation.UsedAt=now;invitation.ProtectedToken=null;
        foreach(var session in await db.UserSessions.Where(x=>x.UserId==user.UserId&&x.RevokedAt==null).ToArrayAsync(ct))session.RevokedAt=now;
        // Setup is anonymous; record the invitation owner without claiming an authenticated operator.
        db.AuditLogs.Add(new AuditLog{EntityType="User",EntityKey=user.UserId.ToString(),Action="INVITATION_PASSWORD_SET",OperationKey=Guid.NewGuid(),OccurredAt=now,AfterJson="{\"invitationUsed\":true}"});
        await db.SaveChangesAsync(ct);return (object)new{changed=true,signInRequired=true};
    },ct);

    // Reuse the single-use setup-token outbox. Requesting a link never changes credentials,
    // ends sessions, or revokes a still-valid link (an anonymous request must not lock out its owner).
    public Task<object> Forgot(ForgotAccountPasswordRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        var now=clock.GetUtcNow().UtcDateTime;
        var identifier=request.Username.Trim();
        // Owner decision 25 Nov: recovery follows the same username-only contract as login.
        var user=await db.Users.SingleOrDefaultAsync(x=>x.Username==identifier,ct);
        if(user!=null&&user.AccountStatus=="ACTIVE"&&user.EmploymentStatus=="ACTIVE"&&user.DeletedAt==null
            &&MailAddress.TryCreate(user.Email,out var email)&&email.Address==user.Email
            &&!await db.AccountInvitations.AnyAsync(x=>x.UserId==user.UserId&&x.CreatedAt>now.AddMinutes(-5),ct)){
            var raw=RandomNumberGenerator.GetBytes(32);
            db.AccountInvitations.Add(new AccountInvitation{UserId=user.UserId,CreatedBy=user.UserId,Recipient=user.Email!,
                TokenHash=SHA256.HashData(raw),PasswordFingerprint=Fingerprint(user),ProtectedToken=Protector.Protect(Convert.ToHexString(raw)),
                CreatedAt=now,ExpiresAt=now.AddMinutes(30),NextAttemptAt=now});
            // CreatedBy is the token owner required by the existing schema; no authenticated actor is asserted.
            db.AuditLogs.Add(new AuditLog{EntityType="User",EntityKey=user.UserId.ToString(),Action="PASSWORD_RECOVERY_REQUESTED",
                OperationKey=Guid.NewGuid(),OccurredAt=now,AfterJson="{\"anonymousRequest\":true}"});
            await db.SaveChangesAsync(ct);
        }
        return (object)new{message="Nếu tài khoản có email hợp lệ, hướng dẫn đặt lại mật khẩu sẽ được gửi đến email đã đăng ký."};
    },ct);
}
