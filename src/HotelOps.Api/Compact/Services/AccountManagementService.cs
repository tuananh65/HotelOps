using System.Net.Mail;
using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed class AccountManagementService(HotelOpsCompactDbContext db,Access access,Atomic atomic,
    Actor actor,Audit audit,IPasswordHasher<User> passwords,TimeProvider clock,IPasswordResetEmail email)
{
    public async Task<object> Detail(long id,CancellationToken ct)
    {
        await access.Require("USER_VIEW",ct:ct);
        var user=await db.Users.AsNoTracking().SingleOrDefaultAsync(u=>u.UserId==id&&u.DeletedAt==null,ct)
            ??throw new ApiException(404,"NOT_FOUND");
        var now=clock.GetUtcNow().UtcDateTime;
        var memberships=await (from m in db.Memberships.AsNoTracking()
            join b in db.Branches on m.BranchId equals b.BranchId
            join d in db.Departments on m.DepartmentId equals (long?)d.DepartmentId into departments
            from d in departments.DefaultIfEmpty()
            where m.UserId==id&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now)
            orderby b.Name,m.Kind
            select new{m.MembershipId,m.BranchId,branchName=b.Name,m.DepartmentId,departmentName=d==null?null:d.Name,
                m.Kind,m.IsPrimary,m.RowVersion}).ToArrayAsync(ct);
        var roles=await (from ur in db.UserRoles.AsNoTracking()
            join r in db.Roles on ur.RoleId equals r.RoleId
            join b in db.Branches on ur.BranchId equals (long?)b.BranchId into branches
            from b in branches.DefaultIfEmpty()
            where ur.UserId==id&&r.IsActive&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now)
            orderby r.Name
            select new{ur.UserRoleId,ur.RoleId,r.Name,ur.BranchId,branchName=b==null?null:b.Name,ur.ExpiresAt,ur.RowVersion}).ToArrayAsync(ct);
        return new{user=Output.Record(user),memberships,roles,emailConfigured=email.IsConfigured};
    }

    public Task<object> Update(long id,UpdateAccountRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.Require("USER_MANAGE",ct:ct);
        var user=await db.Users.SingleOrDefaultAsync(u=>u.UserId==id&&u.DeletedAt==null,ct)??throw new ApiException(404,"NOT_FOUND");
        Output.Version(user.RowVersion,request.RowVersion);
        var (name,address,phone)=await ValidateProfile(db,id,request.FullName,request.Email,request.Phone,ct);
        var before=new{user.FullName,user.Email,user.Phone};
        user.FullName=name;user.Email=address;user.Phone=phone;
        audit.Add("User",id,"UPDATE_PROFILE",before:before,after:new{user.FullName,user.Email,user.Phone});
        await db.SaveChangesAsync(ct);
        return (object)Output.Record(user);
    },ct);

    public async Task<object> PersonalProfile(CancellationToken ct)
    {
        var user=await db.Users.AsNoTracking().SingleOrDefaultAsync(u=>u.UserId==actor.Id&&u.DeletedAt==null,ct)
            ??throw new ApiException(404,"NOT_FOUND");
        return PersonalRecord(user);
    }

    public Task<object> UpdatePersonalProfile(UpdatePersonalProfileRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        var user=await db.Users.SingleOrDefaultAsync(u=>u.UserId==actor.Id&&u.DeletedAt==null,ct)
            ??throw new ApiException(404,"NOT_FOUND");
        // Never accept an id or role from the caller, even for administrators.
        Output.Version(user.RowVersion,request.RowVersion);
        var values=await ValidateProfile(db,user.UserId,request.FullName,request.Email,request.Phone,ct);
        var before=new{user.FullName,user.Email,user.Phone};
        user.FullName=values.Name;user.Email=values.Email;user.Phone=values.Phone;
        audit.Add("User",user.UserId,"UPDATE_PROFILE",before:before,after:new{user.FullName,user.Email,user.Phone});
        await db.SaveChangesAsync(ct);
        return PersonalRecord(user);
    },ct);

    private static object PersonalRecord(User user)=>new{user.UserId,user.EmployeeCode,user.Username,
        user.FullName,user.Email,user.Phone,user.RowVersion};

    internal static async Task<(string Name,string? Email,string? Phone)> ValidateProfile(
        HotelOpsCompactDbContext db,long? userId,string fullName,string? email,string? phone,CancellationToken ct)
    {
        var name=fullName.Trim();var address=NullIfBlank(email);var number=NullIfBlank(phone);
        Output.Require(name.Length is >0 and <=200,"INVALID_ACCOUNT_NAME",400);
        Output.Require(address==null||(address.Length<=254&&ValidEmail(address)),"INVALID_ACCOUNT_EMAIL",400);
        Output.Require(number==null||(number.Length<=30&&new PhoneAttribute().IsValid(number)),"INVALID_ACCOUNT_PHONE",400);
        Output.Require(address==null||!await db.Users.AnyAsync(u=>(userId==null||u.UserId!=userId)&&u.Email==address,ct),"EMAIL_ALREADY_USED",409);
        return (name,address,number);
    }

    public Task<object> ResetPassword(long id,ResetAccountPasswordRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.Require("USER_MANAGE",ct:ct);
        Output.Require(id!=actor.Id,"CANNOT_RESET_SELF",400);
        var admin=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==actor.Id,ct);
        Output.Require(admin.TemporaryPasswordExpiresAt==null,"PASSWORD_CHANGE_REQUIRED",403);
        Output.Require(admin.PasswordHash!=null&&passwords.VerifyHashedPassword(admin,admin.PasswordHash,request.AdminPassword)!=PasswordVerificationResult.Failed,
            "ADMIN_PASSWORD_INVALID",403);
        var user=await db.Users.SingleOrDefaultAsync(u=>u.UserId==id&&u.DeletedAt==null,ct)??throw new ApiException(404,"NOT_FOUND");
        Output.Version(user.RowVersion,request.RowVersion);Output.Reason(request.Reason);
        Output.Require(user.AccountStatus=="ACTIVE"&&user.EmploymentStatus=="ACTIVE","RESET_REQUIRES_ACTIVE_ACCOUNT",409);
        Output.Require(ValidEmail(user.Email),"RESET_EMAIL_REQUIRED",400);
        var gmail=GmailSender.From(request.GmailAddress,request.GmailAppPassword);
        Output.Require(gmail!=null||email.IsConfigured,"EMAIL_NOT_CONFIGURED",503);
        var now=clock.GetUtcNow().UtcDateTime;
        var cutoff=now.AddMinutes(-1);
        Output.Require(!await db.AuditLogs.AnyAsync(a=>a.EntityType=="User"&&a.EntityKey==id.ToString()&&a.Action=="PASSWORD_RESET_EMAIL"&&a.OccurredAt>cutoff,ct),
            "PASSWORD_RESET_COOLDOWN",429);
        var admins=await (from ur in db.UserRoles.AsNoTracking() join r in db.Roles on ur.RoleId equals r.RoleId
            join u in db.Users on ur.UserId equals u.UserId
            where r.Code=="SYS_ADMIN"&&r.IsActive&&ur.BranchId==null&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now)
                &&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null&&u.TemporaryPasswordExpiresAt==null
            select u.UserId).Distinct().ToArrayAsync(ct);
        Output.Require(!admins.Contains(id)||admins.Any(u=>u!=id),"LAST_ADMIN_PROTECTED");
        var temporary=TemporaryAccountPassword.Generate();var expiresAt=now.AddHours(24);
        user.PasswordHash=passwords.HashPassword(user,temporary);user.SecurityStamp=Guid.NewGuid();
        user.TemporaryPasswordExpiresAt=expiresAt;user.FailedLoginCount=0;user.LockedUntil=null;
        foreach(var session in await db.UserSessions.Where(s=>s.UserId==id&&s.RevokedAt==null).ToArrayAsync(ct))session.RevokedAt=now;
        audit.Add("User",id,"PASSWORD_RESET_EMAIL",after:new{request.Reason,expiresAt,delivery="SMTP"});
        // Persist/version-check before SMTP, but commit only after SMTP accepts the message.
        // SMTP is not transactional: a subsequent SQL commit failure still requires an explicit retry.
        await db.SaveChangesAsync(ct);
        await email.Send(user.Email!,user.Username,temporary,expiresAt,ct,gmail);
        return (object)new{sent=true,recipient=user.Email,expiresAt,requiresPasswordChange=true};
    },ct);

    private static string? NullIfBlank(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static bool ValidEmail(string? value)=>!string.IsNullOrWhiteSpace(value)&&MailAddress.TryCreate(value,out var address)&&address.Address==value;
}
