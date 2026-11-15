using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class SprintInternalRequestProgressScenario
{
    private sealed class Identity(CurrentUserInfo user):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo result){result=user;return true;}}

    internal static async Task RunAsync(HotelOpsDbContext db,long actor,long request,List<object> checks,CancellationToken ct)
    {
        // Parent has verified exact disposable identity and exclusively owned synthetic fixture.
        var original=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        var department=await db.Users.Where(u=>u.UserId==actor).Select(u=>u.DepartmentId).SingleAsync(ct);
        var credential=SessionCredential.Generate();var now=DateTime.UtcNow;
        var session=new UserSession{UserId=actor,SessionKeyHash=SessionCredential.Hash(credential),CreatedAt=now,ExpiresAt=now.AddMinutes(20)};
        var triggerInstalled=false;var schemaInstalled=false;
        void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(new{internalRequestProgressCheck=label});}
        Task<byte[]> Revision()=>db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.internal_requests WHERE internal_request_id={request}").SingleAsync(ct);
        try
        {
            foreach(var file in new[]{"InternalRequestDecisionIsolated.sql","InternalRequestProgressIsolated.sql","InternalRequestProgressIsolated.sql"})
                await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/"+file,ct),ct);
            schemaInstalled=true;
            db.UserSessions.Add(session);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            var stored=await db.UserSessions.AsNoTracking().SingleAsync(s=>s.SessionId==session.SessionId,ct);
            var ticket=SessionReference.FromProtectedTicket(actor,credential,false,DateTime.SpecifyKind(stored.CreatedAt,DateTimeKind.Utc));
            var service=new InternalRequestProgressService(db,new(db,new(db)),new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),new Identity(new(actor,session.SessionId,false)));
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"IN_PROGRESS")
                .SetProperty(r=>r.SubmittedAt,now.AddMinutes(-5)).SetProperty(r=>r.DueAt,now.AddDays(1)),ct);
            async Task Denied(InternalRequestProgressInput input,int status,string label)
            {
                var rejected=false;
                try{await service.UpdateAsync(ticket,session.SessionId,request,await Revision(),input,ct);}
                catch(InternalRequestReadException e)when(e.Status==status){rejected=true;}
                Check(rejected,label);
            }
            await service.UpdateAsync(ticket,session.SessionId,request,await Revision(),new(10,"Manager update",null,null,null),ct);
            Check(await db.InternalRequestEvents.AnyAsync(e=>e.InternalRequestId==request&&e.EventType=="PROGRESS",ct),"mapped manager can update with no handler inference");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={original.BranchId} AND department_id={original.ReceivingDepartmentId}",ct);
            await Denied(new(20,null,null,null,null),403,"unmapped non-handler denied despite IR_PROCESS");
            await db.Users.Where(u=>u.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,original.ReceivingDepartmentId),ct);
            db.InternalRequestAssignments.Add(new(){InternalRequestId=request,AssigneeId=actor,AssignedBy=actor,AssignedAt=now.AddMinutes(-1),Status="ACTIVE"});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            await service.UpdateAsync(ticket,session.SessionId,request,await Revision(),new(25,"Handler update","Partial result",null,null),ct);
            await Denied(new(30,null,null,DateTimeOffset.UtcNow.AddDays(2),"Deadline change"),403,"current handler without management mapping cannot change deadline");
            await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
            await Denied(new(30,null,null,null,null),403,"current assignment does not bypass revoked role");
            await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
            await db.Users.Where(u=>u.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,(long?)null),ct);
            await Denied(new(30,null,null,null,null),403,"handler outside receiving department denied");
            await db.Users.Where(u=>u.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,original.ReceivingDepartmentId),ct);
            await db.InternalRequestAssignments.Where(a=>a.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Status,"REASSIGNED").SetProperty(a=>a.EndedAt,DateTime.UtcNow),ct);
            await Denied(new(30,null,null,null,null),403,"former handler loses progress authority immediately");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={original.BranchId} AND department_id={original.ReceivingDepartmentId}",ct);
            var missingReason=false;
            try{await service.UpdateAsync(ticket,session.SessionId,request,await Revision(),new(30,null,null,DateTimeOffset.UtcNow.AddDays(2)," "),ct);}
            catch(ValidationException){missingReason=true;}
            Check(missingReason,"deadline requires nonblank reason");
            var stale=await Revision();
            await service.UpdateAsync(ticket,session.SessionId,request,stale,new(40,null,"Progress result",DateTimeOffset.UtcNow.AddDays(2),"Revised completion forecast"),ct);
            var staleDenied=false;try{await service.UpdateAsync(ticket,session.SessionId,request,stale,new(50,null,null,null,null),ct);}
            catch(InternalRequestReadException e)when(e.Status==412){staleDenied=true;}
            Check(staleDenied,"stale progress cannot overwrite current result");
            if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.triggers WHERE name='TR_DEV3_IRProgressRollback'").SingleAsync(ct)!=0)
                throw new Exception("Preserve existing fault trigger");
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_IRProgressRollback ON dbo.internal_request_progress_outbox AFTER INSERT AS THROW 51897,'Synthetic progress outbox failure',1;",ct);
            triggerInstalled=true;
            var before=await Revision();var events=await db.InternalRequestEvents.CountAsync(e=>e.InternalRequestId==request,ct);
            var fault=false;try{await service.UpdateAsync(ticket,session.SessionId,request,before,new(100,null,"Ready",null,null),ct);}
            catch(SqlException e)when(e.Number==51897){fault=true;}
            var afterFault=await Revision();
            Check(fault&&before.SequenceEqual(afterFault)&&events==await db.InternalRequestEvents.CountAsync(e=>e.InternalRequestId==request,ct)
                &&await db.Database.SqlQuery<int>($"SELECT progress_percent AS Value FROM dbo.internal_request_progress WHERE internal_request_id={request}").SingleAsync(ct)==40,
                "outbox fault rolls back snapshot event audit and request version");
            Check(await db.AuditLogs.CountAsync(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString(),ct)==events,"rollback preserves audit count");
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_IRProgressRollback",ct);triggerInstalled=false;
            await service.UpdateAsync(ticket,session.SessionId,request,await Revision(),new(100,"Ready for review","Complete result",null,null),ct);
            await service.UpdateAsync(ticket,session.SessionId,request,await Revision(),new(100,"Clarification","Complete result",null,null),ct);
            Check(await db.InternalRequests.AnyAsync(r=>r.InternalRequestId==request&&r.Status=="IN_PROGRESS"&&r.ClosedAt==null,ct),"100 percent never closes request");
            Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_progress_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request}").SingleAsync(ct)==1,
                "only transition to 100 creates requester notification intent");
            var raceVersion=await Revision();
            async Task<int> Race(string note)
            {
                await using var context=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
                var contender=new InternalRequestProgressService(context,new(context,new(context)),new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),new Identity(new(actor,session.SessionId,false)));
                try{await contender.UpdateAsync(ticket,session.SessionId,request,raceVersion,new(100,note,"Complete result",null,null),ct);return 200;}
                catch(InternalRequestReadException e){return e.Status;}
            }
            var race=await Task.WhenAll(Race("Concurrent A"),Race("Concurrent B"));
            Check(race.Order().SequenceEqual(new[]{200,412}),"concurrent progress updates commit once and reject stale contender");
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"CLOSED"),ct);
            await Denied(new(80,null,null,null,null),403,"closed request rejects progress");
        }
        finally
        {
            db.ChangeTracker.Clear();
            if(triggerInstalled)await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_IRProgressRollback",CancellationToken.None);
            if(schemaInstalled)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE o FROM dbo.internal_request_progress_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request}",CancellationToken.None);
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.internal_request_progress WHERE internal_request_id={request}",CancellationToken.None);
            }
            await db.InternalRequestAssignments.Where(a=>a.InternalRequestId==request).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequestEvents.Where(e=>e.InternalRequestId==request).ExecuteDeleteAsync(CancellationToken.None);
            await db.AuditLogs.Where(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString()).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,original.Status)
                .SetProperty(r=>r.SubmittedAt,original.SubmittedAt).SetProperty(r=>r.DueAt,original.DueAt).SetProperty(r=>r.UpdatedAt,original.UpdatedAt),CancellationToken.None);
            await db.Users.Where(u=>u.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,department),CancellationToken.None);
            await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={original.BranchId} AND department_id={original.ReceivingDepartmentId}",CancellationToken.None);
            await db.UserSessions.Where(s=>s.SessionId==session.SessionId&&s.UserId==actor).ExecuteDeleteAsync(CancellationToken.None);
            checks.Add(new{internalRequestProgressCleanup=true});
        }
    }
}
