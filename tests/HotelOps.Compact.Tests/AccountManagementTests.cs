using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class AccountDatabaseFactAttribute:FactAttribute
{
    public AccountDatabaseFactAttribute(){if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOTELOPS_ACCOUNT_TEST")))Skip="Requires an isolated HotelOpsCompactWorkdayTestAccount database.";}
}

public sealed class AccountManagementTests
{
    [Fact] public void TemporaryPasswordsAreRandomAndStrong()
    {
        var values=Enumerable.Range(0,100).Select(_=>TemporaryAccountPassword.Generate()).ToArray();
        Assert.Equal(100,values.Distinct().Count());
        foreach(var value in values){Assert.Equal(24,value.Length);Assert.Contains(value,char.IsUpper);Assert.Contains(value,char.IsLower);Assert.Contains(value,char.IsDigit);Assert.Contains(value,c=>!char.IsLetterOrDigit(c));}
    }
    [Fact] public async Task MissingMailConfigurationFailsClosed()
    {
        var sender=new PasswordResetEmail(new ConfigurationBuilder().Build());
        Assert.False(sender.IsConfigured);
        var error=await Assert.ThrowsAsync<ApiException>(()=>sender.Send("recipient@example.invalid","test",TemporaryAccountPassword.Generate(),DateTime.UtcNow.AddDays(1),CancellationToken.None));
        Assert.Equal("EMAIL_NOT_CONFIGURED",error.Code);
    }

    [AccountDatabaseFact] public async Task ManagementAndResetPreserveSecurityAndRollbackOnEmailFailure()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_ACCOUNT_TEST")!;
        Assert.Matches("^HotelOpsCompactWorkdayTestAccount[0-9]+$",new SqlConnectionStringBuilder(connection).InitialCatalog);
        await using var db=new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        var clock=TimeProvider.System;var hash=new PasswordHasher<User>();
        var admin=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.RoleId join u in db.Users on ur.UserId equals u.UserId
            where r.Code=="SYS_ADMIN"&&ur.RevokedAt==null&&u.AccountStatus=="ACTIVE" select u).FirstAsync();
        var adminPassword=TemporaryAccountPassword.Generate();admin.PasswordHash=hash.HashPassword(admin,adminPassword);admin.TemporaryPasswordExpiresAt=null;
        var adminSession=new UserSession{UserId=admin.UserId,TokenHash=RandomNumberGenerator.GetBytes(32),SecurityStamp=admin.SecurityStamp,CreatedAt=DateTime.UtcNow,ExpiresAt=DateTime.UtcNow.AddHours(1)};
        db.UserSessions.Add(adminSession);
        var target=new User{Username="qa_account_"+Guid.NewGuid().ToString("N")[..8],EmployeeCode="QA"+Guid.NewGuid().ToString("N")[..8],FullName="Account reset test",Email="qa-"+Guid.NewGuid().ToString("N")+"@example.invalid",SecurityStamp=Guid.NewGuid(),AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=DateTime.UtcNow};
        var oldPassword=TemporaryAccountPassword.Generate();target.PasswordHash=hash.HashPassword(target,oldPassword);db.Users.Add(target);await db.SaveChangesAsync();
        var targetSession=new UserSession{UserId=target.UserId,TokenHash=RandomNumberGenerator.GetBytes(32),SecurityStamp=target.SecurityStamp,CreatedAt=DateTime.UtcNow,ExpiresAt=DateTime.UtcNow.AddHours(1)};
        db.UserSessions.Add(targetSession);await db.SaveChangesAsync();
        var targetId=target.UserId;var oldStamp=target.SecurityStamp;var oldHash=target.PasswordHash;
        var accessor=new HttpContextAccessor{HttpContext=new DefaultHttpContext()};
        void SetActor(long id,long sessionId)=>accessor.HttpContext.User=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,id.ToString()),new("session_id",sessionId.ToString())],"test"));
        SetActor(admin.UserId,adminSession.SessionId);
        var actor=new Actor(accessor);var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);var mail=new FakeMail();
        var service=new AccountManagementService(db,access,atomic,actor,audit,hash,clock,mail);
        async Task<User> Reload(){db.ChangeTracker.Clear();return await db.Users.SingleAsync(u=>u.UserId==targetId);}
        ResetAccountPasswordRequest Request(User u,string? password=null)=>new(Convert.ToBase64String(u.RowVersion),password??adminPassword,"Verified employee password reset request");
        async Task Fail(string expected,Func<Task<object>> action){var error=await Assert.ThrowsAsync<ApiException>(action);Assert.Equal(expected,error.Code);}
        var detail=JsonSerializer.SerializeToElement(await service.Detail(targetId,default));Assert.True(detail.GetProperty("roles").GetArrayLength()==0);
        target=await Reload();
        await Fail("EMAIL_ALREADY_USED",()=>service.Update(targetId,new(Convert.ToBase64String(target.RowVersion),"Test",admin.Email,null),default));
        target=await Reload();
        await service.Update(targetId,new(Convert.ToBase64String(target.RowVersion),"Updated account",target.Email,"0901234567"),default);
        target=await Reload();Assert.Equal("Updated account",target.FullName);Assert.Equal("0901234567",target.Phone);
        await Fail("ADMIN_PASSWORD_INVALID",()=>service.ResetPassword(targetId,Request(target,"wrong-password"),default));
        target=await Reload();await Fail("STALE_ROW_VERSION",()=>service.ResetPassword(targetId,Request(target) with{RowVersion=Convert.ToBase64String(new byte[8])},default));
        target=await Reload();await Fail("CANNOT_RESET_SELF",()=>service.ResetPassword(admin.UserId,Request(target),default));
        mail.IsConfigured=false;target=await Reload();await Fail("EMAIL_NOT_CONFIGURED",()=>service.ResetPassword(targetId,Request(target),default));mail.IsConfigured=true;
        target=await Reload();target.AccountStatus="INACTIVE";await db.SaveChangesAsync();await Fail("RESET_REQUIRES_ACTIVE_ACCOUNT",()=>service.ResetPassword(targetId,Request(target),default));
        target=await Reload();target.AccountStatus="ACTIVE";var address=target.Email;target.Email=null;await db.SaveChangesAsync();await Fail("RESET_EMAIL_REQUIRED",()=>service.ResetPassword(targetId,Request(target),default));
        target=await Reload();target.Email=address;await db.SaveChangesAsync();
        mail.Fail=true;await Fail("PASSWORD_EMAIL_FAILED",()=>service.ResetPassword(targetId,Request(target),default));
        target=await Reload();Assert.True(oldHash==target.PasswordHash);Assert.Equal(oldStamp,target.SecurityStamp);
        Assert.Null((await db.UserSessions.AsNoTracking().SingleAsync(s=>s.SessionId==targetSession.SessionId)).RevokedAt);
        Assert.False(await db.AuditLogs.AnyAsync(a=>a.EntityKey==targetId.ToString()&&a.Action=="PASSWORD_RESET_EMAIL"));
        mail.Fail=false;var start=DateTime.UtcNow;var response=JsonSerializer.Serialize(await service.ResetPassword(targetId,Request(target),default));
        target=await Reload();Assert.NotNull(mail.Password);AssertSecretAbsent(response,mail.Password!);Assert.Equal(address,mail.Recipient);
        Assert.Equal(PasswordVerificationResult.Success,hash.VerifyHashedPassword(target,target.PasswordHash!,mail.Password!));
        Assert.Equal(PasswordVerificationResult.Failed,hash.VerifyHashedPassword(target,target.PasswordHash!,oldPassword));
        Assert.InRange(target.TemporaryPasswordExpiresAt!.Value,start.AddHours(23.99),DateTime.UtcNow.AddHours(24.01));
        Assert.NotEqual(oldStamp,target.SecurityStamp);Assert.NotNull((await db.UserSessions.AsNoTracking().SingleAsync(s=>s.SessionId==targetSession.SessionId)).RevokedAt);
        var resetAudit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.EntityKey==targetId.ToString()&&a.Action=="PASSWORD_RESET_EMAIL");AssertSecretAbsent(resetAudit.AfterJson!,mail.Password!);
        await Fail("PASSWORD_RESET_COOLDOWN",()=>service.ResetPassword(targetId,Request(target),default));
        target=await Reload();var auth=new AuthService(db,atomic,actor,audit,hash,clock);
        var login=JsonSerializer.SerializeToElement(await auth.Login(new(target.Username,mail.Password!),default));Assert.True(login.GetProperty("requiresPasswordChange").GetBoolean());
        var newSession=await db.UserSessions.AsNoTracking().Where(s=>s.UserId==targetId&&s.RevokedAt==null).SingleAsync();
        SetActor(targetId,newSession.SessionId);
        await Fail("PERMISSION_DENIED",()=>service.Detail(admin.UserId,default));
        await Fail("PERMISSION_DENIED",()=>service.Update(admin.UserId,new(Convert.ToBase64String(admin.RowVersion),"Denied",null,null),default));
        await Fail("PERMISSION_DENIED",()=>service.ResetPassword(admin.UserId,Request(target),default));
        var permanent=TemporaryAccountPassword.Generate();
        target=await Reload();target.TemporaryPasswordExpiresAt=DateTime.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();
        await Fail("TEMPORARY_PASSWORD_EXPIRED",()=>auth.Login(new(target.Username,mail.Password!),default));
        target=await Reload();await Fail("TEMPORARY_PASSWORD_EXPIRED",()=>auth.ChangePassword(new(mail.Password!,permanent),default));
        target=await Reload();target.TemporaryPasswordExpiresAt=DateTime.UtcNow.AddHours(24);await db.SaveChangesAsync();
        await auth.ChangePassword(new(mail.Password!,permanent),default);
        target=await Reload();Assert.Null(target.TemporaryPasswordExpiresAt);Assert.Equal(PasswordVerificationResult.Success,hash.VerifyHashedPassword(target,target.PasswordHash!,permanent));
        Assert.False(await db.UserSessions.AnyAsync(s=>s.UserId==targetId&&s.RevokedAt==null));
    }
    private static void AssertSecretAbsent(string output,string secret)
    {
        // Standard substring assertion failures print both inputs, including the password.
        if(output.Contains(secret,StringComparison.Ordinal))Assert.Fail("Credential appeared in output; sensitive values redacted.");
    }
    private sealed class FakeMail:IPasswordResetEmail
    {
        public bool IsConfigured{get;set;}=true;public bool Fail{get;set;}public string? Password;public string? Recipient;
        public Task Send(string recipient,string username,string password,DateTime expiresAt,CancellationToken ct,GmailSender? gmail=null)
        {if(Fail)throw new ApiException(502,"PASSWORD_EMAIL_FAILED");Password=password;Recipient=recipient;return Task.CompletedTask;}
    }
}
