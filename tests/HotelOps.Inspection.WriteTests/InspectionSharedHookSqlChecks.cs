using System.Data;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
internal static class InspectionSharedHookSqlChecks
{
    internal static async Task RunAsync(HotelOpsDbContext db,string environment,string output,Func<Task> guard)
    {
        var results=new List<object>();
        var root=await db.RoomInspections.AsNoTracking().OrderBy(x=>x.InspectionId).FirstAsync();
        var actor=root.InspectorUserId;var branch=await db.Rooms.Where(r=>r.RoomId==root.RoomId).Select(r=>r.BranchId).SingleAsync();
        var other=await db.Users.Where(u=>u.Username=="int.denied").Select(u=>u.UserId).SingleAsync();
        using var privateData=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(environment))!,"runtime.private.json")));
        CreatedSqlSession login;
        await guard();await using(var setup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await guard();
            var passwords=new PasswordCredentialService(Options.Create(new PasswordHasherOptions{CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
            var verified=await new CredentialLockoutService(db,passwords,TimeProvider.System).VerifyAsync(actor,privateData.RootElement.GetProperty("password").GetString()!,
                (u,_)=>Task.FromResult(u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"));
            login=await new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System).CreateSessionAsync(verified);
            await setup.CommitAsync();db.ChangeTracker.Clear();
        }
        var current=new Identity(new(actor,login.Session.SessionId,false));
        var method=typeof(RoomTypeCreationService).GetMethod("BatchAsync",BindingFlags.Instance|BindingFlags.NonPublic)??throw new Exception("Batch method missing");
        async Task<RoomTypeBatchResult> Batch(HotelOpsDbContext context,List<string> trace,
            Func<IReadOnlyDictionary<long,UserEligibilityLock.LockedUser?>,CancellationToken,Task>? hook,
            Func<long,CancellationToken,Task<RoomTypeBatchResult>> operation,CancellationToken ct=default)
        {
            var realPermissions=new PermissionService(current,new EfPermissionDataSource(context),new UnresolvedPermissionResourceResolver(),TimeProvider.System);
            var service=new RoomTypeCreationService(context,current,new TracePermissions(realPermissions,trace),
                new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),TimeProvider.System);
            var parameters=method.GetParameters();var values=parameters.Select(p=>p.HasDefaultValue?p.DefaultValue:null).ToArray();
            for(var i=0;i<parameters.Length;i++)values[i]=parameters[i].Name switch{
                "ticket"=>login.Ticket,"sessionId"=>login.Session.SessionId,"branchId"=>branch,"operation"=>operation,"ct"=>ct,
                "additionalUsers"=>new[]{other},"requireRoomTypeSchema"=>false,"permissionCode"=>"INSPECTION_PERFORM","onUsersLocked"=>hook,_=>values[i]};
            return await (Task<RoomTypeBatchResult>)method.Invoke(service,values)!;
        }
        async Task<string> State()=>await db.Database.SqlQuery<string>($"SELECT (SELECT i.inspection_id,i.row_version,a.original_file_name FROM dbo.room_inspections i LEFT JOIN dbo.attachments a ON a.entity_type='INSPECTION' AND a.entity_id=i.inspection_id ORDER BY i.inspection_id,a.attachment_id FOR JSON PATH) AS Value").SingleAsync();
        async Task Check(string title,Func<Task> run)
        {
            await guard();var state=await State();var audits=await db.AuditLogs.CountAsync();await run();
            if(db.Database.CurrentTransaction!=null||await State()!=state||await db.AuditLogs.CountAsync()!=audits)throw new Exception("Batch transaction/state leaked");
            results.Add(new{title,result="PASS"});Console.WriteLine("PASS "+title);
        }
        try
        {
            await Check("Existing caller without hook retains Gate/domain/final-Gate behavior",async()=>{
                var trace=new List<string>();var result=await Batch(db,trace,null,(_,_)=>{trace.Add("Domain");return Task.FromResult(new RoomTypeBatchResult(RoomTypeBatchStatus.Success));});
                if(result.Status!=RoomTypeBatchStatus.Success||!trace.SequenceEqual(new[]{"Gate","Domain","Gate"}))throw new Exception("Legacy Batch behavior changed");
            });
            await Check("Optional callback sees complete sorted locked results before Gate/catalog/domain",async()=>{
                var trace=new List<string>();var physical=db.Database.GetDbConnection();System.Data.Common.DbTransaction? observed=null;
                var result=await Batch(db,trace,async(locked,token)=>{
                    trace.Add("Callback");if(!locked.Keys.SequenceEqual(new[]{actor,other}.Order())||locked.Any(x=>x.Value?.UserId!=x.Key))throw new Exception("Wrong locked-user result");
                    foreach(var id in locked.Keys)UserEligibilityLock.RequireEligible(id,locked);
                    observed=db.Database.CurrentTransaction!.GetDbTransaction();
                    if(observed.IsolationLevel!=IsolationLevel.Serializable||!ReferenceEquals(physical,observed.Connection))throw new Exception("Wrong caller transaction");
                    if(await db.Database.SqlQuery<string>($"SELECT APPLOCK_MODE('public','HotelOps:RoomTypes:v1','Transaction') AS Value").SingleAsync(token)!="NoLock")throw new Exception("Callback ran after catalog");
                },async(_,token)=>{
                    trace.Add("Domain");if(!ReferenceEquals(observed,db.Database.CurrentTransaction!.GetDbTransaction()))throw new Exception("Transaction replaced");
                    if(await db.Database.SqlQuery<string>($"SELECT APPLOCK_MODE('public','HotelOps:RoomTypes:v1','Transaction') AS Value").SingleAsync(token)!="Exclusive")throw new Exception("Catalog absent before domain");
                    return new(RoomTypeBatchStatus.Success);
                });
                if(result.Status!=RoomTypeBatchStatus.Success||!trace.SequenceEqual(new[]{"Callback","Gate","Domain","Gate"}))throw new Exception("Hook order differs");
            });
            await Check("Callback exception rolls back injected SQL and prevents Gate/domain execution",async()=>{
                var trace=new List<string>();try{await Batch(db,trace,async(_,token)=>{
                    trace.Add("Callback");await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.attachments SET original_file_name='callback-rollback.pdf' WHERE entity_type='INSPECTION' AND entity_id={root.InspectionId}",token);
                    throw new InvalidOperationException("INJECTED_CALLBACK_FAILURE");
                },(_,_)=>throw new Exception("Domain reached"));throw new Exception("Failure swallowed");}
                catch(InvalidOperationException e)when(e.Message=="INJECTED_CALLBACK_FAILURE"){}
                if(!trace.SequenceEqual(new[]{"Callback"}))throw new Exception("Gate ran after callback failure");
            });
            await Check("Recipient outside actual Batch locked results fails closed before Gate",async()=>{
                var reader=new InspectionRecipientRosterReader(db,new MissingInspectionOriginalCleanerSource());
                var discovered=await reader.DiscoverAsync(actor,branch,root.InspectionId,other,Fe05NotificationKind.CleaningReworkRequested,default);
                var trace=new List<string>();try{await Batch(db,trace,(locked,token)=>{
                    trace.Add("Callback");return new InspectionUsersPrelock(db,reader).RetainLockedUsersAsync(discovered with{AssigneeId=long.MaxValue},locked,token);
                },(_,_)=>throw new Exception("Domain reached"));throw new Exception("Roster mismatch accepted");}
                catch(Fe05NotificationUnavailableException){}
                if(!trace.SequenceEqual(new[]{"Callback"}))throw new Exception("Gate reached for mismatched roster");
            });
            await Check("Concurrent real Batch callers contend on sorted Users then finish without lock-order deadlock",async()=>{
                var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var firstTrace=new List<string>();var secondTrace=new List<string>();
                await using var second=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options);
                var first=Batch(db,firstTrace,async(_,token)=>{ready.TrySetResult();await release.Task.WaitAsync(token);},(_,_)=>Task.FromResult(new RoomTypeBatchResult(RoomTypeBatchStatus.Success)),budget.Token);
                Task<RoomTypeBatchResult>? contender=null;
                try {
                    await ready.Task.WaitAsync(budget.Token);
                    contender=Batch(second,secondTrace,(_,_)=>{secondTrace.Add("Callback");return Task.CompletedTask;},(_,_)=>Task.FromResult(new RoomTypeBatchResult(RoomTypeBatchStatus.Success)),budget.Token);
                    await Task.Delay(150,budget.Token);if(contender.IsCompleted)throw new Exception("Contender did not wait for Users locks");
                } finally {release.TrySetResult();}
                var a=await first;var b=await contender!;
                if(a.Status!=RoomTypeBatchStatus.Success||b.Status!=RoomTypeBatchStatus.Success)throw new Exception("Concurrent callback Batch failed");
            });
            await Check("Concurrent revocation is blocked by retained recipient User lock and rolls back",async()=>{
                var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var held=Batch(db,new List<string>(),async(_,token)=>{ready.TrySetResult();await release.Task.WaitAsync(token);},(_,_)=>Task.FromResult(new RoomTypeBatchResult(RoomTypeBatchStatus.Success)),budget.Token);
                try
                {
                    await ready.Task.WaitAsync(budget.Token);
                    await using var second=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options);
                    await second.Database.OpenConnectionAsync(budget.Token);
                    using var expected=JsonDocument.Parse(await File.ReadAllTextAsync(environment,budget.Token));
                    var identity=await second.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),@@SERVERNAME)+'|'+DB_NAME()+'|'+(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE05INT001.Owner') AS Value").SingleAsync(budget.Token);
                    if(identity!=expected.RootElement.GetProperty("server").GetString()+"|"+expected.RootElement.GetProperty("database").GetString()+"|"+expected.RootElement.GetProperty("owner").GetString())throw new Exception("Contender isolation rejected");
                    await using var tx=await second.Database.BeginTransactionAsync(IsolationLevel.Serializable,budget.Token);
                    try {await second.Database.ExecuteSqlInterpolatedAsync($"SET LOCK_TIMEOUT 200; UPDATE dbo.users SET account_status='INACTIVE' WHERE user_id={other}",budget.Token);throw new Exception("Revocation bypassed retained User lock");}
                    catch(Microsoft.Data.SqlClient.SqlException e)when(e.Number==1222){}
                    finally {if(tx.GetDbTransaction().Connection!=null)await tx.RollbackAsync();}
                }
                finally {release.TrySetResult();await held;}
                if(await db.Users.Where(u=>u.UserId==other).Select(u=>u.AccountStatus).SingleAsync()!="ACTIVE")throw new Exception("Contender revocation persisted");
            });
            await File.WriteAllTextAsync(Path.Combine(output,"shared-hook-sql.json"),JsonSerializer.Serialize(new{success=true,realSession=true,domainMutation=false,sourceReadBinding=(string?)null,sqlDmvUsed=false,cases=results},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception e){await File.WriteAllTextAsync(Path.Combine(output,"shared-hook-sql.json"),JsonSerializer.Serialize(new{success=false,cases=results,error=e.GetType().Name+": "+e.Message},new JsonSerializerOptions{WriteIndented=true}));throw;}
    }
    private sealed class Identity(CurrentUserInfo actor):ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo user){user=actor;return true;}}
    // Instrumentation delegates every decision to the actual permission service; no synthetic grants.
    private sealed class TracePermissions(IPermissionService real,List<string> trace):IPermissionService
    {
        public Task<PermissionEvaluation> EvaluateBranchAsync(string permission,long branch,CancellationToken ct){trace.Add("Gate");return real.EvaluateBranchAsync(permission,branch,ct);}
        public Task<PermissionEvaluation> EvaluateGlobalAsync(string permission,CancellationToken ct)=>real.EvaluateGlobalAsync(permission,ct);
        public Task<PermissionEvaluation> EvaluateResourceAsync(string permission,PermissionResource resource,CancellationToken ct)=>real.EvaluateResourceAsync(permission,resource,ct);
    }
}
