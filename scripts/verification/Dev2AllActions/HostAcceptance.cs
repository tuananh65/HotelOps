using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.Data;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class HostAcceptance
{
    internal const string DatabaseGuid="6e68f686-d7c1-4002-94d9-a58fa73175da";
    internal static async Task VerifyIdentity(HotelOpsDbContext db)
    {
        if(await db.Database.SqlQueryRaw<int>("SELECT CAST(CASE WHEN DB_NAME()=N'HotelOps_DEV2_FullStack_Trial' AND CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=N'KIENPC' AND CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')) IS NULL THEN 1 ELSE 0 END AS int) AS [Value]").SingleAsync()!=1)throw new InvalidOperationException("Exact KIENPC default trial required");
        var guid=await db.Database.SqlQueryRaw<string>("SELECT LOWER(CONVERT(nvarchar(36),database_guid)) AS [Value] FROM sys.database_recovery_status WHERE database_id=DB_ID()").SingleAsync();
        if(guid!=DatabaseGuid)throw new InvalidOperationException("Trial database GUID mismatch");
    }
    internal static async Task Run(string output,string? address,bool fixtureOnly=false,bool focused=false,string focusAction="A004A-001",bool normalAuthority=false)
    {
        if(normalAuthority)
        {
            RequireNormalEnvironment();
            var startup=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(output,"NORMAL_API_STARTUP.json")))!;
            if(startup["hooksDisabled"]?.GetValue<bool>()!=true||startup["databaseGuid"]?.GetValue<string>()!=DatabaseGuid||startup["apiProcessId"]?.GetValue<int>() is not >0)
                throw new InvalidOperationException("Unhooked API startup evidence required");
            if(fixtureOnly||focused)throw new InvalidOperationException("Normal authority is a separate acceptance mode");
        }
        if(focused&&!AllowedFocus(focusAction))throw new InvalidOperationException("Only A001/A034/A059 focused acceptance is allowlisted");
        Directory.CreateDirectory(output);var url=fixtureOnly?null:new Uri(address??throw new InvalidOperationException("API address required"));
        if(!fixtureOnly&&(url!.Scheme!="https"||!url.IsLoopback))throw new InvalidOperationException("Trusted localhost HTTPS required");
        var cs=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DEV2_TRIAL_CONNECTION")??throw new InvalidOperationException("Host private connection required"));
        if(cs.InitialCatalog!=Dev2DemoPolicyIdentity.Database||!cs.IntegratedSecurity||cs.DataSource is not("KIENPC" or "lpc:KIENPC"))throw new InvalidOperationException("Exact local default trial Windows authentication required");
        var options=new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(cs.ConnectionString).Options;
        HotelOpsDbContext Open()=>new(options);
        await using var leaseDb=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(new SqlConnectionStringBuilder(cs.ConnectionString){Pooling=false}.ConnectionString).Options);
        await using(var db=Open())await VerifyIdentity(db);
        foreach(var name in new[]{"EF_SCHEMA_COMPATIBILITY.json","BACKUP_AND_ROLLBACK.json"})
        {
            var r=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(output,name)))!;
            if(!string.Equals(r["databaseGuid"]!.GetValue<string>(),DatabaseGuid,StringComparison.OrdinalIgnoreCase)
                ||(name.StartsWith("EF_")?r["status"]!.GetValue<string>()!="PASS":r["backupVerified"]!.GetValue<bool>()!=true))throw new InvalidOperationException("Target-bound schema and backup receipts required");
        }
        if(Environment.GetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1")!=Dev2ExpandedPolicyIdentity.Sha256)throw new InvalidOperationException("V2 policy required");
        var hooks=fixtureOnly||normalAuthority?Path.Combine(output,"hooks"):Environment.GetEnvironmentVariable("DEV2_TRIAL_HOOK_DIRECTORY")??throw new InvalidOperationException("Explicit trial-only verification hooks required");
        Directory.CreateDirectory(hooks);
        var fixtures=new Fixtures(Open,output);var checks=new List<object>();var actions=new List<object>();var clients=new List<HttpClient>();var tokens=new List<string>();var failed=false;var cleanup=false;string phase="fixture-create";
        FailureDiagnostics? diagnostic=null;ActionReceipt? active=null;var diagnostics=new List<FailureDiagnostics>();var failedActions=new HashSet<string>();var matrix=Dev2ActionAdmissionRegistry.KnownActions.ToDictionary(x=>x.DecisionId,x=>new ActionReceipt(x));
        Task Write(string name,object value)=>FixtureRunSafety.Write(Path.Combine(output,name),value);
        void Check(bool ok,string id,string test){checks.Add(new{id,test,pass=ok});if(active is not null){active.Phase=test;active.Checks.Add(new{test,pass=ok,expectedHttpStatus=active.ExpectedHttpStatus,actualHttpStatus=active.ActualHttpStatus,apiDiagnostic=active.ApiDiagnostic,correlationId=active.CorrelationId});}if(!ok)throw new AcceptanceFailure(test.Contains("HTTP")?"HTTP_STATUS":test.Contains("rollback")?"ROLLBACK":test.Contains("audit")?"AUDIT":test.Contains("locked")?"CONCURRENCY":"ASSERTION_FAILED");}
        var jars=new Dictionary<HttpClient,CookieContainer>();var loginEvidence=new List<object>();var restrictedLoginAt=DateTime.MinValue;
        HttpClient Client(){var jar=new CookieContainer();var client=new HttpClient(new HttpClientHandler{CookieContainer=jar,AllowAutoRedirect=false}){BaseAddress=url,Timeout=TimeSpan.FromSeconds(65)};jars.Add(client,jar);return client;}
        bool HasAuthCookie(HttpClient client,Uri target)=>jars[client].GetCookies(target).Cast<Cookie>().Any(x=>x.Name==HotelOpsCookieTicket.CookieName&&!x.Expired);
        async Task<(int status,JsonNode? body)> Send(TrialCase c,int actor,string? header=null,bool csrf=true,JsonObject? body=null)
        {
            if(normalAuthority&&header is not null)throw new InvalidOperationException("Fault/barrier headers forbidden in normal authority mode");
            using var request=new HttpRequestMessage(new HttpMethod(c.Identity.HttpMethod),c.Url);
            if(c.Body is not null)request.Content=JsonContent.Create(body??c.Body);
            if(csrf&&actor>=0&&c.Body is not null)request.Headers.Add(HotelOpsAntiforgery.HeaderName,tokens[actor]);
            if(header is not null)request.Headers.Add(header,"1");
            var auditProbe=header=="X-Dev2-Audit-Fault"&&c.Identity.DecisionId=="A004A-059"?Guid.NewGuid().ToString("N"):null;
            if(auditProbe is not null)request.Headers.Add("X-Dev2-Audit-Probe",auditProbe);
            var probe=actor==6&&!normalAuthority?Guid.NewGuid().ToString("N"):null;if(probe is not null)request.Headers.Add("X-Dev2-Auth-Probe",probe);
            using var anonymous=actor<0?Client():null;
            if(actor<0&&c.Body is not null&&csrf)
            {
                using var anti=await anonymous!.GetAsync("/api/auth/csrf");anti.EnsureSuccessStatusCode();
                var antiBody=await anti.Content.ReadFromJsonAsync<JsonElement>();
                request.Headers.Add(HotelOpsAntiforgery.HeaderName,antiBody.GetProperty("csrfToken").GetString());
            }
            using var response=await (actor<0?anonymous!:clients[actor]).SendAsync(request);
            if(probe is not null)
            {
                var observation=Path.Combine(hooks,"auth-"+probe+".json");
                // Response bytes may arrive just before the outer observer finishes its receipt.
                for(var retry=0;retry<40&&!File.Exists(observation);retry++)await Task.Delay(25);
                var receipt=JsonNode.Parse(await File.ReadAllTextAsync(observation))!;
                await Write("RESTRICTED_REQUEST_COOKIE.json",new{decisionId=c.Identity.DecisionId,serverObservation=receipt,actualStatus=(int)response.StatusCode,secretValuesRecorded=false});
                Check(receipt["cookieReceived"]!.GetValue<bool>()&&receipt["authenticated"]!.GetValue<bool>()&&receipt["sqlSnapshotConfirmed"]!.GetValue<bool>()&&receipt["isRestricted"]!.GetValue<bool>(),c.Identity.DecisionId,"restricted Cookie received and SQL session confirmed");
            }
            if(active is not null){active.ActualHttpStatus=(int)response.StatusCode;active.Actor=actor<0?"ANONYMOUS":"fixture-user-"+actor;active.ApiDiagnostic="NONE";active.CorrelationId=null;}
            var content=await response.Content.ReadAsStringAsync();JsonNode? json=null;try{json=JsonNode.Parse(content);}catch(JsonException){}
            if(active is not null&&json is JsonObject problem&&(int)response.StatusCode>=400)
            {
                var detail=problem["detail"] is JsonValue value&&value.TryGetValue<string>(out var text)?text:null;
                var authCode=problem["code"]?.GetValue<string>();
                active.ApiDiagnostic=authCode is "SESSION_INVALID" or "AUTHENTICATION_REQUIRED" or "PASSWORD_CHANGE_REQUIRED" or "FORBIDDEN" or "CSRF_VALIDATION_FAILED"?authCode:detail is not null&&ValidationDiagnostics.Known.TryGetValue(detail,out var reference)?reference:"UNCLASSIFIED_NO_PAYLOAD_RECORDED";
                if(problem["traceId"] is JsonValue trace&&trace.TryGetValue<string>(out var correlation)&&System.Text.RegularExpressions.Regex.IsMatch(correlation,"^[0-9a-fA-F:-]{16,128}$"))active.CorrelationId=correlation;
            }
            if(auditProbe is not null)
            {
                var path=Path.Combine(hooks,"audit-"+auditProbe+".json");
                for(var retry=0;retry<40&&!File.Exists(path);retry++)await Task.Delay(25);
                var boundary=File.Exists(path)?JsonNode.Parse(await File.ReadAllTextAsync(path)):null;
                await Write("FIX04_A059_AUDIT_BOUNDARY.json",new{decisionId=c.Identity.DecisionId,httpStatus=(int)response.StatusCode,boundaryReached=boundary is not null,observation=boundary});
                if((int)response.StatusCode is >=500 and <=599)Check(boundary?["inventoryAuditPending"]?.GetValue<bool>()==true&&boundary?["postedDocumentTracked"]?.GetValue<bool>()==true&&boundary?["transactionActive"]?.GetValue<bool>()==true,c.Identity.DecisionId,"actual inventory audit-fault boundary reached");
            }
            return ((int)response.StatusCode,json);
        }
        async Task Deny(TrialCase c,int actor,string test,bool csrf=true,JsonObject? body=null)
        {
            if(active is not null){active.Phase=test;active.ExpectedHttpStatus=test=="missing-csrf"?"400":actor<0?"401":"403";}
            var before=await fixtures.Snapshot();var result=await Send(c,actor,csrf:csrf,body:body);
            Check(before==await fixtures.Snapshot(),c.Identity.DecisionId,test+" no business/audit effects");
            Check(test=="missing-csrf"?result.status==400:actor<0?result.status==401:result.status==403,c.Identity.DecisionId,test+" HTTP");
            if(actor==6)Check(result.body?["code"]?.GetValue<string>()=="PASSWORD_CHANGE_REQUIRED",c.Identity.DecisionId,"restricted exact Auth problem code");
        }
        async Task SetRole(int actor,bool active)
        {await using var db=Open();await db.UserRoles.Where(x=>x.UserId==fixtures.Users[actor].UserId&&x.RoleId==fixtures.MainRole.RoleId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,active));}
        async Task Login(int actor,bool replace=false)
        {
            var client=Client();if(replace){clients[actor].Dispose();clients[actor]=client;}else clients.Add(client);
            using var anti=await client.GetAsync("/api/auth/csrf");anti.EnsureSuccessStatusCode();var token=(await anti.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("csrfToken").GetString()!;
            using var req=new HttpRequestMessage(HttpMethod.Post,"/api/auth/login"){Content=JsonContent.Create(new{username=fixtures.Users[actor].Username,password=fixtures.Password})};req.Headers.Add(HotelOpsAntiforgery.HeaderName,token);
            using var response=await client.SendAsync(req);
            var issued=response.Headers.TryGetValues("Set-Cookie",out var values)&&values.Any(x=>x.StartsWith(HotelOpsCookieTicket.CookieName+"=",StringComparison.Ordinal));
            var eligible=HasAuthCookie(client,new Uri(url!,"/api/assets/1/handovers"));
            Check(response.StatusCode==HttpStatusCode.OK,"LOGIN","fixture-user-"+actor);
            var session=await response.Content.ReadFromJsonAsync<JsonElement>();
            var restricted=session.GetProperty("mustChangePassword").GetBoolean();
            loginEvidence.Add(new{actor="fixture-user-"+actor,loginHttp=(int)response.StatusCode,authCookieIssued=issued,authCookieEligible=eligible,isRestricted=restricted});await Write("FIX03_LOGIN_SESSION_EVIDENCE.json",loginEvidence);
            Check(issued&&eligible,"LOGIN","Cookie issued and eligible for business URL");Check(restricted==(actor==6),"LOGIN","Server returned expected restriction");
            var newToken=session.GetProperty("csrfToken").GetString()!;if(replace)tokens[actor]=newToken;else tokens.Add(newToken);
            using var probe=await client.GetAsync("/api/auth/session");
            Check(probe.StatusCode==HttpStatusCode.OK,"SESSION","Cookie roundtrip fixture-user-"+actor);
            var confirmed=await probe.Content.ReadFromJsonAsync<JsonElement>();Check(confirmed.GetProperty("mustChangePassword").GetBoolean()==(actor==6),"SESSION","Confirmed server restriction");
            // Session endpoint can issue a new identity-bound antiforgery token.
            tokens[actor]=confirmed.GetProperty("csrfToken").GetString()!;
            loginEvidence.Add(new{actor="fixture-user-"+actor,sessionHttp=(int)probe.StatusCode,isRestricted=actor==6,cookieRoundtripConfirmed=true});await Write("FIX03_LOGIN_SESSION_EVIDENCE.json",loginEvidence);
            if(actor==6)restrictedLoginAt=DateTime.UtcNow;
        }
        void ResetBarrier(string name){foreach(var ext in new[]{"waiting","release"}){var path=Path.Combine(hooks,name+"."+ext);if(File.Exists(path))File.Delete(path);}}
        async Task WaitBarrier(string name,Task pending)
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(18));
            while(!File.Exists(Path.Combine(hooks,name+".waiting"))){if(pending.IsCompleted)throw new InvalidOperationException("Verification hook did not execute");await Task.Delay(25,timeout.Token);}
        }
        try
        {
            phase="orphan-preflight";await FixtureRunSafety.Gate(leaseDb,output);
            await FixtureRunSafety.State(output,fixtures.Marker,"FIXTURE_CREATE_STARTED");
            await fixtures.Create();await File.WriteAllLinesAsync(Path.Combine(hooks,"fixture-users.txt"),fixtures.UserIds.Select(x=>x.ToString()));
            await FixtureRunSafety.State(output,fixtures.Marker,"FIXTURE_COMMITTED");
            if(fixtureOnly){phase="fixture-smoke-created";return;}
            phase="real-cookie-login";
            for(var actor=0;actor<fixtures.Users.Length;actor++)await Login(actor);
            // Mutations first populate actual histories, fulfillment and receipt links used by later GET positives.
            foreach(var known in Dev2ActionAdmissionRegistry.KnownActions.OrderBy(x=>x.HttpMethod=="GET"?1:0).ThenBy(x=>x.DecisionId))
            {
                // A034 needs a real posted material issue; execute A051 through its full acceptance path.
                if(focused&&known.DecisionId!=focusAction&&!(focusAction=="A004A-034"&&known.DecisionId=="A004A-051"))continue;
                // A fresh credential login, never extend or edit a SQL session to hide expiry.
                active=null;if(DateTime.UtcNow-restrictedLoginAt>TimeSpan.FromMinutes(10))await Login(6,replace:true);
                phase=known.DecisionId;active=matrix[known.DecisionId];active.FixtureId=fixtures.Marker;
                await FixtureRunSafety.State(output,fixtures.Marker,"ACTION_STARTED",known.DecisionId);
                var dependency=known.DecisionId switch{"A004A-012"=>"A004A-018","A004A-013"=>"A004A-016","A004A-034" or "A004A-041" or "A004A-042"=>"A004A-051","A004A-054"=>"A004A-057","A004A-055"=>"A004A-058",_=>null};
                if(dependency is not null&&failedActions.Contains(dependency)){active.Status="SKIPPED_DEPENDENCY";active.FailureCategory="FAILED_PREREQUISITE_"+dependency;continue;}
                var startSnapshot=await fixtures.Snapshot();var positivePhase=false;active.Status="RUNNING";
                try
                {
                active.Phase="case-construction";var c=await fixtures.Case(known);var mutation=c.Body is not null;
                if(known.DecisionId=="A004A-059"){active.Phase="preconditions:before-negatives";await fixtures.VerifyReceiptPostingPreconditions(c,"before-negatives");}
                active.ExpectedHttpStatus=mutation?"200 or201":"200";
                // For anonymous unsafe calls provide a valid anonymous antiforgery token so auth is tested independently.
                await Deny(c,-1,"anonymous");
                foreach(var actor in new[]{3,4,5,6})
                {
                    var body=c.Body?.DeepClone().AsObject();if(body is not null)body[known.DecisionId=="A004A-021"?"reportedBy":"actorUserId"]=fixtures.Users[actor].UserId;
                    await Deny(c,actor,new[]{"no-grant","wrong-branch","own-excluded","restricted"}[actor-3],body:body);
                }
                foreach(var code in Dev2DemoBindings.PermissionCodes(known.DecisionId))
                {
                    await using var scope=Open();var permission=await scope.Permissions.Where(x=>x.PermissionCode==code).Select(x=>x.PermissionId).SingleAsync();
                    try{await scope.RolePermissions.Where(x=>x.RoleId==fixtures.MainRole.RoleId&&x.PermissionId==permission).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.DataScope,"OWN"));await Deny(c,c.Actor,"required operand excluded: "+code);}
                    finally{await scope.RolePermissions.Where(x=>x.RoleId==fixtures.MainRole.RoleId&&x.PermissionId==permission).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.DataScope,"BRANCH"));}
                }
                await using(var expires=Open())
                {
                    try{await expires.UserRoles.Where(x=>x.UserId==fixtures.Users[c.Actor].UserId&&x.RoleId==fixtures.MainRole.RoleId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UtcNow.AddMinutes(-1)));await Deny(c,c.Actor,"expired role assignment");}
                    finally{await expires.UserRoles.Where(x=>x.UserId==fixtures.Users[c.Actor].UserId&&x.RoleId==fixtures.MainRole.RoleId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,(DateTime?)null));}
                }
                if(known.DecisionId=="A004A-042")await Deny(c with{Url=c.Url.Replace("/"+fixtures.Requests[51].RequestId+"/fulfillments/","/"+fixtures.Requests[50].RequestId+"/fulfillments/")},c.Actor,"nested parent mismatch");
                if(mutation)
                {
                    var spoof=c.Body!.DeepClone().AsObject();spoof[known.DecisionId=="A004A-021"?"reportedBy":"actorUserId"]=fixtures.Users[3].UserId;await Deny(c,c.Actor,"actor-spoof",body:spoof);await Deny(c,c.Actor,"missing-csrf",csrf:false);
                    if(int.Parse(known.DecisionId[^3..]) is 4 or 5 or >=26 and <=32 or >=44 and <=50 or 51 or 59)
                    {
                        var other=int.Parse(known.DecisionId[^3..]) is 51 or 59?1:c.Actor==0?1:0;var delegated=c.Body.DeepClone().AsObject();delegated["actorUserId"]=fixtures.Users[other].UserId;
                        // SOD/ownership applies to 44/45/49, approval/close, execute/review, receiver and posting.
                        await Deny(c,other,"ownership-or-sod",body:delegated);
                    }
                    if(c.Body.ContainsKey("expectedRowVersion"))
                    {
                        active.ExpectedHttpStatus="400";var stale=c.Body.DeepClone().AsObject();stale["expectedRowVersion"]=Convert.ToBase64String(new byte[8]);var before=await fixtures.Snapshot();var result=await Send(c,c.Actor,body:stale);Check(result.status==400,known.DecisionId,"stale-version rejected");Check(before==await fixtures.Snapshot(),known.DecisionId,"stale-version rollback");
                    }
                    if(known.DecisionId=="A004A-059")
                    {
                        active.ExpectedHttpStatus="400";var stale=c.Body!.DeepClone().AsObject();stale["receiptExpectedRowVersion"]=Convert.ToBase64String(new byte[8]);var before=await fixtures.Snapshot();var rejected=await Send(c,c.Actor,body:stale);Check(before==await fixtures.Snapshot(),known.DecisionId,"stale receipt version no effects");Check(rejected.status==400,known.DecisionId,"stale receipt version rejected");
                        await fixtures.VerifyReceiptPostingPreconditions(c,"before-audit-fault");
                    }
                    if(!normalAuthority){active.ExpectedHttpStatus="500-599";var faultBefore=await fixtures.Snapshot();var fault=await Send(c,c.Actor,"X-Dev2-Audit-Fault");Check(faultBefore==await fixtures.Snapshot(),known.DecisionId,"full business/stock/audit rollback");Check(fault.status is >=500 and <=599,known.DecisionId,"injected audit failure");}
                }
                // Revoke AFTER Stage A, BEFORE Stage B. Each action must reread actual grant paths.
                if(!normalAuthority){
                active.ExpectedHttpStatus="403";ResetBarrier("admission");var revBefore=await fixtures.Snapshot();var revoked=Send(c,c.Actor,"X-Dev2-Admission-Barrier");
                try{await WaitBarrier("admission",revoked);await SetRole(c.Actor,false);await File.WriteAllTextAsync(Path.Combine(hooks,"admission.release"),"RELEASE");var denied=await revoked;Check(denied.status==403,known.DecisionId,"mid-request grant revocation");Check(revBefore==await fixtures.Snapshot(),known.DecisionId,"revocation no business effects");}
                finally{await File.WriteAllTextAsync(Path.Combine(hooks,"admission.release"),"RELEASE");await SetRole(c.Actor,true);}
                }
                // Positive business request, holding a concurrent revocation until commit for each mutation.
                positivePhase=true;active.ExpectedHttpStatus=mutation?"200 or201":"200";active.Phase="positive business request";
                var beforePositive=await fixtures.Snapshot();long auditId;await using(var db=Open())auditId=await db.AuditLogs.MaxAsync(x=>(long?)x.AuditId)??0;
                (int status,JsonNode? body) positive;
                if(mutation&&!normalAuthority)
                {
                    ResetBarrier("save");var saving=Send(c,c.Actor,"X-Dev2-Save-Barrier");await WaitBarrier("save",saving);var revoke=SetRole(c.Actor,false);
                    try{await Task.Delay(200);Check(!revoke.IsCompleted,known.DecisionId,"grant row remains locked through save/commit");await File.WriteAllTextAsync(Path.Combine(hooks,"save.release"),"RELEASE");positive=await saving;await revoke;}
                    finally{await File.WriteAllTextAsync(Path.Combine(hooks,"save.release"),"RELEASE");await SetRole(c.Actor,true);}
                }
                else positive=await Send(c,c.Actor);
                Check(positive.status is 200 or 201&&positive.body is not null,known.DecisionId,"positive HTTP + DTO");
                if(mutation)
                {
                    Check(beforePositive!=await fixtures.Snapshot(),known.DecisionId,"persisted feature effect");await fixtures.VerifyEffect(known.DecisionId,positive.body!);
                    if(known.DecisionId=="A004A-059")await fixtures.VerifyReceiptPostingEffect(c);
                    await using var db=Open();var audits=await db.AuditLogs.Where(x=>x.AuditId>auditId&&x.BranchId==fixtures.Branch.BranchId).Select(x=>x.ActorUserId).ToListAsync();Check(audits.Count>0&&audits.All(x=>x==fixtures.Users[c.Actor].UserId),known.DecisionId,"fresh trusted actor audit");
                    if(known.DecisionId=="A004A-051")fixtures.FulfillmentId=positive.body!["fulfillmentId"]!.GetValue<long>();
                }
                else {Check(beforePositive==await fixtures.Snapshot(),known.DecisionId,"read has no feature effects");await fixtures.VerifyRead(known.DecisionId,positive.body!);}
                if(known.DecisionId=="A004A-034")
                {
                    var pagingResults=new List<object>();
                    foreach(var test in new[]{("",1,20,200),("?page=1&pageSize=1",1,1,200),("?page=2",2,20,200),("?pageSize=1",1,1,200),("?page=0",0,20,400),("?pageSize=0",1,0,400),("?pageSize=101",1,101,400),("?page=2147483647&pageSize=100",int.MaxValue,100,400),("?page=invalid",0,20,400)})
                    {
                        var snapshot=await fixtures.Snapshot();active.ExpectedHttpStatus=test.Item4.ToString();
                        var paged=await Send(c with{Url=c.Url+test.Item1},c.Actor);
                        Check(paged.status==test.Item4,known.DecisionId,"pagination HTTP "+test.Item1);
                        Check(snapshot==await fixtures.Snapshot(),known.DecisionId,"pagination no business/audit effects "+test.Item1);
                        if(test.Item4==200)await fixtures.VerifyMaterialAvailability(paged.body!,test.Item2,test.Item3);
                        pagingResults.Add(new{query=test.Item1,expectedStatus=test.Item4,actualStatus=paged.status,noBusinessEffects=true,dtoVerified=test.Item4==200});
                        await Write("FIX07_A034_PAGINATION_SQL.json",new{status="RUNNING",cases=pagingResults});
                    }
                    await Write("FIX07_A034_PAGINATION_SQL.json",new{status="PASS",cases=pagingResults});
                    active.ExpectedHttpStatus="200";active.ActualHttpStatus=positive.status;active.ApiDiagnostic="NONE";
                }
                active.Status="PASS";active.AuthorizationResult="PASS";active.BusinessResult="PASS";active.AuditResult=mutation?"PASS":"NOT_APPLICABLE";active.ConcurrencyResult=mutation?"PASS":"NOT_APPLICABLE";active.RollbackResult=mutation?"PASS":"NOT_APPLICABLE";active.ActualDatabaseEffect="Verified exact persisted effect/read scope; checks listed below";
                if(normalAuthority){active.ConcurrencyResult=mutation?"NOT_RUN_NORMAL_MODE":"NOT_APPLICABLE";active.RollbackResult=mutation?"NOT_RUN_FAULT_ROLLBACK_NORMAL_MODE":"NOT_APPLICABLE";}
                actions.Add(new{id=known.DecisionId,positive=true,negative=true,sql=true,auditRollback=mutation&&!normalAuthority?"PASS":normalAuthority?"NOT_RUN_NORMAL_MODE":"NOT_APPLICABLE",transactionRevocation=mutation&&!normalAuthority?"PASS":normalAuthority?"NOT_RUN_NORMAL_MODE":"NOT_APPLICABLE"});await Write("ACTION_ACCEPTANCE_PROGRESS.json",actions);await Write("FULL61_ACTION_ACCEPTANCE.json",matrix.Values);
                await FixtureRunSafety.State(output,fixtures.Marker,"ACTION_PASS",known.DecisionId);
                }
                catch(Exception error)
                {
                    failed=true;failedActions.Add(known.DecisionId);active.Status="FAIL";
                    var detail=FailureDiagnostics.Capture(error,known.DecisionId+" / "+active.Phase,null,null,null,output);detail.TransactionCommitted=fixtures.Committed;active.FailureCategory=detail.FailureCategory;diagnostics.Add(detail);
                    await Write("ACTION_FAILURE_DIAGNOSTICS.json",diagnostics);await Write("FULL61_ACTION_ACCEPTANCE.json",matrix.Values);
                    // Only a rolled-back business400 with unchanged feature graph is an independent failure.
                    // Security, provider, pending barrier and partial-write failures abort the shared graph.
                    if(!(positivePhase&&active.ActualHttpStatus==400&&startSnapshot==await fixtures.Snapshot()))throw;
                    active.Continuation="CONTINUED_AFTER_UNCHANGED_GRAPH_VERIFIED";
                }
            }
        }
        catch(Exception error)
        {
            failed=true;diagnostic=fixtures.Failure??FailureDiagnostics.Capture(error,phase,phase=="fixture-create"?fixtures.Stage:null,phase=="fixture-create"?fixtures.SaveBoundary:null,null,output);
            if(active is not null){active.Status="FAIL";active.FailureCategory=diagnostic.FailureCategory;active.Phase=phase+" / "+active.Phase;}
        }
        finally
        {
            await FixtureRunSafety.State(output,fixtures.Marker,"CLEANUP_STARTED",active?.DecisionId);
            foreach(var name in new[]{"admission","save"})await File.WriteAllTextAsync(Path.Combine(hooks,name+".release"),"RELEASE");
            foreach(var client in clients)client.Dispose();fixtures.Password="";
            try
            {
                if(fixtures.Committed)await fixtures.Cleanup();
                await fixtures.VerifyNoRoots();
                cleanup=true;await Write("FIXTURE_CLEANUP.json",new{status="PASS",fixtures.Marker,remainingRoots=0,databaseGuid=DatabaseGuid,fixtureCommitted=fixtures.Committed});
            }
            catch(Exception error){failed=true;var cleanupError=FailureDiagnostics.Capture(error,"fixture-cleanup",fixtures.Stage,fixtures.SaveBoundary,null,output);cleanupError.TransactionCommitted=fixtures.Committed;await Write("FIXTURE_CLEANUP.json",new{status="FAIL_REQUIRES_LOCAL_REVIEW",fixtures.Marker,diagnostic=cleanupError,automaticRestore=false});}
            foreach(var detail in diagnostics)detail.CleanupCompleted=cleanup;
            await FixtureRunSafety.State(output,fixtures.Marker,cleanup?"CLEANUP_COMPLETE":"CLEANUP_FAILED",active?.DecisionId);
            if(diagnostics.Count>0)await Write("ACTION_FAILURE_DIAGNOSTICS.json",diagnostics);
            if(diagnostic is not null)
            {
                diagnostic.TransactionCommitted=fixtures.Committed;diagnostic.CleanupCompleted=cleanup;await Write("ACTION_FAILURE.json",diagnostic);await Write("FIXTURE_FAILURE_DIAGNOSTICS.json",diagnostic);
                Console.Error.WriteLine(JsonSerializer.Serialize(diagnostic));
            }
            if(fixtureOnly)
            {
                await Write("FIXTURE_SMOKE_RESULT.json",new{status=!failed&&cleanup&&fixtures.Committed?"PASS":"FAIL",phase,failedStage=diagnostic?.FixtureStage,fixtureCommitted=fixtures.Committed,cleanup,remainingRoots=cleanup?(int?)0:null,database=cs.InitialCatalog,databaseGuid=DatabaseGuid,actionsExecuted=0,apiStarted=false,diagnostic});
                if(failed||!cleanup||!fixtures.Committed)Environment.ExitCode=1;
            }
            foreach(var row in matrix.Values.Where(x=>x.Status=="NOT_RUN"))row.FailureCategory=fixtureOnly?"FIXTURE_ONLY_MODE":focused?"OUTSIDE_FOCUSED_SELECTION":active is null?"FIXTURE_OR_LOGIN_ABORT":"ABORTED_SHARED_STATE_NOT_CERTIFIED";
            await Write("FULL61_ACTION_ACCEPTANCE.json",matrix.Values);
            await Write("FULL61_NEGATIVE_RESULTS.json",matrix.Values.Select(x=>new{x.DecisionId,x.Status,x.AuthorizationResult,x.FailureCategory,x.Checks}));
            await Write("AUDIT_TRANSACTION_RESULTS.json",matrix.Values.Select(x=>new{x.DecisionId,x.Status,x.AuditResult,x.RollbackResult,x.ConcurrencyResult}));
            var complete=!failed&&cleanup&&actions.Count==61;
            if(normalAuthority)await Write("NORMAL_DEMO_AUTHORITY.json",new{status=complete?"PASS":"FAIL",databaseGuid=DatabaseGuid,actionsAccepted=actions.Count,cleanup,remainingRoots=cleanup?(int?)0:null,hooksDisabled=true,realCookie=true,csrfEnforced=true,productionAcceptance=false,faultRollbackConcurrency="NOT_RUN_NORMAL_MODE; verified separately by preserved Full61 SQL receipt",actions});
            var focusedComplete=focused&&!failed&&cleanup&&matrix[focusAction].Status=="PASS"&&(focusAction!="A004A-034"||matrix["A004A-051"].Status=="PASS");
            if(focused){var result=new{status=focusedComplete?"PASS":"FAIL",decisionId=focusAction,action=matrix[focusAction],cleanup,databaseGuid=DatabaseGuid,full61Accepted=false};await Write("FOCUSED_SQL_ACCEPTANCE.json",result);await Write(focusAction switch{"A004A-001"=>"FIX03_FOCUSED_SQL_ACCEPTANCE.json","A004A-034"=>"FIX07_FOCUSED_SQL_ACCEPTANCE.json",_=>"FIX04_FOCUSED_SQL_ACCEPTANCE.json"},result);}
            await Write("ALL61_SQL_ACCEPTANCE.json",new{status=fixtureOnly?"NOT_RUN_FIXTURE_ONLY":normalAuthority?"NOT_RUN_NORMAL_MODE":focused?"NOT_RUN_FOCUSED_ONLY":complete?"PASS":"FAIL",phase,checks,actions,fullyAcceptedActionIds=complete&&!normalAuthority?Dev2ActionAdmissionRegistry.KnownActions.Select(x=>x.DecisionId).ToArray():Array.Empty<string>(),positiveAccepted=actions.Count,negativeAccepted=actions.Count,cleanup,database=cs.InitialCatalog,databaseGuid=DatabaseGuid,FULL_004_READY=false,gateNote="Full checkpoint additionally requires source seals and all offline/frontend regressions and normal demo-authority validation without verification hooks. This harness cannot authorize checkpoint005 alone."});
            await Write("SQL_E2E_RESULTS.json",new{status=fixtureOnly?"NOT_RUN":normalAuthority?(complete?"PASS_NORMAL_AUTHORITY_ONLY":"FAIL_NORMAL_AUTHORITY"):focusedComplete?"PASS_"+focusAction+"_ONLY":complete?"PASS":"FAIL",acceptedActions=actions.Count,cleanup,databaseGuid=DatabaseGuid});
            if(!fixtureOnly&&!complete&&!focusedComplete)Environment.ExitCode=1;
        }
    }
    internal static bool AllowedFocus(string id)=>id is "A004A-001" or "A004A-034" or "A004A-059";
    internal static void RequireNormalEnvironment()
    {
        foreach(var key in new[]{"DOTNET_STARTUP_HOOKS","ASPNETCORE_HOSTINGSTARTUPASSEMBLIES","DEV2_TRIAL_HOOK_DIRECTORY"})
            if(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))throw new InvalidOperationException("Normal API authority forbids verification hooks");
    }
}
