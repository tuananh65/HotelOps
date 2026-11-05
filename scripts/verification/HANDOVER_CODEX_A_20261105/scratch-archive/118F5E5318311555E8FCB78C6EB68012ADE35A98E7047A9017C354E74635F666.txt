using System.Data.Common;
using System.Net;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Task65StructureConcurrency
{
    internal static List<object> Waits { get; }=[];
    internal static List<object> Failures { get; }=[];
    internal static async Task RunAsync(HotelOpsDbContext setup,Func<DbCommandInterceptor?,HotelOpsDbContext> open,
        Func<string,Task<CookieJar>> login,IReadOnlyDictionary<string,User> identities,long branch,long floor,long area,
        Func<HttpMethod,string,CookieJar,object?,string?,CancellationToken,Task<HttpResponseMessage>> send,Action<bool,string> check,CancellationToken ct)
    {
        var actor=identities["admin"].UserId;var jar=await login("admin");var other=await login("other");
        string Route(bool a,long id)=>$"/api/branches/{branch}/{(a?"areas":"floors")}/{id}";
        object Body(string code,string name)=>new{code,name,reason="Concurrent correction"};
        async Task<string> Tag(bool a,long id,CookieJar who){using var r=await send(HttpMethod.Get,Route(a,id),who,null,null,ct);check(r.StatusCode==HttpStatusCode.OK,"UC015 concurrency GET current ETag");return r.Headers.ETag!.Tag;}
        async Task<int> Spid(HotelOpsDbContext db){await db.Database.OpenConnectionAsync(ct);return await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);}
        async Task<int> Wait(string label,int owner,Task pending,string? tag=null)
        {
            for(var i=0;i<180&&!pending.IsCompleted;i++){
                var rows=await setup.Database.SqlQuery<WaitRow>($"SELECT CONVERT(int,r.session_id) AS SessionId,r.wait_type AS Kind,t.text AS Query FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.blocking_session_id={owner} AND r.wait_type LIKE 'LCK_M_%'").ToArrayAsync(ct);
                var hit=rows.FirstOrDefault(x=>tag==null||x.Query.Contains(tag,StringComparison.Ordinal));
                if(hit!=null){Waits.Add(new{label,owner,waiter=hit.SessionId,hit.Kind});return hit.SessionId;}await Task.Delay(35,ct);
            }if(pending.IsFaulted)await pending;throw new InvalidOperationException("No DMV wait: "+label);
        }
        var etag=await Tag(false,floor,jar);
        var race=await Task.WhenAll(send(HttpMethod.Put,Route(false,floor),jar,Body("T65_F","Race one"),etag,ct),send(HttpMethod.Put,Route(false,floor),other,Body("T65_F","Race two"),etag,ct));
        check(race.Select(x=>(int)x.StatusCode).Order().SequenceEqual(new[]{200,412}),"UC015 concurrent same-version HTTPS writes commit exactly one; loser412, no lost update");foreach(var r in race)r.Dispose();
        var second=new Floor{BranchId=branch,FloorCode="T65_RACE2",FloorName="Second race target",IsActive=true};setup.Add(second);await setup.SaveChangesAsync(ct);setup.ChangeTracker.Clear();
        var t1=await Tag(false,floor,jar);var t2=await Tag(false,second.FloorId,other);
        var duplicates=await Task.WhenAll(send(HttpMethod.Put,Route(false,floor),jar,Body("T65_DUPLICATE","Duplicate one"),t1,ct),send(HttpMethod.Put,Route(false,second.FloorId),other,Body("T65_DUPLICATE","Duplicate two"),t2,ct));
        check(duplicates.Select(x=>(int)x.StatusCode).Order().SequenceEqual(new[]{200,409}),"UC015 different-target duplicate-code race has one200/one409");foreach(var r in duplicates)r.Dispose();
        var createTag=await Tag(false,floor,jar);
        var createRace=await Task.WhenAll(send(HttpMethod.Put,Route(false,floor),jar,Body("T65_CREATE_RACE","Update versus create"),createTag,ct),
            send(HttpMethod.Post,$"/api/branches/{branch}/floors",other,new{code="T65_CREATE_RACE",name="Concurrent UC014"},null,ct));
        check((createRace[0].StatusCode==HttpStatusCode.OK&&createRace[1].StatusCode==HttpStatusCode.Conflict)
            ||(createRace[0].StatusCode==HttpStatusCode.Conflict&&createRace[1].StatusCode==HttpStatusCode.Created),"UC014/015 share branch namespace lock: create/update race cannot duplicate");foreach(var r in createRace)r.Dispose();
        foreach(var scenario in new[]{"stale-writer-first","permission","cancel"})
        {
            jar=await login("admin");var tag=await Tag(false,floor,jar);
            var old=await setup.Floors.AsNoTracking().SingleAsync(x=>x.FloorId==floor,ct);
            await using var holder=open(null);var owner=await Spid(holder);await using var tx=await holder.Database.BeginTransactionAsync(ct);
            await holder.Floors.Where(x=>x.FloorId==floor).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Description,scenario=="stale-writer-first"?"Authoritative SQL change":old.Description),ct);
            using var stop=CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pending=send(HttpMethod.Put,Route(false,floor),jar,Body(old.FloorCode,"After wait"),tag,stop.Token);
            Task? revoke=null;await using var revoker=open(null);
            try{
                var api=await Wait("https-"+scenario,owner,pending,"UC015:TargetFloor");
                if(scenario=="permission"){
                    revoke=Revoke();async Task Revoke(){await using var rtx=await revoker.Database.BeginTransactionAsync(ct);await new UserEligibilityLock(revoker).LockAsync([actor],ct);await revoker.UserRoles.Where(x=>x.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,false),ct);await rtx.CommitAsync(ct);}
                    await Wait("https-retained-User-grants",api,revoke);check(!revoke.IsCompleted,"UC015 permission revocation waits retained actor User before feature commit");
                }
                if(scenario=="cancel"){
                    stop.Cancel();var canceled=false;try{using var response=await pending;}catch(OperationCanceledException){canceled=true;}
                    var released=false;for(var i=0;i<180;i++){if(!await setup.Database.SqlQuery<int>($"SELECT CONVERT(int,request_session_id) AS [Value] FROM sys.dm_tran_locks WHERE request_session_id={api} AND resource_database_id=DB_ID() AND request_owner_type='TRANSACTION'").AnyAsync(ct)){released=true;break;}await Task.Delay(35,ct);}
                    check(canceled&&released,"UC015 actual HTTPS cancellation releases transaction before blocker release");await tx.RollbackAsync(ct);
                }else{
                    await tx.CommitAsync(ct);using var response=await pending;
                    check((int)response.StatusCode==(scenario=="stale-writer-first"?412:200),"UC015 authoritative reread after target wait: "+scenario);
                    if(revoke!=null){await revoke;using var denied=await send(HttpMethod.Put,Route(false,floor),jar,Body(old.FloorCode,"Forbidden"),response.Headers.ETag!.Tag,ct);check(denied.StatusCode==HttpStatusCode.Forbidden,"UC015 next HTTPS PUT observes committed permission revocation");}
                }
            }finally{
                stop.Cancel();if(holder.Database.CurrentTransaction!=null)await tx.RollbackAsync(CancellationToken.None);try{using var r=await pending;}catch{}if(revoke!=null)try{await revoke;}catch{}
                await setup.UserRoles.Where(x=>x.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,true),ct);
            }
        }
        // Persisted sessions and production accessor/permission/session/update services for
        // post-UPDATE fault schedules. No test hook exists in production code.
        var raw=SessionCredential.Generate();var now=DateTime.UtcNow;
        var row=new UserSession{UserId=actor,SessionKeyHash=SessionCredential.Hash(raw),CreatedAt=now,LastUsedAt=now,ExpiresAt=now.AddMinutes(30)};
        setup.Add(row);await setup.SaveChangesAsync(ct);setup.ChangeTracker.Clear();
        var ticket=SessionReference.FromProtectedTicket(actor,raw,false,now);var snapshot=new SessionSnapshot(row.SessionId,actor,now,row.ExpiresAt,now.AddHours(12),false);
        HotelStructureUpdateService Service(HotelOpsDbContext db){var context=new DefaultHttpContext{User=HotelOpsCookieTicket.Create(new(ticket,snapshot),TimeProvider.System).Principal};context.Items[typeof(SessionSnapshot)]=snapshot;
            var current=new CurrentUserAccessor(new Context{HttpContext=context});return new(db,current,new PermissionService(current,new EfPermissionDataSource(db),new UnresolvedPermissionResourceResolver(),TimeProvider.System),new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),TimeProvider.System);}
        async Task<StructureUpdateResult> Read(bool a,long id){await using var db=open(null);return await Service(db).ReadAsync(ticket,row.SessionId,branch,id,a,ct);}
        Task<StructureUpdateResult> Write(HotelOpsDbContext db,bool a,long id,string tag,string code,string name,CancellationToken token)=>a
            ?Service(db).UpdateAreaAsync(ticket,row.SessionId,branch,id,tag,new(code,name,"Verified fault/retry",null,"SQL test"),token)
            :Service(db).UpdateFloorAsync(ticket,row.SessionId,branch,id,tag,new(code,name,"Verified fault/retry","SQL test"),token);
        var rt=await setup.RoomTypes.Select(x=>x.RoomTypeId).FirstAsync(ct);
        // A parent writer that wins the lock is authoritatively reread by the real update.
        var parentBaseline=await Read(true,area);var parentId=parentBaseline.Value!.FloorId!.Value;
        await using(var holder=open(null))
        await using(var writer=open(null))
        {
            var owner=await Spid(holder);await using var tx=await holder.Database.BeginTransactionAsync(ct);
            await holder.Floors.Where(x=>x.FloorId==parentId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,false),ct);
            var pending=Service(writer).UpdateAreaAsync(ticket,row.SessionId,branch,area,parentBaseline.ETag!,new(parentBaseline.Value.Code,"Invalidated parent","Concurrent parent eligibility test",parentId),ct);
            try{await Wait("Area-parent-deactivate-first",owner,pending,"UC015:ParentFloor");await tx.CommitAsync(ct);
                check((await pending).Status==StructureUpdateStatus.InvalidParent,"UC015 Area waits then rejects newly inactive parent, no stale eligibility");}
            finally{if(holder.Database.CurrentTransaction!=null)await tx.RollbackAsync(CancellationToken.None);try{await pending;}catch{}await setup.Floors.Where(x=>x.FloorId==parentId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,true),ct);}
        }
        check((await Read(true,area)).ETag==parentBaseline.ETag,"UC015 parent eligibility rejection leaves Area revision untouched");
        foreach(var a in new[]{false,true})foreach(var scenario in new[]{"audit-failure","lock1222","timeout","deadlock1205","cancel-after-update","session-expiry","grant-expiry"})
        {
            var id=a?area:floor;var before=await Read(a,id);check(before.Status==StructureUpdateStatus.Success,"UC015 fault baseline read");
            var auditCount=await setup.AuditLogs.CountAsync(x=>x.EntityType==(a?"areas":"floors")&&x.EntityId==id.ToString(),ct);
            if(scenario=="session-expiry")await setup.UserSessions.Where(x=>x.SessionId==row.SessionId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UtcNow.AddSeconds(5)),ct);
            if(scenario=="grant-expiry")await setup.UserRoles.Where(x=>x.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UtcNow.AddSeconds(5)),ct);
            var probe=new Probe{FailAudit=scenario=="audit-failure",HoldAudit=scenario!="audit-failure",RoomType=rt};
            await using var writer=open(probe);var writerSpid=await Spid(writer);
            await writer.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY LOW",ct);
            if(scenario=="timeout")writer.Database.SetCommandTimeout(2);
            if(scenario=="lock1222")await writer.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 1200",ct);
            await using var holder=open(null);var owner=await Spid(holder);await holder.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY HIGH",ct);await using var tx=await holder.Database.BeginTransactionAsync(ct);
            if(scenario!="audit-failure")await holder.Database.SqlQuery<long>($"SELECT room_type_id AS [Value] FROM dbo.room_types WITH(XLOCK,HOLDLOCK) WHERE room_type_id={rt}").ToArrayAsync(ct);
            using var stop=CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pending=Write(writer,a,id,before.ETag!,before.Value!.Code,"Attempt "+scenario,stop.Token);Task? inverse=null;
            try{
                if(scenario!="audit-failure")await Wait(scenario+(a?"-Area":"-Floor"),owner,pending,"T65:AUDIT_WAIT");
                if(scenario=="deadlock1205"){inverse=new UserEligibilityLock(holder).LockAsync([actor],ct);await Wait("test-inverse-deadlock",writerSpid,inverse);}
                if(scenario is "session-expiry" or "grant-expiry"){await Task.Delay(6000,ct);await tx.RollbackAsync(ct);}
                if(scenario=="cancel-after-update")stop.Cancel();
                StructureUpdateResult? result=null;var caught=false;
                try{result=await pending;}catch(DbUpdateException e)when(scenario=="audit-failure"&&Number(e)==51065){caught=true;}catch(OperationCanceledException)when(scenario=="cancel-after-update"){caught=true;}
                if(inverse!=null)await inverse;
                if(holder.Database.CurrentTransaction!=null)await tx.RollbackAsync(ct);
                var expected=scenario=="lock1222"?1222:scenario=="timeout"?-2:scenario=="deadlock1205"?1205:51065;
                var valid=scenario is "audit-failure" or "cancel-after-update"?caught:scenario=="session-expiry"?result?.Status==StructureUpdateStatus.SessionInvalid:scenario=="grant-expiry"?result?.Status==StructureUpdateStatus.Forbidden:result?.Status==StructureUpdateStatus.Unavailable&&probe.Numbers.Contains(expected);
                check(valid&&probe.SawUpdate&&writer.Database.CurrentTransaction==null&&!writer.ChangeTracker.Entries().Any(),$"UC015 actual post-UPDATE {scenario} {(a?"Area":"Floor")} fails closed with clean transaction/tracker (caught={caught},status={result?.Status},update={probe.SawUpdate},sql={string.Join(',',probe.Numbers)},tx={writer.Database.CurrentTransaction!=null},tracked={writer.ChangeTracker.Entries().Count()})");
                Failures.Add(new{scenario,kind=a?"area":"floor",numbers=probe.Numbers,status=result?.Status.ToString(),probe.SawUpdate});
            }finally{
                stop.Cancel();if(holder.Database.CurrentTransaction!=null)await tx.RollbackAsync(CancellationToken.None);try{await pending;}catch{}if(inverse!=null)try{await inverse;}catch{}
                await setup.UserSessions.Where(x=>x.SessionId==row.SessionId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,row.ExpiresAt),ct);
                await setup.UserRoles.Where(x=>x.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,(DateTime?)null),ct);
            }
            var after=await Read(a,id);
            check(after.ETag==before.ETag&&after.Value==before.Value&&await setup.AuditLogs.CountAsync(x=>x.EntityType==(a?"areas":"floors")&&x.EntityId==id.ToString(),ct)==auditCount,"UC015 "+scenario+" rolls back complete row/audit/revision, including parent");
            await using var retry=open(null);var ok=await Write(retry,a,id,before.ETag!,before.Value.Code,"Explicit retry "+scenario,ct);
            check(ok.Status==StructureUpdateStatus.Success&&ok.ETag!=before.ETag,"UC015 fresh explicit retry commits once after "+scenario);
            await using var replay=open(null);check((await Write(replay,a,id,before.ETag!,before.Value.Code,"Stale replay",ct)).Status==StructureUpdateStatus.Stale,"UC015 old request cannot replay mutation after committed retry");
        }
        // An expired SQL session also rejects an actual production HTTPS PUT before mutation.
        jar=await login("admin");var last=await Tag(false,floor,jar);var sid=await setup.UserSessions.Where(x=>x.UserId==actor).MaxAsync(x=>x.SessionId,ct);
        await setup.UserSessions.Where(x=>x.SessionId==sid).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UtcNow.AddSeconds(-1)),ct);
        using var expired=await send(HttpMethod.Put,Route(false,floor),jar,Body("T65_F","Expired request"),last,ct);
        check(expired.StatusCode==HttpStatusCode.Unauthorized,"UC015 production HTTPS expired session rejects PUT401");
    }
    private sealed class Context:IHttpContextAccessor{public HttpContext? HttpContext{get;set;}}
    private sealed class WaitRow{public int SessionId{get;set;}public string Kind{get;set;}="";public string Query{get;set;}="";}
    private static int? Number(Exception e){for(Exception? x=e;x!=null;x=x.InnerException)if(x is SqlException sql)return sql.Number;return null;}
    private sealed class Probe:DbCommandInterceptor
    {
        internal bool FailAudit,HoldAudit,SawUpdate;internal long RoomType;internal List<int> Numbers{get;}=[];
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command,CommandExecutedEventData data,int result,CancellationToken ct=default)
        {if(command.CommandText.Contains("UPDATE")&&(command.CommandText.Contains("[floor_code]")||command.CommandText.Contains("[area_code]")))SawUpdate=true;return ValueTask.FromResult(result);}
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<DbDataReader> result,CancellationToken ct=default)
        {
            if(command.CommandText.Contains("INSERT INTO [audit_logs]")){
                if(FailAudit)command.CommandText=";THROW 51065,'Deliberate Task65 audit failure',1;";
                else if(HoldAudit)command.CommandText="/* T65:AUDIT_WAIT */ DECLARE @t65held bigint; SELECT @t65held=room_type_id FROM dbo.room_types WITH(UPDLOCK,HOLDLOCK) WHERE room_type_id="+RoomType.ToString(System.Globalization.CultureInfo.InvariantCulture)+"; "+command.CommandText;
            }return ValueTask.FromResult(result);
        }
        public override Task CommandFailedAsync(DbCommand command,CommandErrorEventData data,CancellationToken ct=default){var n=Number(data.Exception);if(n!=null)Numbers.Add(n.Value);return Task.CompletedTask;}
    }
}
