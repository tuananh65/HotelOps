using System.Data.Common;
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

internal static class Task68RoomTypeFaults
{
    internal static List<object> Waits{get;}=[];
    internal static List<object> Failures{get;}=[];
    internal static async Task RunAsync(HotelOpsDbContext setup,Func<DbCommandInterceptor?,HotelOpsDbContext> open,
        IReadOnlyDictionary<string,User> users,long branch,long checklist,long floor,Action<bool,string> check,CancellationToken ct)
    {
        var actor=users["admin"].UserId;var raw=SessionCredential.Generate();var now=DateTime.UtcNow;
        var row=new UserSession{UserId=actor,SessionKeyHash=SessionCredential.Hash(raw),CreatedAt=now,LastUsedAt=now,ExpiresAt=now.AddMinutes(30)};
        setup.Add(row);await setup.SaveChangesAsync(ct);setup.ChangeTracker.Clear();
        var ticket=SessionReference.FromProtectedTicket(actor,raw,false,now);var snapshot=new SessionSnapshot(row.SessionId,actor,now,row.ExpiresAt,now.AddHours(12),false);
        RoomTypeCreationService Service(HotelOpsDbContext db)
        {
            var context=new DefaultHttpContext{User=HotelOpsCookieTicket.Create(new(ticket,snapshot),TimeProvider.System).Principal};context.Items[typeof(SessionSnapshot)]=snapshot;
            var current=new CurrentUserAccessor(new Context{HttpContext=context});
            return new(db,current,new PermissionService(current,new EfPermissionDataSource(db),new UnresolvedPermissionResourceResolver(),TimeProvider.System),
                new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),TimeProvider.System);
        }
        Task<RoomTypeCreationResult> Create(HotelOpsDbContext db,string code,CancellationToken token)=>Service(db).CreateAsync(ticket,row.SessionId,new(branch,code,"SQL safety test",30,checklist),token);
        async Task<int> Spid(HotelOpsDbContext db){await db.Database.OpenConnectionAsync(ct);return await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);}
        async Task<int> Wait(string scenario,int owner,Task pending)
        {
            for(var i=0;i<180&&!pending.IsCompleted;i++)
            {
                var found=await setup.Database.SqlQuery<WaitRow>($"SELECT CONVERT(int,session_id) AS SessionId,wait_type AS Kind FROM sys.dm_exec_requests WHERE blocking_session_id={owner} AND wait_type LIKE 'LCK_M_%'").ToArrayAsync(ct);
                if(found.Length>0){Waits.Add(new{scenario,owner,waiter=found[0].SessionId,found[0].Kind});return found[0].SessionId;}
                await Task.Delay(35,ct);
            }
            if(pending.IsFaulted)await pending;throw new InvalidOperationException("No UC017 SQL wait: "+scenario);
        }
        // User-first committed revocation wins; reader must not use stale principal role claims.
        await using(var holder=open(null))await using(var writer=open(null))
        {
            var owner=await Spid(holder);await using var tx=await holder.Database.BeginTransactionAsync(ct);
            await new UserEligibilityLock(holder).LockAsync([actor],ct);
            await holder.UserRoles.Where(x=>x.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,false),ct);
            var pending=Create(writer,"T68_STALE_ACTOR",ct);
            try{await Wait("actor-revocation-first",owner,pending);await tx.CommitAsync(ct);
                check((await pending).Status==RoomTypeCreationStatus.Forbidden,"UC017 actor authority authoritatively reread after User lock wait");}
            finally{if(holder.Database.CurrentTransaction!=null)await tx.RollbackAsync(CancellationToken.None);try{await pending;}catch{}await setup.UserRoles.Where(x=>x.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,true),ct);}
        }
        foreach(var scenario in new[]{"audit-failure","lock1222","timeout","deadlock1205","cancel-after-insert","session-expiry","grant-expiry"})
        {
            var code="T68_FAULT_"+Array.IndexOf(new[]{"audit-failure","lock1222","timeout","deadlock1205","cancel-after-insert","session-expiry","grant-expiry"},scenario);
            var auditCount=await setup.AuditLogs.CountAsync(x=>x.EntityType=="room_types",ct);
            if(scenario=="session-expiry")await setup.UserSessions.Where(x=>x.SessionId==row.SessionId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UtcNow.AddSeconds(5)),ct);
            if(scenario=="grant-expiry")await setup.UserRoles.Where(x=>x.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UtcNow.AddSeconds(5)),ct);
            var hook=new AuditProbe{Code=code,Floor=floor,Fail=scenario=="audit-failure"};await using var writer=open(hook);
            var writerSpid=await Spid(writer);await writer.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY LOW",ct);
            if(scenario=="lock1222")await writer.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 1200",ct);
            if(scenario=="timeout")writer.Database.SetCommandTimeout(2);
            await using var holder=open(null);var owner=await Spid(holder);await holder.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY HIGH",ct);
            await using var tx=await holder.Database.BeginTransactionAsync(ct);
            if(scenario!="audit-failure")await holder.Database.SqlQuery<long>($"SELECT floor_id AS [Value] FROM dbo.floors WITH(XLOCK,HOLDLOCK) WHERE floor_id={floor}").ToArrayAsync(ct);
            using var stop=CancellationTokenSource.CreateLinkedTokenSource(ct);var pending=Create(writer,code,stop.Token);Task? inverse=null;
            try
            {
                if(scenario!="audit-failure")await Wait("post-insert-"+scenario,owner,pending);
                if(scenario=="deadlock1205"){inverse=new UserEligibilityLock(holder).LockAsync([actor],ct);await Wait("test-inverse-deadlock",writerSpid,inverse);}
                if(scenario is "session-expiry" or "grant-expiry"){await Task.Delay(6000,ct);await tx.RollbackAsync(ct);}
                if(scenario=="cancel-after-insert")stop.Cancel();
                RoomTypeCreationResult? result=null;var caught=false;
                try{result=await pending;}catch(DbUpdateException e)when(scenario=="audit-failure"&&Number(e)==51069){caught=true;}catch(OperationCanceledException)when(scenario=="cancel-after-insert"){caught=true;}
                if(inverse!=null)await inverse;
                if(holder.Database.CurrentTransaction!=null)await tx.RollbackAsync(ct);
                var expected=scenario=="lock1222"?1222:scenario=="timeout"?-2:scenario=="deadlock1205"?1205:51069;
                var valid=scenario is "audit-failure" or "cancel-after-insert"?caught:scenario=="session-expiry"?result?.Status==RoomTypeCreationStatus.SessionInvalid:scenario=="grant-expiry"?result?.Status==RoomTypeCreationStatus.Forbidden:result?.Status==RoomTypeCreationStatus.Unavailable&&hook.Numbers.Contains(expected);
                check(valid&&hook.SawInsert&&writer.Database.CurrentTransaction==null&&!writer.ChangeTracker.Entries().Any(),"UC017 real post-INSERT "+scenario+" rolls back with clean context (status="+result?.Status+",sql="+string.Join(',',hook.Numbers)+")");
                Failures.Add(new{scenario,status=result?.Status.ToString(),numbers=hook.Numbers,hook.SawInsert});
            }
            finally
            {
                stop.Cancel();if(holder.Database.CurrentTransaction!=null)await tx.RollbackAsync(CancellationToken.None);
                try{await pending;}catch{}if(inverse!=null)try{await inverse;}catch{}
                await setup.UserSessions.Where(x=>x.SessionId==row.SessionId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,row.ExpiresAt),ct);
                await setup.UserRoles.Where(x=>x.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,(DateTime?)null),ct);
            }
            check(!await setup.RoomTypes.AnyAsync(x=>x.TypeCode==code,ct)&&await setup.AuditLogs.CountAsync(x=>x.EntityType=="room_types",ct)==auditCount,"UC017 no partial room-type/audit after "+scenario);
            await using var retry=open(null);var success=await Create(retry,code,ct);
            check(success.Status==RoomTypeCreationStatus.Success,"UC017 fresh explicit retry commits once after "+scenario);
            await using var replay=open(null);check((await Create(replay,code,ct)).Status==RoomTypeCreationStatus.DuplicateCode,"UC017 replay cannot create duplicate/audit after "+scenario);
        }
    }
    private sealed class Context:IHttpContextAccessor{public HttpContext? HttpContext{get;set;}}
    private sealed class WaitRow{public int SessionId{get;set;}public string Kind{get;set;}="";}
    private static int? Number(Exception e){for(Exception? x=e;x!=null;x=x.InnerException)if(x is SqlException sql)return sql.Number;return null;}
    private sealed class AuditProbe:DbCommandInterceptor
    {
        internal string Code="";internal long Floor;internal bool Fail,SawInsert;internal List<int> Numbers{get;}=[];
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<DbDataReader> result,CancellationToken ct=default)
        {
            if(command.CommandText.Contains("INSERT INTO [audit_logs]"))
            {
                await using var proof=command.Connection!.CreateCommand();proof.Transaction=command.Transaction;
                proof.CommandText="SELECT COUNT(*) FROM dbo.room_types WHERE type_code=@code";proof.Parameters.Add(new SqlParameter("@code",Code));
                SawInsert=(int)(await proof.ExecuteScalarAsync(ct))! == 1;
                command.CommandText=Fail?";THROW 51069,'Task68 deliberate audit failure',1;":
                    "/* T68:AUDIT_WAIT */ DECLARE @held bigint; SELECT @held=floor_id FROM dbo.floors WITH(UPDLOCK,HOLDLOCK) WHERE floor_id="+Floor.ToString(System.Globalization.CultureInfo.InvariantCulture)+"; "+command.CommandText;
            }
            return result;
        }
        public override Task CommandFailedAsync(DbCommand command,CommandErrorEventData data,CancellationToken ct=default){var n=Number(data.Exception);if(n!=null)Numbers.Add(n.Value);return Task.CompletedTask;}
    }
}
