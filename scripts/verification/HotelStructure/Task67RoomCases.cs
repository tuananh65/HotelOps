using System.Data.Common;
using System.Net;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Rooms;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Rooms;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Task67RoomCases
{
    internal static List<object> Waits { get; }=[];
    internal static async Task RunAsync(HotelOpsDbContext db,Func<DbCommandInterceptor?,HotelOpsDbContext> open,HttpClient http,
        Func<string,Task<CookieJar>> login,IReadOnlyDictionary<string,User> users,long branch,long otherBranch,Action<bool,string> check,CancellationToken ct)
    {
        var globalRole=new Role{RoleCode="T67_GLOBAL",RoleName="Trial explicit ROOM_VIEW ALL",IsActive=true};
        var ownRole=new Role{RoleCode="T67_OWN",RoleName="Trial unsupported OWN",IsActive=true};
        db.AddRange(globalRole,ownRole);await db.SaveChangesAsync(ct);
        var permission=await db.Permissions.Where(x=>x.PermissionCode=="ROOM_VIEW").Select(x=>x.PermissionId).SingleAsync(ct);
        db.AddRange(new RolePermission{RoleId=globalRole.RoleId,PermissionId=permission,DataScope="ALL"},
            new RolePermission{RoleId=ownRole.RoleId,PermissionId=permission,DataScope="OWN"});
        foreach(var (key,role,assignmentBranch) in new[]{("other",globalRole.RoleId,(long?)null),("branch",globalRole.RoleId,(long?)branch),("owner",ownRole.RoleId,(long?)branch)})
            db.Add(new UserRole{UserId=users[key].UserId,RoleId=role,BranchId=assignmentBranch,IsActive=true,AssignedAt=DateTime.UtcNow.AddHours(-1)});
        db.Add(new UserBranchAccess{UserId=users["owner"].UserId,BranchId=branch,AccessLevel="STANDARD",GrantedAt=DateTime.UtcNow.AddHours(-1)});
        var type=new RoomType{TypeCode="T67_TYPE",TypeName="Trial room type",IsActive=true};
        var floor=new Floor{BranchId=branch,FloorCode="T67_F",FloorName="Trial floor",FloorNumber=1,IsActive=true};
        var otherFloor=new Floor{BranchId=otherBranch,FloorCode="T67_B",FloorName="Foreign floor marker",FloorNumber=2,IsActive=true};
        db.AddRange(type,floor,otherFloor);await db.SaveChangesAsync(ct);
        var area=new Area{BranchId=branch,FloorId=floor.FloorId,AreaCode="T67_A",AreaName="Trial area",IsActive=true};
        db.Add(area);await db.SaveChangesAsync(ct);
        var rooms=Enumerable.Range(0,27).Select(i=>new Room{BranchId=branch,FloorId=floor.FloorId,AreaId=area.AreaId,
            RoomTypeId=type.RoomTypeId,RoomNumber="T67_"+i.ToString("D2"),OperationalStatus=i==0?"OUT_OF_SERVICE":i==2?"READY":"AVAILABLE",IsActive=i!=26,Description="Room description"}).ToArray();
        var foreign=new Room{BranchId=otherBranch,FloorId=otherFloor.FloorId,RoomTypeId=type.RoomTypeId,RoomNumber="T67_FOREIGN",OperationalStatus="AVAILABLE",IsActive=true};
        var corrupt=new Room{BranchId=branch,FloorId=otherFloor.FloorId,RoomTypeId=type.RoomTypeId,RoomNumber="CORRUPT_FOREIGN",OperationalStatus="AVAILABLE",IsActive=true};
        db.AddRange(rooms);db.AddRange(foreign,corrupt);await db.SaveChangesAsync(ct);
        var block=new RoomBlock{RoomId=rooms[0].RoomId,Reason="Verified room block reason",BlockedBy=users["other"].UserId,BlockedAt=DateTime.UtcNow.AddHours(-1)};
        var foreignBlock=new RoomBlock{RoomId=foreign.RoomId,Reason="Foreign block secret marker",BlockedBy=users["other"].UserId,BlockedAt=DateTime.UtcNow};
        db.AddRange(block,foreignBlock);await db.SaveChangesAsync(ct);
        var now=DateTime.UtcNow;
        db.AddRange(new RoomStatusHistory{RoomId=rooms[0].RoomId,ToStatus="AVAILABLE",TriggerType="INIT",ChangedAt=now.AddHours(-2)},
            new RoomStatusHistory{RoomId=rooms[0].RoomId,FromStatus="AVAILABLE",ToStatus="OUT_OF_SERVICE",TriggerType="ROOM_BLOCK",TriggerId=foreignBlock.BlockId,Reason="Foreign linkage is not published",ChangedAt=now.AddHours(-1)},
            new RoomStatusHistory{RoomId=rooms[0].RoomId,FromStatus="OUT_OF_SERVICE",ToStatus="OUT_OF_SERVICE",TriggerType="ROOM_BLOCK",TriggerId=block.BlockId,Reason=block.Reason,ChangedBy=users["other"].UserId,ChangedAt=now},
            new RoomStatusHistory{RoomId=rooms[2].RoomId,ToStatus="DIRTY",TriggerType="MANUAL_CORRECTION",Reason="Stale evidence must not become current reason",ChangedAt=now});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var reader=await login("viewer");var global=await login("other");var bound=await login("branch");var master=await login("admin");var owner=await login("owner");
        var auditCount=await db.AuditLogs.CountAsync(ct);
        Task<string> RoomSnapshot()=>db.Database.SqlQueryRaw<string>("SELECT (SELECT * FROM dbo.rooms ORDER BY room_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS [Value]").SingleAsync(ct);
        var originalRooms=await RoomSnapshot();
        async Task<HttpResponseMessage> Send(string path,CookieJar jar,CancellationToken? token=null)
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,path);
            if(jar.Cookies.Count>0)request.Headers.Add("Cookie",string.Join("; ",jar.Cookies.Select(x=>x.Key+"="+x.Value)));
            return await http.SendAsync(request,token??ct);
        }
        async Task<JsonElement> Get(string path,CookieJar jar,int expected=200)
        {
            using var response=await Send(path,jar);var text=await response.Content.ReadAsStringAsync(ct);
            check((int)response.StatusCode==expected,"UC023 HTTPS expected "+expected+", actual "+(int)response.StatusCode);
            check(!text.Contains("Foreign block secret marker")&&!text.Contains("passwordHash")&&!text.Contains("sessionKey")&&!text.Contains("SqlException")&&!text.Contains("stackTrace"),"UC023 no secret/provider/foreign-source disclosure");
            if(expected==200)check(response.Headers.CacheControl?.NoStore==true,"UC023 successful HTTP no-store");
            using var json=JsonDocument.Parse(text);return json.RootElement.Clone();
        }
        string List(string suffix="")=>$"/api/rooms?branchId={branch}"+suffix;
        string Detail(long id,long? b=null)=>$"/api/rooms/{id}?branchId={b??branch}";
        string History(long id,string suffix="")=>$"/api/rooms/{id}/history?branchId={branch}"+suffix;
        using(var anonymous=await Send(List(),new CookieJar()))check(anonymous.StatusCode==HttpStatusCode.Unauthorized,"UC023 anonymous GET401");
        await Get(List(),master,403);await Get(List(),owner,403);
        await Get("/api/rooms?allBranches=true",reader,403);await Get("/api/rooms?allBranches=true",bound,403);
        var all=await Get("/api/rooms?allBranches=true&pageSize=100",global);
        check(all.GetProperty("totalCount").GetInt64()==28&&all.GetProperty("items").EnumerateArray().Any(x=>x.GetProperty("branchId").GetInt64()==otherBranch),"UC023 explicit global ALL SQL grant permits both branches, malformed FK excluded");
        var first=await Get(List(),reader);var second=await Get(List("&page=2"),reader);
        check(first.GetProperty("totalCount").GetInt64()==27&&first.GetProperty("items").GetArrayLength()==20&&second.GetProperty("items").GetArrayLength()==7,"UC023 default20 pagination and count");
        var ids=first.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray()).Select(x=>x.GetProperty("id").GetInt64()).ToArray();
        check(ids.SequenceEqual(rooms.Select(x=>x.RoomId))&&ids.Distinct().Count()==27,"UC023 floor/room deterministic pages without duplicates");
        check(first.GetProperty("items")[0].EnumerateObject().All(x=>new[]{"id","branchId","roomNumber","floor","area","roomType","operationalStatusCode","isActive"}.Contains(x.Name)),"UC023 list DTO allowlist");
        await Get(List(),bound);await Get($"/api/rooms?branchId={otherBranch}",reader,403);
        await Get(Detail(foreign.RoomId),reader,404);await Get(Detail(long.MaxValue),reader,404);
        await Get(Detail(foreign.RoomId,otherBranch),reader,403);await Get(Detail(corrupt.RoomId),global,404);
        foreach(var query in new[]{"&floorId="+floor.FloorId,"&areaId="+area.AreaId,"&roomTypeId="+type.RoomTypeId})
            check((await Get(List(query),reader)).GetProperty("totalCount").GetInt64()==27,"UC023 supported SQL relationship filter");
        check((await Get(List("&isActive=false"),reader)).GetProperty("totalCount").GetInt64()==1,"UC023 inactive filter");
        check((await Get(List("&operationalStatusCode=OUT_OF_SERVICE"),reader)).GetProperty("totalCount").GetInt64()==1,"UC023 raw persisted status filter");
        check((await Get(List("&search=T67_00"),reader)).GetProperty("totalCount").GetInt64()==1,"UC023 literal keyword search");
        check((await Get(List("&search=%25%5B"),reader)).GetProperty("totalCount").GetInt64()==0,"UC023 wildcard syntax treated literally, empty list");
        check((await Get(List("&page=999"),reader)).GetProperty("items").GetArrayLength()==0,"UC023 beyond-end empty page");
        foreach(var sort in new[]{"floor","roomNumber","roomType","status"})foreach(var descending in new[]{false,true})
        {
            var path=List("&pageSize=100&sort="+sort+"&descending="+descending.ToString().ToLowerInvariant());
            var one=await Get(path,reader);var two=await Get(path,reader);
            check(one.GetRawText()==two.GetRawText(),"UC023 deterministic whitelisted sort "+sort+" descending="+descending);
            if(sort=="roomNumber"&&descending)check(one.GetProperty("items")[0].GetProperty("id").GetInt64()==rooms[^1].RoomId,"UC023 descending sorting actually applied");
        }
        foreach(var path in new[]{"/api/rooms",List("&allBranches=true"),List("&page=0"),List("&pageSize=101"),List("&sort=DROP%20TABLE"),
            List("&floorId=-1"),List("&operationalStatusCode=FAKE"),List("&page=2147483647&pageSize=100"),"/api/rooms/0?branchId="+branch,Detail(rooms[0].RoomId,0)})
            await Get(path,reader,400);
        var detail=await Get(Detail(rooms[0].RoomId),reader);
        var evidence=detail.GetProperty("currentStatusEvidence");
        check(detail.GetProperty("room").GetProperty("operationalStatusCode").GetString()=="OUT_OF_SERVICE"
            &&evidence.GetProperty("reason").GetString()==block.Reason&&evidence.GetProperty("source").GetProperty("id").GetInt64()==block.BlockId,"UC023 detail reason and verified same-room block from persisted latest history");
        check(evidence.GetProperty("recordedAtUtc").GetString()!.EndsWith('Z')&&detail.GetProperty("description").GetString()=="Room description","UC023 detail fields and UTC serialization");
        var history=await Get(detail.GetProperty("historyUrl").GetString()!,reader);
        check(history.GetProperty("items").GetArrayLength()==3&&history.GetProperty("coverage").GetString()=="RECORDED_STATUS_EVENTS_ONLY","UC023 real history link, truthful coverage");
        check(history.GetProperty("items")[0].GetProperty("source").GetProperty("id").GetInt64()==block.BlockId
            &&history.GetProperty("items")[1].GetProperty("source").GetProperty("id").ValueKind==JsonValueKind.Null,"UC023 foreign-room polymorphic source ID suppressed");
        check(!history.GetRawText().Contains("changedBy")&&!detail.GetRawText().Contains("actorUserId"),"UC023 no unrelated personnel fields in detail/history");
        var historyPage=await Get(History(rooms[0].RoomId,"&pageSize=1&page=2"),reader);
        check(historyPage.GetProperty("items").GetArrayLength()==1&&historyPage.GetProperty("totalCount").GetInt64()==3,"UC023 history count/page");
        check((await Get(Detail(rooms[1].RoomId),reader)).GetProperty("currentStatusEvidence").GetProperty("availability").GetString()=="NOT_RECORDED","UC023 missing history does not fabricate current reason");
        check((await Get(History(rooms[1].RoomId),reader)).GetProperty("items").GetArrayLength()==0,"UC023 empty recorded history supported");
        check((await Get(Detail(rooms[2].RoomId),reader)).GetProperty("currentStatusEvidence").GetProperty("availability").GetString()=="OUT_OF_SYNC","UC023 stale history does not override authoritative room status");
        await Get(History(foreign.RoomId),reader,404);await Get(History(rooms[0].RoomId,"&pageSize=101"),reader,400);
        check(await db.AuditLogs.CountAsync(ct)==auditCount&&await db.RoomStatusHistories.CountAsync(ct)==4,"UC023 successful and denied reads create no business audit/history events");
        check(await RoomSnapshot()==originalRooms,"UC023 functional HTTP reads preserve every persisted Room column");
        // Exercise both other supported polymorphic source batches with persisted SQL fixtures.
        var task=new CleaningTask{TaskNo="T67_CLEAN",RoomId=rooms[3].RoomId,
            CleaningTypeId=await db.ReferenceValues.Where(x=>x.GroupCode=="CLEANING_TYPE").Select(x=>x.ReferenceId).FirstAsync(ct),
            Status="IN_PROGRESS",CreatedBy=users["other"].UserId,CreatedAt=now};
        var checklist=new InspectionChecklist{ChecklistCode="T67_CHECK",ChecklistName="Trial checklist",RoomTypeId=type.RoomTypeId,IsActive=true};
        db.AddRange(task,checklist);await db.SaveChangesAsync(ct);
        var version=new InspectionChecklistVersion{ChecklistId=checklist.ChecklistId,VersionNo=1,Status="DRAFT"};db.Add(version);await db.SaveChangesAsync(ct);
        var inspection=new RoomInspection{InspectionNo="T67_INSPECT",RoomId=rooms[4].RoomId,VersionId=version.VersionId,
            InspectorUserId=users["other"].UserId,Status="PENDING",CreatedAt=now};db.Add(inspection);await db.SaveChangesAsync(ct);
        await db.Rooms.Where(x=>x.RoomId==rooms[3].RoomId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.OperationalStatus,"CLEANING"),ct);
        await db.Rooms.Where(x=>x.RoomId==rooms[4].RoomId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.OperationalStatus,"INSPECTION"),ct);
        db.AddRange(new RoomStatusHistory{RoomId=rooms[3].RoomId,ToStatus="CLEANING",TriggerType="CLEANING_TASK",TriggerId=task.CleaningTaskId,Reason="Recorded cleaning task",ChangedAt=now},
            new RoomStatusHistory{RoomId=rooms[4].RoomId,ToStatus="INSPECTION",TriggerType="INSPECTION",TriggerId=inspection.InspectionId,Reason="Recorded inspection",ChangedAt=now},
            new RoomStatusHistory{RoomId=rooms[5].RoomId,ToStatus="AVAILABLE",TriggerType="MAINTENANCE",TriggerId=long.MaxValue,Reason="Recorded source unavailable",ChangedAt=now});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var taskDetail=await Get(Detail(rooms[3].RoomId),reader);
        check(taskDetail.GetProperty("currentStatusEvidence").GetProperty("source").GetProperty("id").GetInt64()==task.CleaningTaskId,"UC023 SQL verified cleaning-task source");
        var inspectionDetail=await Get(Detail(rooms[4].RoomId),reader);
        check(inspectionDetail.GetProperty("currentStatusEvidence").GetProperty("source").GetProperty("id").GetInt64()==inspection.InspectionId,"UC023 SQL verified inspection source");
        var missingSource=await Get(Detail(rooms[5].RoomId),reader);
        check(missingSource.GetProperty("currentStatusEvidence").GetProperty("source").GetProperty("id").ValueKind==JsonValueKind.Null
            &&missingSource.GetProperty("currentStatusEvidence").GetProperty("source").GetProperty("availability").GetString()=="UNRESOLVED","UC023 untyped/unavailable source is truthful, no invented maintenance linkage");
        // SQL-backed expiry/revocation must affect the next real HTTP request.
        var readerId=users["viewer"].UserId;
        await db.UserRoles.Where(x=>x.UserId==readerId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,false),ct);
        try{await Get(List(),reader,403);await Get(Detail(rooms[0].RoomId),reader,403);await Get(History(rooms[0].RoomId),reader,403);}
        finally{await db.UserRoles.Where(x=>x.UserId==readerId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,true),ct);}
        var session=await db.UserSessions.AsNoTracking().Where(x=>x.UserId==readerId).OrderByDescending(x=>x.SessionId).FirstAsync(ct);
        await db.UserSessions.Where(x=>x.SessionId==session.SessionId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RevokedAt,DateTime.UtcNow),ct);
        await Get(List(),reader,401);reader=await login("viewer");
        session=await db.UserSessions.AsNoTracking().Where(x=>x.UserId==readerId).OrderByDescending(x=>x.SessionId).FirstAsync(ct);
        await db.UserSessions.Where(x=>x.SessionId==session.SessionId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UtcNow.AddSeconds(-1)),ct);
        await Get(Detail(rooms[0].RoomId),reader,401);reader=await login("viewer");
        foreach(var scenario in new[]{"writer-first","cancel","timeout"})
        {
            await using var holder=open(null);await holder.Database.OpenConnectionAsync(ct);
            var ownerSpid=await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
            await using var tx=await holder.Database.BeginTransactionAsync(ct);
            await holder.Rooms.Where(x=>x.RoomId==rooms[0].RoomId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Description,"Authoritative writer value"),ct);
            using var stop=CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pending=Send(Detail(rooms[0].RoomId),reader,stop.Token);
            try
            {
                var observed=false;
                for(var i=0;i<180&&!pending.IsCompleted;i++)
                {
                    observed=await db.Database.SqlQuery<int>($"SELECT CONVERT(int,COUNT(*)) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={ownerSpid} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct)>0;
                    if(observed)break;await Task.Delay(35,ct);
                }
                check(observed,"UC023 production HTTPS "+scenario+" actual DMV SQL lock wait");Waits.Add(new{scenario,ownerSpid,observed});
                if(scenario=="writer-first")
                {
                    await tx.CommitAsync(ct);using var response=await pending;response.EnsureSuccessStatusCode();
                    using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                    check(json.RootElement.GetProperty("description").GetString()=="Authoritative writer value","UC023 HTTPS detail authoritative reread after SQL wait");
                }
                else if(scenario=="cancel")
                {
                    stop.Cancel();var cancelled=false;try{using var response=await pending;}catch(OperationCanceledException){cancelled=true;}
                    check(cancelled,"UC023 real HTTPS waiting cancellation propagates");
                    await using var probe=open(null);probe.Database.SetCommandTimeout(3);await using var ptx=await probe.Database.BeginTransactionAsync(ct);
                    await new UserEligibilityLock(probe).LockAsync([readerId],ct);
                    check(holder.Database.CurrentTransaction!=null,"UC023 canceled HTTP releases User transaction before feature blocker is released");
                }
                else
                {
                    using var response=await pending;check((int)response.StatusCode==503,"UC023 actual HTTP command timeout maps503 without partial data");
                    var text=await response.Content.ReadAsStringAsync(ct);check(!text.Contains("T67_00")&&!text.Contains("SqlException"),"UC023 SQL timeout response contains no partial room or provider text");
                }
            }
            finally{stop.Cancel();if(holder.Database.CurrentTransaction!=null)await tx.RollbackAsync(CancellationToken.None);try{using var response=await pending;}catch(OperationCanceledException){}}
        }
        await DetailSqlConsistency(db,open,users["other"].UserId,branch,rooms[0].RoomId,check,ct);
    }

    private static async Task DetailSqlConsistency(HotelOpsDbContext setup,Func<DbCommandInterceptor?,HotelOpsDbContext> open,long actor,long branch,long room,Action<bool,string> check,CancellationToken ct)
    {
        var raw=SessionCredential.Generate();var now=DateTime.UtcNow;
        var row=new UserSession{UserId=actor,SessionKeyHash=SessionCredential.Hash(raw),CreatedAt=now,LastUsedAt=now,ExpiresAt=now.AddMinutes(30)};
        setup.Add(row);await setup.SaveChangesAsync(ct);setup.ChangeTracker.Clear();
        var ticket=SessionReference.FromProtectedTicket(actor,raw,false,now);
        var snapshot=new SessionSnapshot(row.SessionId,actor,now,row.ExpiresAt,now.AddHours(12),false);
        RoomDirectoryService Service(HotelOpsDbContext db)
        {
            var context=new DefaultHttpContext{User=HotelOpsCookieTicket.Create(new(ticket,snapshot),TimeProvider.System).Principal};
            context.Items[typeof(SessionSnapshot)]=snapshot;var current=new CurrentUserAccessor(new Context{HttpContext=context});
            return new(db,current,new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),
                new PermissionService(current,new EfPermissionDataSource(db),new UnresolvedPermissionResourceResolver(),TimeProvider.System));
        }
        var hook=new HistoryProbe();await using var reader=open(hook);await reader.Database.OpenConnectionAsync(ct);
        var readerSpid=await reader.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
        var pending=Service(reader).HistoryAsync(ticket,branch,room,1,100,ct);
        await hook.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10),ct);
        await using var writer=open(null);await writer.Database.OpenConnectionAsync(ct);
        var writerSpid=await writer.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
        var append=writer.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_status_history(room_id,to_status,trigger_type,changed_at,reason) VALUES({room},{"OUT_OF_SERVICE"},{"MANUAL_CORRECTION"},{DateTime.UtcNow},{"Concurrent history fixture"})",ct);
        try
        {
            var observed=false;
            for(var i=0;i<160&&!append.IsCompleted;i++)
            {
                observed=await setup.Database.SqlQuery<int>($"SELECT CONVERT(int,COUNT(*)) AS [Value] FROM sys.dm_exec_requests WHERE session_id={writerSpid} AND blocking_session_id={readerSpid} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct)>0;
                if(observed)break;await Task.Delay(35,ct);
            }
            check(observed,"UC023 history count retains range while concurrent append waits (DMV)");Waits.Add(new{scenario="history-count-page",readerSpid,writerSpid});
            hook.Release.TrySetResult();var result=await pending;await append;
            check(result.History?.TotalCount==3&&result.History.Items.Count==3&&reader.Database.CurrentTransaction==null&&!reader.ChangeTracker.Entries().Any(),"UC023 actual service history count/page consistent, clean transaction/tracker");
            await using var next=open(null);check((await Service(next).HistoryAsync(ticket,branch,room,1,100,ct)).History?.TotalCount==4,"UC023 next history request sees committed append");
            check(hook.SourceReads<=3,"UC023 source validation uses fixed batches, not per-event queries");
        }
        finally{hook.Release.TrySetResult();try{await pending;}catch{}try{await append;}catch{}}
        foreach(var scenario in new[]{"session-expiry","grant-expiry"})
        {
            if(scenario=="session-expiry")await setup.UserSessions.Where(x=>x.SessionId==row.SessionId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UtcNow.AddSeconds(3)),ct);
            else await setup.UserRoles.Where(x=>x.UserId==actor&&x.Role.RoleCode=="T67_GLOBAL").ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UtcNow.AddSeconds(3)),ct);
            var expiryHook=new HistoryProbe();await using var context=open(expiryHook);
            var reading=Service(context).HistoryAsync(ticket,branch,room,1,100,ct);
            try
            {
                await expiryHook.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10),ct);await Task.Delay(4000,ct);
                expiryHook.Release.TrySetResult();var result=await reading;
                check(result.Status==(scenario=="session-expiry"?RoomDirectoryStatus.SessionInvalid:RoomDirectoryStatus.Forbidden)
                    &&result.History==null&&context.Database.CurrentTransaction==null&&!context.ChangeTracker.Entries().Any(),
                    "UC023 new history path final "+scenario+" withholds materialized history and releases transaction");
            }
            finally
            {
                expiryHook.Release.TrySetResult();try{await reading;}catch{}
                await setup.UserSessions.Where(x=>x.SessionId==row.SessionId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,row.ExpiresAt),ct);
                await setup.UserRoles.Where(x=>x.UserId==actor&&x.Role.RoleCode=="T67_GLOBAL").ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,(DateTime?)null),ct);
            }
        }
    }
    private sealed class Context:IHttpContextAccessor{public HttpContext? HttpContext{get;set;}}
    private sealed class HistoryProbe:DbCommandInterceptor
    {
        internal TaskCompletionSource Entered{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int SourceReads;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<DbDataReader> result,CancellationToken ct=default)
        {
            if(command.CommandText.Contains("UC023:HistoryPage")){Entered.TrySetResult();await Release.Task.WaitAsync(ct);}
            if(command.CommandText.Contains("FROM [room_blocks]")||command.CommandText.Contains("FROM [room_inspections]")||command.CommandText.Contains("FROM [cleaning_tasks]"))SourceReads++;
            return result;
        }
    }
}
