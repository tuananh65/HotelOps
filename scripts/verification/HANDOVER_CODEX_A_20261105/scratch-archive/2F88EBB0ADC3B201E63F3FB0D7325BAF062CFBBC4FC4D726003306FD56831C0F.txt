using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Assets;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Task66StructureCases
{
    internal static async Task RunAsync(HotelOpsDbContext db, Func<DbCommandInterceptor?,HotelOpsDbContext> open, HttpClient http,
        Func<string,Task<CookieJar>> login, IReadOnlyDictionary<string,User> identities, long branch,long foreignBranch,
        Action<bool,string> check,CancellationToken ct)
    {
        var floor = new Floor { BranchId=branch,FloorCode="T66_F",FloorName="Trial floor",IsActive=true };
        db.Add(floor); await db.SaveChangesAsync(ct);
        var area = new Area { BranchId=branch,FloorId=floor.FloorId,AreaCode="T66_A",AreaName="Trial area",IsActive=true };
        var empty = new Area { BranchId=branch,AreaCode="T66_FREE",AreaName="Trial empty area",IsActive=true };
        db.AddRange(area,empty); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var admin=await login("admin");var denied=await login("viewer");var scoped=await login("branch");
        string Path(bool isArea,long id,long? b=null)=>$"/api/branches/{b??branch}/{(isArea?"areas":"floors")}/{id}";
        async Task<HttpResponseMessage> Send(HttpMethod method,string path,CookieJar jar,object? body=null,string? tag=null,CancellationToken? token=null)
        {
            using var req=new HttpRequestMessage(method,path);
            if(jar.Cookies.Count>0)req.Headers.Add("Cookie",string.Join("; ",jar.Cookies.Select(x=>x.Key+"="+x.Value)));
            if(jar.Token!=null)req.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName,jar.Token);
            if(tag!=null)req.Headers.TryAddWithoutValidation("If-Match",tag);
            if(body!=null)req.Content=JsonContent.Create(body);
            return await http.SendAsync(req,token??ct);
        }
        async Task<string> Tag(bool isArea,long id)
        {using var r=await Send(HttpMethod.Get,Path(isArea,id),admin);r.EnsureSuccessStatusCode();return r.Headers.ETag!.Tag;}
        async Task<JsonElement> Attempt(bool isArea,long id,int status,string? tag=null,CookieJar? jar=null,object? body=null,string? path=null)
        {
            using var r=await Send(HttpMethod.Post,(path??Path(isArea,id))+"/deactivate",jar??admin,
                body??new{reason="Location no longer used",confirmation=true},tag??await Tag(isArea,id));
            var text=await r.Content.ReadAsStringAsync(ct);
            check((int)r.StatusCode==status,"UC016 HTTPS expected "+status+", actual "+(int)r.StatusCode);
            check(!text.Contains("SqlException")&&!text.Contains("passwordHash"),"UC016 no provider/credential disclosure");
            if(status!=400)check(r.Headers.CacheControl?.NoStore==true,"UC016 authenticated controller responses no-store");
            using var json=JsonDocument.Parse(text);return json.RootElement.Clone();
        }
        var count=await db.AuditLogs.CountAsync(ct);var ftag=await Tag(false,floor.FloorId);var atag=await Tag(true,area.AreaId);
        var anonymous=new CookieJar();
        using(var csrf=await Send(HttpMethod.Get,"/api/auth/csrf",anonymous))
        {
            anonymous.Read(csrf);using var json=JsonDocument.Parse(await csrf.Content.ReadAsStringAsync(ct));
            anonymous.Token=json.RootElement.GetProperty("csrfToken").GetString();
        }
        using(var response=await Send(HttpMethod.Post,Path(true,area.AreaId)+"/deactivate",anonymous,new{reason="Location no longer used",confirmation=true},atag))
            check((int)response.StatusCode==401,"UC016 anonymous with valid CSRF rejected401");
        await Attempt(true,area.AreaId,404,atag,path:Path(true,long.MaxValue));
        var blocked=await Attempt(false,floor.FloorId,409,ftag);
        check(blocked.GetProperty("dependencies").EnumerateArray().Any(x=>x.GetProperty("kind").GetString()=="ACTIVE_CHILD_AREA"
            &&x.GetProperty("ids")[0].GetInt64()==area.AreaId),"UC016 exact active child Area dependency");
        var unresolved=await Attempt(true,empty.AreaId,503);
        check(unresolved.GetProperty("code").GetString()=="STRUCTURE_WRITER_COORDINATION_REQUIRED","UC016 empty target does not bypass uncoordinated production writers");
        await Attempt(true,area.AreaId,403,atag,denied);
        await Attempt(true,area.AreaId,403,atag,scoped,path:Path(true,area.AreaId,foreignBranch));
        await Attempt(true,area.AreaId,400,atag,body:new{reason="short",confirmation=true});
        await Attempt(true,area.AreaId,400,atag,body:new{reason="Location no longer used",confirmation=false});
        await Attempt(true,area.AreaId,400,atag,body:new{reason="Location no longer used",confirmation=true,isActive=false});
        await Attempt(true,area.AreaId,412,"\""+new string('0',64)+"\"");
        await Attempt(true,area.AreaId,400,"W/"+atag);
        using(var missing=await Send(HttpMethod.Post,Path(true,area.AreaId)+"/deactivate",admin,new{reason="Location no longer used",confirmation=true}))
            check((int)missing.StatusCode==428,"UC016 missing If-Match 428");
        await Attempt(true,area.AreaId,400,atag,path:Path(true,0));
        // Check production predicates using real persisted positive and negative fixtures.
        var roomType=new RoomType{TypeCode="T66_ROOMTYPE",TypeName="Trial room type",IsActive=true};
        var department=new Department{BranchId=branch,DepartmentCode="T66_DEPT",DepartmentName="Trial department",IsActive=true};
        db.AddRange(roomType,department);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var room=new Room{BranchId=branch,FloorId=floor.FloorId,AreaId=area.AreaId,
            RoomTypeId=roomType.RoomTypeId,RoomNumber="T66_ROOM",OperationalStatus="AVAILABLE",IsActive=true};
        db.Add(room);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var deps=await Attempt(true,area.AreaId,409,atag);
        check(deps.GetProperty("dependencies").EnumerateArray().Any(x=>x.GetProperty("kind").GetString()=="ACTIVE_ROOM"),"UC016 active Room blocks Area");
        await db.Rooms.Where(x=>x.RoomId==room.RoomId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,false),ct);
        await Attempt(true,area.AreaId,503,atag);
        var type=new AssetType{TypeCode="T66_TYPE",TypeName="Trial asset type",IsActive=true};db.Add(type);await db.SaveChangesAsync(ct);
        var asset=new Asset{BranchId=branch,AssetCode="T66_ASSET",AssetName="Trial asset",AssetTypeId=type.AssetTypeId,
            LifecycleStatus="IN_USE",ConditionStatus="GOOD",CurrentRoomId=room.RoomId};db.Add(asset);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        deps=await Attempt(true,area.AreaId,409,atag);
        check(deps.GetProperty("dependencies").EnumerateArray().Any(x=>x.GetProperty("kind").GetString()=="ACTIVE_ASSET"),"UC016 active Asset in inactive Room still blocks");
        await db.Assets.Where(x=>x.AssetId==asset.AssetId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.LifecycleStatus,"RETIRED"),ct);
        await Attempt(true,area.AreaId,503,atag);
        var incident=new Incident{BranchId=branch,IncidentNo="T66_INC",AreaId=area.AreaId,IncidentTitle="Trial incident",Description="Trial",
            Status="RESOLVED",ReportedBy=identities["admin"].UserId,DiscoveredAt=DateTime.UtcNow,ReportedAt=DateTime.UtcNow};
        db.Add(incident);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        deps=await Attempt(true,area.AreaId,409,atag);
        check(deps.GetProperty("dependencies").EnumerateArray().Any(x=>x.GetProperty("kind").GetString()=="OPEN_INCIDENT"),"UC016 RESOLVED Incident is reopenable, blocks");
        await db.Incidents.Where(x=>x.IncidentId==incident.IncidentId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"CLOSED"),ct);
        await Attempt(true,area.AreaId,503,atag);
        var maintenance=new MaintenanceWork{WorkNo="T66_WORK",IncidentId=incident.IncidentId,Title="Trial completed work",
            Status="COMPLETED",CreatedBy=identities["admin"].UserId,CreatedAt=DateTime.UtcNow};
        db.Add(maintenance);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        deps=await Attempt(true,area.AreaId,409,atag);
        check(deps.GetProperty("dependencies").EnumerateArray().Any(x=>x.GetProperty("kind").GetString()=="OPEN_MAINTENANCE_WORK"),"UC016 unverified COMPLETED maintenance blocks even with closed Incident");
        await db.MaintenanceWorks.Where(x=>x.MaintenanceWorkId==maintenance.MaintenanceWorkId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"CLOSED"),ct);
        await Attempt(true,area.AreaId,503,atag);
        var task=new OperationalTask{BranchId=branch,DepartmentId=department.DepartmentId,
            TaskCode="T66_TASK",Title="Trial task",AreaId=area.AreaId,Status="COMPLETED",CreatedBy=identities["admin"].UserId,CreatedAt=DateTime.UtcNow};
        db.Add(task);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        deps=await Attempt(true,area.AreaId,409,atag);
        check(deps.GetProperty("dependencies").EnumerateArray().Any(x=>x.GetProperty("kind").GetString()=="TASK_COMPLETION_CONTRACT_UNRESOLVED"),"UC016 COMPLETED Task not silently treated as terminal");
        await db.OperationalTasks.Where(x=>x.TaskId==task.TaskId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"TODO"),ct);
        deps=await Attempt(true,area.AreaId,409,atag);
        check(deps.GetProperty("dependencies").EnumerateArray().Any(x=>x.GetProperty("kind").GetString()=="OPEN_OPERATIONAL_TASK"),"UC016 current TODO task separately blocks");
        await db.OperationalTasks.Where(x=>x.TaskId==task.TaskId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"CANCELLED"),ct);
        await Attempt(true,area.AreaId,503,atag);
        check(await Tag(false,floor.FloorId)==ftag&&await Tag(true,area.AreaId)==atag&&await db.AuditLogs.CountAsync(ct)==count,
            "UC016 dependency/coordination/validation rejection preserves targets, revision and audit");
        var simultaneous=await Task.WhenAll(
            Send(HttpMethod.Post,Path(true,area.AreaId)+"/deactivate",admin,new{reason="Location no longer used",confirmation=true},atag),
            Send(HttpMethod.Post,Path(true,area.AreaId)+"/deactivate",scoped,new{reason="Location no longer used",confirmation=true},atag));
        check(simultaneous.All(x=>(int)x.StatusCode==503),"UC016 simultaneous empty-target attempts both fail closed; no duplicate mutation");
        foreach(var response in simultaneous)response.Dispose();
        foreach(var scenario in new[]{"stale","cancel"})
        {
            await using var holder=open(null);await holder.Database.OpenConnectionAsync(ct);
            var spid=await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
            await using var tx=await holder.Database.BeginTransactionAsync(ct);
            await holder.Areas.Where(x=>x.AreaId==area.AreaId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Description,
                scenario=="stale"?"T66 authoritative concurrent change":(string?)null),ct);
            using var stop=CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pending=Send(HttpMethod.Post,Path(true,area.AreaId)+"/deactivate",admin,new{reason="Location no longer used",confirmation=true},atag,stop.Token);
            try
            {
                var observed=false;
                for(var i=0;i<150&&!pending.IsCompleted;i++)
                {
                    observed=await db.Database.SqlQuery<int>($"SELECT CONVERT(int,COUNT(*)) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={spid} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct)>0;
                    if(observed)break;await Task.Delay(40,ct);
                }
                check(observed,"UC016 HTTPS "+scenario+" waits actual SQL target lock (DMV)");
                if(scenario=="cancel")
                {
                    stop.Cancel();var cancelled=false;
                    try{using var response=await pending;}catch(OperationCanceledException){cancelled=true;}
                    check(cancelled,"UC016 HTTPS cancellation while waiting propagates");
                }
                else
                {
                    await tx.CommitAsync(ct);using var response=await pending;
                    check((int)response.StatusCode==412,"UC016 target is authoritatively reread after concurrent writer commit");
                }
            }
            finally{stop.Cancel();if(holder.Database.CurrentTransaction!=null)await tx.RollbackAsync(CancellationToken.None);try{using var response=await pending;}catch(OperationCanceledException){}}
            if(scenario=="stale")await db.Areas.Where(x=>x.AreaId==area.AreaId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Description,(string?)null),ct);
        }
        var actor=identities["admin"].UserId;
        await db.UserRoles.Where(x=>x.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,false),ct);
        try{await Attempt(true,area.AreaId,403,atag);}finally{await db.UserRoles.Where(x=>x.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,true),ct);}
        var session=await db.UserSessions.AsNoTracking().Where(x=>x.UserId==actor).OrderByDescending(x=>x.SessionId).FirstAsync(ct);
        await db.UserSessions.Where(x=>x.SessionId==session.SessionId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UtcNow.AddSeconds(-1)),ct);
        try{await Attempt(true,area.AreaId,401,atag);}finally{await db.UserSessions.Where(x=>x.SessionId==session.SessionId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,session.ExpiresAt),ct);}
        // DEV2 defect reproduction, never reported as a successful UC016 mutation.
        await db.Areas.Where(x=>x.AreaId==empty.AreaId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,false),ct);
        await Attempt(true,empty.AreaId,409);
        await using(var context=open(null))
        {
            var created=await new IncidentService(context).CreateIncidentAsync(new CreateIncidentRequest{BranchId=branch,
                AreaId=empty.AreaId,ReportedBy=identities["admin"].UserId,IncidentTitle="T66 inactive location repro",
                Description="Disposable evidence only",DiscoveredAt=DateTime.UtcNow},ct);
            check(await db.Incidents.AnyAsync(x=>x.IncidentId==created.IncidentId&&x.AreaId==empty.AreaId,ct),
                "CONFIRMED DEV2 defect: actual CreateIncidentAsync commits OPEN Incident at inactive Area");
        }
        await db.Assets.Where(x=>x.AssetId==asset.AssetId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.CurrentRoomId,(long?)null)
            .SetProperty(x=>x.LifecycleStatus,"IN_USE"),ct);
        await db.Areas.Where(x=>x.AreaId==empty.AreaId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,true),ct);
        var pause=new PauseBeforeAssetWrite();await using(var context=open(pause))
        {
            var writer=new AssetService(context).AssignLocationAsync(asset.AssetId,new AssignAssetLocationRequest{
                ActorUserId=identities["admin"].UserId,AreaId=empty.AreaId},ct);
            try
            {
                await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15),ct);
                await db.Areas.Where(x=>x.AreaId==empty.AreaId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,false),ct);
            }
            finally{pause.Resume.TrySetResult();}
            await writer;
        }
        check(await db.Assets.AnyAsync(x=>x.AssetId==asset.AssetId&&x.CurrentAreaId==empty.AreaId,ct)
            &&await db.Areas.AnyAsync(x=>x.AreaId==empty.AreaId&&!x.IsActive,ct),
            "CONFIRMED DEV2 race: actual AssignLocationAsync commits stale active-Area decision after independent deactivation");
    }
    private sealed class PauseBeforeAssetWrite:DbCommandInterceptor
    {
        internal TaskCompletionSource Reached { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Resume { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData data,
            InterceptionResult<DbDataReader> result,CancellationToken ct=default)
        {
            if(command.CommandText.Contains("UPDATE [assets]",StringComparison.OrdinalIgnoreCase))
            {Reached.TrySetResult();await Resume.Task.WaitAsync(ct);}
            return result;
        }
    }
}
