using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Task73WorkflowCases
{
    internal static List<string> Receipts { get; } = [];
    internal static async Task Deploy(HotelOpsDbContext db, string stage, bool expected, Action<bool,string> check, CancellationToken ct)
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../"));
        var receipt=Path.Combine(root,"obj","task73-hk-"+stage+"-"+Guid.NewGuid().ToString("N")+".local.json");
        var start=new ProcessStartInfo("powershell"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root};
        foreach(var argument in new[]{"-NoProfile","-File",Path.Combine(root,"scripts/database/release/housekeeping/Run-Housekeeping.ps1"),"-Stage",stage,"-Database",db.Database.GetDbConnection().Database,"-Receipt",receipt})start.ArgumentList.Add(argument);
        using var process=Process.Start(start)!;
        try { var stdout=process.StandardOutput.ReadToEndAsync(ct);var stderr=process.StandardError.ReadToEndAsync(ct);await process.WaitForExitAsync(ct);
            await File.WriteAllTextAsync(receipt+".log",await stdout+await stderr,ct);using var json=JsonDocument.Parse(await File.ReadAllTextAsync(receipt,ct));
            Receipts.Add(Path.GetRelativePath(root,receipt).Replace('\\','/'));check((process.ExitCode==0&&json.RootElement.GetProperty("success").GetBoolean())==expected,"Housekeeping real deployment "+stage+" expected="+expected+" "+Path.GetFileName(receipt)); }
        finally { if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync(CancellationToken.None);} }
    }
    internal static async Task RunAsync(HotelOpsDbContext db,Func<DbCommandInterceptor?,HotelOpsDbContext> open,HttpClient http,
        Func<string,Task<CookieJar>> login,IReadOnlyDictionary<string,User> users,long branch,long foreign,string root,Action<bool,string> check,CancellationToken ct)
    {
        if(await HousekeepingSchema.InstalledAsync(db,ct))await Deploy(db,"Rollback",true,check,ct);
        var preserved=await db.CleaningTasks.AsNoTracking().OrderBy(t=>t.CleaningTaskId).Select(t=>new{t.CleaningTaskId,t.Status,t.TaskNo}).ToArrayAsync(ct);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER T73_DdlFault ON DATABASE FOR CREATE_INDEX AS BEGIN IF COL_LENGTH(N'dbo.cleaning_tasks',N'workflow_status') IS NOT NULL THROW 51739,'Task73 post-column DDL fault',1; END",ct);
        try{await Deploy(db,"Apply",false,check,ct);check(!await HousekeepingSchema.InstalledAsync(db,ct),"genuine post-column DDL fault rolls back module/marker");}
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER T73_DdlFault ON DATABASE",ct);}
        await Deploy(db,"Apply",true,check,ct);await Deploy(db,"Apply",true,check,ct);await Deploy(db,"Verify",true,check,ct);
        check((await db.CleaningTasks.AsNoTracking().OrderBy(t=>t.CleaningTaskId).Select(t=>new{t.CleaningTaskId,t.Status,t.TaskNo}).ToArrayAsync(ct)).SequenceEqual(preserved),"populated legacy upgrade preserves source status and content");
        await Deploy(db,"Rollback",true,check,ct);await Deploy(db,"Apply",true,check,ct);
        var jar=await login("viewer");var denied=await login("denied");var admin=await login("admin");var other=await login("other");
        var supervisorRole=await db.Roles.Where(r=>r.RoleCode=="HK_SUPERVISOR").Select(r=>r.RoleId).SingleAsync(ct);
        db.Add(new UserRole{UserId=users["other"].UserId,RoleId=supervisorRole,BranchId=branch,IsActive=true,AssignedAt=DateTime.UtcNow.AddHours(-1)});
        db.Add(new UserBranchAccess{UserId=users["other"].UserId,BranchId=branch,AccessLevel="STANDARD",GrantedAt=DateTime.UtcNow.AddHours(-1)});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var anonymous=new CookieJar();using(var response=await http.GetAsync("/api/auth/csrf",ct)){anonymous.Read(response);using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));anonymous.Token=json.RootElement.GetProperty("csrfToken").GetString();}
        async Task<(JsonElement Body,string? Tag)> Send(HttpMethod method,string path,CookieJar who,int expected,object? body=null,string? tag=null,bool csrf=true)
        {
            using var request=new HttpRequestMessage(method,path);Headers(request,who);if(!csrf)request.Headers.Remove(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName);
            if(tag!=null)request.Headers.TryAddWithoutValidation("If-Match",tag);if(body!=null)request.Content=JsonContent.Create(body);
            using var response=await http.SendAsync(request,ct);var text=await response.Content.ReadAsStringAsync(ct);
            check((int)response.StatusCode==expected,"Task73 "+method+" "+path+" expected="+expected+" actual="+(int)response.StatusCode+(expected!=(int)response.StatusCode?" "+text:""));
            check(!new[]{"storageKey","passwordHash","SqlException","stackTrace"}.Any(s=>text.Contains(s,StringComparison.OrdinalIgnoreCase)),"workflow DTO hides storage/credentials/provider details");
            if(expected is 200 or 201)check(response.Headers.CacheControl?.NoStore==true,"workflow no-store");
            using var json=JsonDocument.Parse(text);return(json.RootElement.Clone(),response.Headers.ETag?.Tag);
        }
        var now=DateTime.UtcNow;var due=now.AddHours(2);var target=await db.CleaningTasks.SingleAsync(t=>t.TaskNo=="T73_TARGET",ct);
        var worker=await db.Users.SingleAsync(u=>u.EmployeeCode=="T73_A_worker",ct);
        await db.UserBranchAccesses.Where(a=>a.UserId==worker.UserId&&a.BranchId==branch).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,(DateTime?)null),ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET status={"IN_PROGRESS"},workflow_status={"PENDING_INSPECTION"} WHERE cleaning_task_id={target.CleaningTaskId}",ct);
        var assign=$"/api/branches/{branch}/cleaning-tasks/{target.CleaningTaskId}/inspections";
        object Assign(long inspector)=>new{inspectorId=inspector,dueAt=due,reason="Independent room quality check"};
        await Send(HttpMethod.Post,assign,anonymous,401,Assign(users["viewer"].UserId));await Send(HttpMethod.Post,assign,denied,403,Assign(users["viewer"].UserId));
        await Send(HttpMethod.Post,assign,jar,400,Assign(worker.UserId));
        var cleaner=await db.Users.Where(u=>u.EmployeeCode=="T73_CleanerInspector").Select(u=>u.UserId).SingleAsync(ct);
        await Send(HttpMethod.Post,assign,jar,400,Assign(cleaner));await Send(HttpMethod.Post,assign.Replace($"branches/{branch}",$"branches/{foreign}"),jar,403,Assign(users["viewer"].UserId));
        var assigned=await Send(HttpMethod.Post,assign,jar,201,Assign(users["viewer"].UserId));var id=assigned.Body.GetProperty("id").GetInt64();
        check(assigned.Body.GetProperty("notification").GetString()=="NOT_INTEGRATED","UC041 never reports unintegrated notification as delivered");
        await Send(HttpMethod.Post,assign,jar,409,Assign(users["viewer"].UserId));
        var route=$"/api/branches/{branch}/inspections/{id}";
        var read=await Send(HttpMethod.Get,route+"/execution",jar,200);
        var criterion=read.Body.GetProperty("criteria")[0].GetProperty("itemId").GetInt64();
        var version=read.Body.GetProperty("versionId").GetInt64();
        await db.InspectionChecklistVersions.Where(v=>v.VersionId==version).ExecuteUpdateAsync(s=>s.SetProperty(v=>v.Status,"RETIRED"),ct);
        object Reason()=>new{reason="Inspection performed by assigned inspector"};
        foreach(var path in new[]{route+"/start",route+"/finish",route+"/defects"})
        {
            var body=path.EndsWith("defects")?(object)new{itemId=criterion,severityId=1,description="Dust",location="Desk"}:Reason();
            await Send(HttpMethod.Post,path,anonymous,401,body,read.Tag);await Send(HttpMethod.Post,path,denied,403,body,read.Tag);
            await Send(HttpMethod.Post,path,jar,428,body);await Send(HttpMethod.Post,path,jar,400,body,"*");
            await Send(HttpMethod.Post,path.Replace($"branches/{branch}",$"branches/{foreign}"),jar,403,body,read.Tag);
        }
        await Send(HttpMethod.Get,route+"/execution",other,403);await Send(HttpMethod.Get,$"/api/branches/{branch}/inspections/{long.MaxValue}/execution",jar,404);
        await Send(HttpMethod.Post,route+"/start",jar,400,Reason(),read.Tag,false);
        await Fault("room_inspections",()=>Send(HttpMethod.Post,route+"/start",jar,500,Reason(),read.Tag));
        var started=await Send(HttpMethod.Post,route+"/start",jar,200,Reason(),read.Tag);
        check(started.Body.GetProperty("state").GetString()=="INSPECTING"&&started.Body.GetProperty("versionId").GetInt64()==version,"UC042 starts assigned immutable version even after retirement");
        await Send(HttpMethod.Post,route+"/start",jar,412,Reason(),read.Tag);
        await Send(HttpMethod.Post,route+"/finish",jar,400,Reason(),started.Tag);
        var bytes=Encoding.ASCII.GetBytes("%PDF-1.7\nTask73 private inspection evidence\n%%EOF");
        object Photo(string name="evidence.pdf",string? data=null)=>new{name,mimeType="application/pdf",base64=data??Convert.ToBase64String(bytes)};
        object Save(string outcome="FAIL",long? item=null,object[]? photos=null)=>new{reason="Record actual criterion outcome",results=new[]{new{itemId=item??criterion,outcome,note="Observed during inspection",photos=photos??Array.Empty<object>()}}};
        await Send(HttpMethod.Put,route+"/results",anonymous,401,Save(),started.Tag);await Send(HttpMethod.Put,route+"/results",denied,403,Save(),started.Tag);
        await Send(HttpMethod.Put,route+"/results",jar,400,Save("UNKNOWN"),started.Tag);await Send(HttpMethod.Put,route+"/results",jar,400,Save(item:long.MaxValue),started.Tag);
        await Send(HttpMethod.Put,route+"/results",jar,400,Save(photos:[Photo(data:"not-base64")]),started.Tag);
        await Send(HttpMethod.Put,route+"/results",jar,400,Save(photos:[Photo(name:"../escape.pdf")]),started.Tag);
        var fileCount=Directory.GetFiles(root).Length;var attachmentCount=await db.Attachments.CountAsync(ct);
        await Fault("room_inspections",()=>Send(HttpMethod.Put,route+"/results",jar,500,Save(photos:[Photo()]),started.Tag));
        check(Directory.GetFiles(root).Length==fileCount&&await db.Attachments.CountAsync(ct)==attachmentCount&&!await db.InspectionResults.AnyAsync(r=>r.InspectionId==id,ct),"UC042 audit fault rolls back result/photo links/metadata and removes owned private file");
        var saved=await Send(HttpMethod.Put,route+"/results",jar,200,Save(photos:[Photo()]),started.Tag);
        check(saved.Body.GetProperty("state").GetString()=="INSPECTING"&&saved.Body.GetProperty("criteria")[0].GetProperty("outcome").GetString()=="FAIL","UC042 Save Draft persists result without concluding inspection");
        await Send(HttpMethod.Post,route+"/finish",jar,400,Reason(),saved.Tag);
        var severity=await db.ReferenceValues.Where(r=>r.GroupCode=="SEVERITY"&&r.ValueCode=="MINOR").Select(r=>r.ReferenceId).SingleAsync(ct);
        object Defect(long? item=null,long? level=null,string description="Dust on desk",string location="Desk near window",object[]? photos=null)=>new{itemId=item??criterion,severityId=level??severity,description,location,photos=photos??Array.Empty<object>()};
        var passDraft=await Send(HttpMethod.Put,route+"/results",jar,200,Save("PASS"),saved.Tag);
        await Send(HttpMethod.Post,route+"/defects",jar,400,Defect(),passDraft.Tag);
        saved=await Send(HttpMethod.Put,route+"/results",jar,200,Save(),passDraft.Tag);
        await Send(HttpMethod.Post,route+"/defects",jar,400,Defect(description:" "),saved.Tag);await Send(HttpMethod.Post,route+"/defects",jar,400,Defect(location:" "),saved.Tag);
        await Send(HttpMethod.Post,route+"/defects",jar,400,Defect(item:long.MaxValue),saved.Tag);await Send(HttpMethod.Post,route+"/defects",jar,400,Defect(level:long.MaxValue),saved.Tag);
        await Send(HttpMethod.Post,route+"/defects",jar,400,Defect(photos:Enumerable.Range(0,6).Select(_=>Photo()).ToArray()),saved.Tag);
        await Send(HttpMethod.Post,route+"/defects",jar,400,Defect(photos:[Photo(data:Convert.ToBase64String(Encoding.UTF8.GetBytes("wrong magic")))]),saved.Tag);
        await Send(HttpMethod.Post,route+"/defects",jar,400,Defect(photos:[Photo(name:"evidence.exe")]),saved.Tag);
        await Send(HttpMethod.Post,route+"/defects",jar,400,Defect(photos:[Photo(data:Convert.ToBase64String(new byte[10*1024*1024+1]))]),saved.Tag);
        fileCount=Directory.GetFiles(root).Length;attachmentCount=await db.Attachments.CountAsync(ct);
        await Fault("room_inspections",()=>Send(HttpMethod.Post,route+"/defects",jar,500,Defect(photos:[Photo()]),saved.Tag));
        check(Directory.GetFiles(root).Length==fileCount&&await db.Attachments.CountAsync(ct)==attachmentCount&&!await db.InspectionDefects.AnyAsync(d=>d.InspectionId==id,ct),"UC043 audit failure rolls back defect/location/photo bridge/metadata and private bytes");
        var defected=await Send(HttpMethod.Post,route+"/defects",jar,200,Defect(photos:[Photo()]),saved.Tag);
        var defectId=defected.Body.GetProperty("defects")[0].GetProperty("id").GetInt64();
        check(defected.Body.GetProperty("defects")[0].GetProperty("location").GetString()=="Desk near window"&&defected.Body.GetProperty("defects")[0].GetProperty("evidenceIds").GetArrayLength()==1,"UC043 actual location and relational private photo association persisted");
        var download=defected.Body.GetProperty("evidence")[0].GetProperty("downloadPath").GetString()!;
        using(var request=new HttpRequestMessage(HttpMethod.Get,download)){Headers(request,jar);using var response=await http.SendAsync(request,ct);check((await response.Content.ReadAsByteArrayAsync(ct)).SequenceEqual(bytes)&&response.StatusCode==System.Net.HttpStatusCode.OK,"source-authorized private evidence HTTPS round-trip exact bytes");}
        await Send(HttpMethod.Get,download,denied,403);
        await Send(HttpMethod.Put,route+"/results",jar,400,Save("PASS"),defected.Tag);
        await Fault("room_inspections",()=>Send(HttpMethod.Post,route+"/finish",jar,500,Reason(),defected.Tag));
        var finished=await Send(HttpMethod.Post,route+"/finish",jar,200,Reason(),defected.Tag);
        check(finished.Body.GetProperty("state").GetString()=="PENDING_CONCLUSION"&&await db.RoomInspections.AnyAsync(i=>i.InspectionId==id&&i.Status=="IN_PROGRESS"&&i.Result==null,ct),"UC042 Finish preserves distinct Pending Conclusion, never auto-approves/rejects");
        await Send(HttpMethod.Put,route+"/results",jar,409,Save(),finished.Tag);
        var extra=await Send(HttpMethod.Post,route+"/defects",jar,200,Defect(description:"Additional described failure"),finished.Tag);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status={"FAILED"},status={"COMPLETED"},result={"FAIL"} WHERE inspection_id={id}",ct);
        var concluded=await Send(HttpMethod.Get,route+"/execution",jar,200);
        await Send(HttpMethod.Post,route+"/defects",jar,409,Defect(),concluded.Tag);await Send(HttpMethod.Post,route+"/start",jar,409,Reason(),concluded.Tag);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status={"REWORK_REQUIRED"},status={"IN_PROGRESS"} WHERE cleaning_task_id={target.CleaningTaskId}",ct);
        object Rework(long[]? defects=null)=>new{assigneeId=worker.UserId,dueAt=due,defectIds=defects??[defectId],reason="Repair described inspection defects"};
        await Send(HttpMethod.Post,route+"/reworks",jar,400,Rework([long.MaxValue]),concluded.Tag);
        await Fault("cleaning_reworks",()=>Send(HttpMethod.Post,route+"/reworks",jar,500,Rework(),concluded.Tag));
        var rework=await Send(HttpMethod.Post,route+"/reworks",jar,201,Rework(),concluded.Tag);
        check(rework.Body.GetProperty("notification").GetString()=="NOT_INTEGRATED","UC046 records no delivered notification without DEV2 adapter");
        await Send(HttpMethod.Post,route+"/reworks",jar,409,Rework(),concluded.Tag);
        await Deploy(db,"Rollback",false,check,ct);await Deploy(db,"Verify",true,check,ct);
        var oldTask=await db.CleaningTasks.SingleAsync(t=>t.TaskNo=="T73_OLD",ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET status={"PENDING"},workflow_status={"PENDING_ASSIGNMENT"} WHERE cleaning_task_id={oldTask.CleaningTaskId}",ct);
        await db.CleaningAssignments.Where(a=>a.CleaningTaskId==oldTask.CleaningTaskId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.AssignmentStatus,"COMPLETED").SetProperty(a=>a.EndedAt,now),ct);
        var options=await Send(HttpMethod.Get,$"/api/branches/{branch}/cleaning-tasks/{oldTask.CleaningTaskId}/assignment-options",jar,200);
        var assignedCleaning=await Send(HttpMethod.Post,$"/api/branches/{branch}/cleaning-tasks/{oldTask.CleaningTaskId}/assign",jar,200,new{assigneeId=worker.UserId,dueAt=due,reason="Assign eligible on-shift housekeeper"},options.Tag);
        check(assignedCleaning.Body.GetProperty("notification").GetString()=="NOT_INTEGRATED"&&await db.CleaningAssignments.AnyAsync(a=>a.CleaningTaskId==oldTask.CleaningTaskId&&a.AssigneeUserId==worker.UserId&&a.AssignmentStatus=="ACTIVE",ct),"UC029 business assignment persisted; notification remains honest PARTIAL");
        // Independent all-Pass path on another legitimate pending-inspection cycle.
        var originalRoom=await db.Rooms.AsNoTracking().SingleAsync(r=>r.RoomId==target.RoomId,ct);
        var legacyChecklist=await db.InspectionChecklists.Where(c=>c.ChecklistCode=="T72_LEGACY").Select(c=>c.ChecklistId).SingleAsync(ct);
        var typePath=$"/api/room-types/{originalRoom.RoomTypeId}?branchId={branch}";
        var typeRead=await Send(HttpMethod.Get,typePath,admin,200);
        await Send(HttpMethod.Put,typePath,admin,200,new{name="Legacy compatible inspection input",standardCleaningMinutes=30,defaultChecklistId=legacyChecklist},typeRead.Tag);
        var passRoom=new Room{BranchId=branch,FloorId=originalRoom.FloorId,RoomTypeId=originalRoom.RoomTypeId,RoomNumber="T73_PASS",IsActive=true,OperationalStatus="INSPECTION"};db.Add(passRoom);await db.SaveChangesAsync(ct);
        var passTask=new CleaningTask{TaskNo="T73_PASS_CYCLE",RoomId=passRoom.RoomId,CleaningTypeId=target.CleaningTypeId,Status="IN_PROGRESS",CreatedBy=users["viewer"].UserId,CreatedAt=now};db.Add(passTask);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status={"PENDING_INSPECTION"} WHERE cleaning_task_id={passTask.CleaningTaskId}",ct);
        var passAssigned=await Send(HttpMethod.Post,$"/api/branches/{branch}/cleaning-tasks/{passTask.CleaningTaskId}/inspections",jar,201,Assign(users["other"].UserId));
        var passRoute=$"/api/branches/{branch}/inspections/{passAssigned.Body.GetProperty("id").GetInt64()}";
        var passRead=await Send(HttpMethod.Get,passRoute+"/execution",other,200);var passItem=passRead.Body.GetProperty("criteria")[0].GetProperty("itemId").GetInt64();
        var passStart=await Send(HttpMethod.Post,passRoute+"/start",other,200,Reason(),passRead.Tag);
        var passSave=await Send(HttpMethod.Put,passRoute+"/results",other,200,Save("PASS",passItem),passStart.Tag);
        var passFinish=await Send(HttpMethod.Post,passRoute+"/finish",other,200,Reason(),passSave.Tag);
        check(passFinish.Body.GetProperty("state").GetString()=="PENDING_CONCLUSION"&&passFinish.Body.GetProperty("defects").GetArrayLength()==0,"UC042 all-mandatory-Pass path finishes without fabricating a defect or auto-approving");
        var legacyFrozen=false;try{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_checklist_items SET criterion={"Tampered legacy item"} WHERE item_id={passItem}",ct);}catch(SqlException error)when(error.Errors.Cast<SqlError>().Any(e=>e.Number==51731)){legacyFrozen=true;}
        check(legacyFrozen,"new assigned inspections freeze even legacy template criteria under approved compatibility guard");
        await Concurrency();

        void Headers(HttpRequestMessage request,CookieJar who){if(who.Cookies.Count>0)request.Headers.Add("Cookie",string.Join("; ",who.Cookies.Select(x=>x.Key+"="+x.Value)));if(who.Token!=null)request.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName,who.Token);}
        async Task Fault(string entity,Func<Task> operation)
        {
            if(entity is not("room_inspections" or "cleaning_reworks"))throw new InvalidOperationException("Closed fault allowlist");
            var before=await db.AuditLogs.CountAsync(ct);
            await db.Database.ExecuteSqlRawAsync(entity=="room_inspections"?"CREATE TRIGGER T73_AuditFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='room_inspections') THROW 51738,'Task73 audit fault',1; END":"CREATE TRIGGER T73_AuditFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='cleaning_reworks') THROW 51738,'Task73 audit fault',1; END",ct);
            try{await operation();check(await db.AuditLogs.CountAsync(ct)==before,"business/audit transaction fault leaves no audit");}finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.T73_AuditFault",ct);}
        }
        async Task Concurrency()
        {
            // Reopen only the disposable fixture through explicit SQL; this is not a claimed UC045/047 API.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status={"INSPECTING"},status={"IN_PROGRESS"},result=NULL WHERE inspection_id={id}",ct);
            var baseline=await Send(HttpMethod.Get,route+"/execution",jar,200);
            async Task<int> Race(){using var r=new HttpRequestMessage(HttpMethod.Post,route+"/defects");Headers(r,jar);r.Headers.TryAddWithoutValidation("If-Match",baseline.Tag);r.Content=JsonContent.Create(Defect(description:"Concurrent defect"));using var response=await http.SendAsync(r,ct);return(int)response.StatusCode;}
            var count=await db.InspectionDefects.CountAsync(d=>d.InspectionId==id,ct);
            check((await Task.WhenAll(Race(),Race())).Order().SequenceEqual(new[]{200,412})&&await db.InspectionDefects.CountAsync(d=>d.InspectionId==id,ct)==count+1,"UC043 same-rowversion race commits once; stale loser412");
            foreach(var action in new[]{"results","defects"})
            {
                baseline=await Send(HttpMethod.Get,route+"/execution",jar,200);
                await using var holder=open(null);await holder.Database.OpenConnectionAsync(ct);var owner=await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);await using var held=await holder.Database.BeginTransactionAsync(ct);
                await holder.RoomInspections.Where(i=>i.InspectionId==id).ExecuteUpdateAsync(s=>s.SetProperty(i=>i.SummaryNote,"Uncommitted contention fixture"),ct);
                using var stop=CancellationTokenSource.CreateLinkedTokenSource(ct);using var request=new HttpRequestMessage(action=="results"?HttpMethod.Put:HttpMethod.Post,route+"/"+action);Headers(request,jar);request.Headers.TryAddWithoutValidation("If-Match",baseline.Tag);request.Content=JsonContent.Create(action=="results"?Save():Defect());
                var pending=http.SendAsync(request,stop.Token);var api=0;
                for(var n=0;n<200&&!pending.IsCompleted;n++){var rows=await db.Database.SqlQuery<int>($"SELECT CONVERT(int,session_id) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={owner} AND database_id=DB_ID() AND wait_type LIKE 'LCK_M_%'").ToArrayAsync(ct);if(rows.Length>0){api=rows[0];break;}await Task.Delay(30,ct);}
                check(api>0,"actual SQL DMV wait during "+action);stop.Cancel();var canceled=false;try{using var response=await pending;}catch(OperationCanceledException){canceled=true;}
                var released=false;for(var n=0;n<200;n++){if(!await db.Database.SqlQuery<int>($"SELECT CONVERT(int,request_session_id) AS [Value] FROM sys.dm_tran_locks WHERE request_session_id={api} AND resource_database_id=DB_ID() AND request_owner_type='TRANSACTION'").AnyAsync(ct)){released=true;break;}await Task.Delay(30,ct);}
                check(canceled&&released,"actual HTTPS cancellation releases inspection SQL transaction before blocker rollback: "+action);await held.RollbackAsync(ct);
                var after=await Send(HttpMethod.Get,route+"/execution",jar,200);check(after.Tag==baseline.Tag,"canceled "+action+" preserves SQL rowversion");
            }
        }
    }
}
