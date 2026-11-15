using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class SprintSubmissionNoticeSourceProbe
{
    // Nested only in the exact disposable, synthetic submission fixture; restore every fault in finally.
    internal static async Task OpenAsync(HotelOpsDbContext db,long user,long notice,long branch,long rework,CancellationToken ct,bool denied=false)
    {
        await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
        var sid=await fresh.UserSessions.AsNoTracking().Where(s=>s.UserId==user&&s.RevokedAt==null).MaxAsync(s=>s.SessionId,ct);
        try
        {
            var source=await new NotificationCenterService(fresh,new Identity(new(user,sid,false)),TimeProvider.System,Options.Create(new NotificationCenterOptions())).ResolveSourceAsync(notice,ct);
            if(denied||source.Path!=$"/inspections/reworks/{rework}/assign?branchId={branch}")throw new Exception("Submission source authorization/path mismatch");
        }
        catch(InsightException e) when(denied&&e.Status==404) { }
    }

    internal static async Task VerifyAsync(HotelOpsDbContext db,long user,long notice,long branch,long rework,long auditId,CancellationToken ct)
    {
        var audit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.AuditId==auditId,ct);
        var notification=await db.Notifications.AsNoTracking().SingleAsync(n=>n.NotificationId==notice,ct);
        await OpenAsync(db,user,notice,branch,rework,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={user} AND branch_id={branch}",ct);
        try{await OpenAsync(db,user,notice,branch,rework,ct,denied:true);}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={user} AND branch_id={branch}",CancellationToken.None);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={user}",ct);
        try{await OpenAsync(db,user,notice,branch,rework,ct,denied:true);}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=1 WHERE user_id={user}",CancellationToken.None);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values='[]' WHERE audit_id={auditId}",ct);
        try{await OpenAsync(db,user,notice,branch,rework,ct,denied:true);}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values={audit.NewValues} WHERE audit_id={auditId}",CancellationToken.None);}
        await db.Database.ExecuteSqlRawAsync("UPDATE dbo.fe04_rework_authority_issuers SET is_active=0 WHERE issuer='FE04.UC036' AND version=1",ct);
        try{await OpenAsync(db,user,notice,branch,rework,ct,denied:true);}
        finally{await db.Database.ExecuteSqlRawAsync("UPDATE dbo.fe04_rework_authority_issuers SET is_active=1 WHERE issuer='FE04.UC036' AND version=1",CancellationToken.None);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_recipients SET delivery_status='PENDING' WHERE notification_id={notice} AND user_id={user}",ct);
        try{await OpenAsync(db,user,notice,branch,rework,ct,denied:true);}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_recipients SET delivery_status='DELIVERED' WHERE notification_id={notice} AND user_id={user}",CancellationToken.None);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notifications SET created_by={user} WHERE notification_id={notice}",ct);
        try{await OpenAsync(db,user,notice,branch,rework,ct,denied:true);}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notifications SET created_by={notification.CreatedBy} WHERE notification_id={notice}",CancellationToken.None);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notifications SET action_url='https://untrusted.invalid/' WHERE notification_id={notice}",ct);
        try{await OpenAsync(db,user,notice,branch,rework,ct);}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notifications SET action_url={notification.ActionUrl} WHERE notification_id={notice}",CancellationToken.None);}
    }
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;}}
}
