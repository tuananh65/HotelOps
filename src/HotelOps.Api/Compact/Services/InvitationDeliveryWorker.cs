using System.Security.Cryptography;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed class InvitationDeliveryWorker(IServiceScopeFactory scopes,TimeProvider clock,ILogger<InvitationDeliveryWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(10),clock);
        do {try {await DeliverNext(stoppingToken);}catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception){logger.LogWarning("Invitation delivery worker failed; a later tick will retry. No message content was logged.");}}
        while(await timer.WaitForNextTickAsync(stoppingToken));
    }
    public async Task<bool> DeliverNext(CancellationToken ct)
    {
        await using var scope=scopes.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HotelOpsCompactDbContext>();
        var email=scope.ServiceProvider.GetRequiredService<IInvitationEmail>();
        if(email.ConfigurationError!=null)return false;
        var now=clock.GetUtcNow().UtcDateTime;AccountInvitation? invitation;User? user;
        var lease=Guid.NewGuid();
        await using(var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct)){
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource='HotelOps.InvitationDelivery',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;IF @r<0 THROW 51991,'Invitation delivery lock unavailable.',1;",ct);
            invitation=await db.AccountInvitations.Where(x=>x.DeliveryStatus!="SENT"&&x.RevokedAt==null&&x.UsedAt==null&&x.ExpiresAt>now&&x.Attempts<5&&x.NextAttemptAt<=now&&(x.LeaseUntil==null||x.LeaseUntil<=now))
                .OrderBy(x=>x.InvitationId).FirstOrDefaultAsync(ct);
            if(invitation==null)return false;
            user=await db.Users.AsNoTracking().SingleAsync(x=>x.UserId==invitation.UserId,ct);
            if(user.DeletedAt!=null||user.AccountStatus!="ACTIVE"||user.EmploymentStatus!="ACTIVE"
                ||!CryptographicOperations.FixedTimeEquals(AccountInvitationService.Fingerprint(user),invitation.PasswordFingerprint)){
                invitation.RevokedAt=now;invitation.ProtectedToken=null;await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return false;
            }
            invitation.LeaseId=lease;invitation.LeaseUntil=now.AddMinutes(2);invitation.Attempts++;
            await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
        }
        string? failure=null;
        try {
            var protector=scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("HotelOps.Compact.Invitation.v1");
            var token=protector.Unprotect(invitation.ProtectedToken??throw new CryptographicException());
            await email.Send(invitation.InvitationId,invitation.Recipient,user.Username,token,invitation.ExpiresAt,ct);
        }catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(CryptographicException){failure="INVITATION_KEY_UNAVAILABLE";}
        catch(Exception){failure="EMAIL_DELIVERY_FAILED";}
        // A resend/setup may revoke this row while SMTP is in progress. Never revive its token.
        db.ChangeTracker.Clear();
        var current=await db.AccountInvitations.SingleAsync(x=>x.InvitationId==invitation.InvitationId,ct);
        if(current.LeaseId!=lease)return false;
        current.DeliveryStatus=failure==null?"SENT":"FAILED";current.LastError=failure;current.SentAt=failure==null?clock.GetUtcNow().UtcDateTime:null;
        current.LeaseUntil=null;current.LeaseId=null;current.NextAttemptAt=clock.GetUtcNow().UtcDateTime.AddMinutes(Math.Min(60,Math.Pow(2,current.Attempts)));
        if(failure==null)current.ProtectedToken=null;
        await db.SaveChangesAsync(ct);return failure==null;
    }
}
