using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class SprintAssignedReworkProbe
{
    internal static async Task VerifyAsync(HotelOpsDbContext db,long cleaner,long branch,long rework,CancellationToken ct)
    {
        var passwords=new PasswordCredentialService(Options.Create(new PasswordHasherOptions
        {CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
        var secret=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))+"Aa1!";
        var user=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==cleaner,ct);
        var hash=passwords.HashPassword(user,secret);
        var sessions=new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System);
        SessionReference ticket;long sid;
        await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={cleaner}",ct);
            var verified=await new CredentialLockoutService(db,passwords,TimeProvider.System).VerifyAsync(cleaner,secret,
                (u,_)=>Task.FromResult(u.EmploymentStatus=="ACTIVE"&&u.AccountStatus=="ACTIVE"),ct);
            var login=await sessions.CreateSessionAsync(verified,ct);ticket=login.Ticket;sid=login.Session.SessionId;
            await tx.CommitAsync(ct);db.ChangeTracker.Clear();
        }
        var current=new Identity(new(cleaner,sid,false));
        var service=new RoomTypeCreationService(db,current,new PermissionService(current,new EfPermissionDataSource(db),new UnresolvedPermissionResourceResolver(),TimeProvider.System),sessions,TimeProvider.System);
        var inbox=new NotificationCenterService(db,current,TimeProvider.System,Options.Create(new NotificationCenterOptions()));
        var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.SourceType=="CLEANING_REWORK"&&n.SourceId==rework,ct);
        if((await inbox.ResolveSourceAsync(notice.NotificationId,ct)).Path!=$"/cleaning/reworks/{rework}?branchId={branch}")throw new Exception("Source routing mismatch");
        var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("frontend/scripts/sprintReworkInboxBrowser.mjs");
        using(var process=Process.Start(start)??throw new Exception("Inbox browser unavailable"))
        {
            try
            {
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username=user.Username,password=secret,branch,rework,notification=notice.NotificationId}).AsMemory(),ct);
                process.StandardInput.Close();
                var output=process.StandardOutput.ReadToEndAsync(ct);var error=process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);
                var result=JsonSerializer.Deserialize<JsonElement>(await output);await error;
                if(process.ExitCode!=0||!result.GetProperty("success").GetBoolean())
                    throw new Exception("Assignee inbox browser: "+result.GetRawText());
            }
            finally{if(!process.HasExited)process.Kill(true);}
        }
        await db.Database.ExecuteSqlRawAsync("""
            CREATE USER DEV3_AssignedReworkReader WITHOUT LOGIN;
            GRANT SELECT ON SCHEMA::dbo TO DEV3_AssignedReworkReader;
            GRANT VIEW DEFINITION TO DEV3_AssignedReworkReader;
            """,ct);
        try
        {
            await db.Database.OpenConnectionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("EXECUTE AS USER='DEV3_AssignedReworkReader'",ct);
            try
            {
                var result=await service.ReadAssignedReworkAsync(ticket,sid,branch,rework,null,ct);
                if(result.Status!=RoomTypeBatchStatus.Success||result.Value is not ReworkExecutionDto dto||dto.AssigneeId!=cleaner
                    ||dto.Capability!="READ_ONLY"||dto.SourceAuthority!="FE05_COMMITTED_V1"||dto.Defects.Count!=1)
                    throw new Exception("Assigned committed read failed: "+result.Status);
                if((await service.ReadAssignedReworkAsync(ticket,sid,branch,rework,null,ct,long.MaxValue)).Status!=RoomTypeBatchStatus.NotFound)
                    throw new Exception("Unrelated evidence disclosed");
                if((await service.ReadAssignedReworkAsync(ticket,sid,long.MaxValue,rework,null,ct)).Status==RoomTypeBatchStatus.Success)
                    throw new Exception("Wrong branch source disclosed");
            }
            finally{await db.Database.ExecuteSqlRawAsync("REVERT",ct);await db.Database.CloseConnectionAsync();}
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={cleaner}",ct);
            try
            {
                try{await inbox.ResolveSourceAsync(notice.NotificationId,ct);throw new Exception("Revoked source link disclosed");}
                catch(HotelOps.Api.Services.Insights.InsightException){}
                if((await service.ReadAssignedReworkAsync(ticket,sid,branch,rework,null,ct)).Status==RoomTypeBatchStatus.Success)
                    throw new Exception("Revoked reader disclosed source");
            }
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={cleaner}",ct);}
        }
        finally{await db.Database.ExecuteSqlRawAsync("DROP USER DEV3_AssignedReworkReader",ct);}
        await SprintReworkExecutionProbe.VerifyAsync(db,ticket,sid,branch,rework,ct);
    }
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;}}
}
