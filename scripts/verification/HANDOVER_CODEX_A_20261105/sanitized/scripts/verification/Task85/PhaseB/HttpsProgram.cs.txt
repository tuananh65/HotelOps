using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

if (args.Length != 2 || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$") || File.Exists(args[1]))
    throw new ArgumentException("New guarded disposable trial and unused receipt path required.");
var root = Directory.GetCurrentDirectory();
var connection = new SqlConnectionStringBuilder
{
    DataSource = @"REDACTED_LOCAL_SQL_HOST\SQLEXPRESS", InitialCatalog = args[0], IntegratedSecurity = true,
    TrustServerCertificate = true, ConnectTimeout = 10, ApplicationName = "HotelOps UC037 disposable acceptance"
};
using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(8));
var ct = budget.Token;
HotelOpsDbContext Open() => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection.ConnectionString).Options);
await using var db = Open();
await db.Database.OpenConnectionAsync(ct);
await using (var identity = db.Database.GetDbConnection().CreateCommand())
{
    identity.CommandText = "SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
    await using var reader = await identity.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct) || reader.GetString(0) != connection.DataSource || reader.GetString(1) != args[0]
        || reader.IsDBNull(2) || reader.GetString(2) != "Task17") throw new InvalidOperationException("Trial identity rejected.");
}
if(args[0] != "HotelOps_Canonical_20261104_133500_c03885b1_Fresh") throw new InvalidOperationException("Exact new Task85B trial only" );
if(await db.Users.AnyAsync(ct)||await db.Rooms.AnyAsync(ct)||await db.CleaningTasks.AnyAsync(ct)) throw new InvalidOperationException("Fresh trial fixtures required; no reset" );
var cases = new List<string>();
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + label);
    cases.Add(label); Console.WriteLine("PASS: " + label);
}
async Task<string> Fingerprint(params string[] tables)
{
    var hashes = new List<string>();
    foreach (var table in tables)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT * FROM dbo.[{table}] ORDER BY 1 FOR JSON PATH,INCLUDE_NULL_VALUES";
        var text = new StringBuilder();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) text.Append(reader.GetString(0));
        hashes.Add(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))));
    }
    return string.Join(":", hashes);
}
var catalogBefore = await Fingerprint("roles", "permissions", "role_permissions", "branches", "departments", "reference_values");
var branches = await db.Branches.OrderBy(x => x.BranchCode).ToArrayAsync(ct);
var a = branches.Single(x => x.BranchCode == "BR001").BranchId;
var b = branches.Single(x => x.BranchCode == "BR002").BranchId;
var departments = await db.Departments.ToArrayAsync(ct);
var roles = await db.Roles.ToDictionaryAsync(x => x.RoleCode, ct);
var now = DateTime.UtcNow;
var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "aA1-";
var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
    { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
var users = new Dictionary<string, User>();
foreach (var name in new[] { "supervisor", "other", "denied", "admin", "restricted", "own" })
{
    var branch = name == "other" ? b : a;
    var user = new User { EmployeeCode = "T85B_" + name, Username = "t85b." + name, Email = "t85b." + name + "@example.invalid",
        FullName = "Monitor " + name, PrimaryBranchId = branch,
        DepartmentId = departments.Single(x => x.BranchId == branch && x.DepartmentCode == "HK").DepartmentId,
        AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = now,
        TemporaryPasswordExpiresAt = name == "restricted" ? now.AddHours(24) : null };
    user.PasswordHash = hasher.HashPassword(user, password); db.Add(user); users[name] = user;
}
await db.SaveChangesAsync(ct);
foreach (var pair in users)
{
    var branch = pair.Value.PrimaryBranchId!.Value;
    var code = pair.Key switch { "admin" => "SYS_ADMIN", "own" => "HK_STAFF", "denied" => "RECEPTION", _ => "HK_SUPERVISOR" };
    db.Add(new UserRole { UserId = pair.Value.UserId, RoleId = roles[code].RoleId,
        BranchId = pair.Key == "admin" ? null : branch, IsActive = true, AssignedAt = now.AddMinutes(-1) });
    if (pair.Key != "admin") db.Add(new UserBranchAccess { UserId = pair.Value.UserId, BranchId = branch,
        AccessLevel = "STANDARD", IsPrimary = true, GrantedAt = now.AddMinutes(-1) });
}
var floorA = new Floor {BranchId=a,FloorCode="T85B_A",FloorName="Turnover A",IsActive=true};
var floorB = new Floor {BranchId=b,FloorCode="T85B_B",FloorName="Turnover B",IsActive=true};
var roomType = new RoomType {TypeCode="T85B_TYPE",TypeName="History type",IsActive=true,StandardCapacity=2};
db.AddRange(floorA,floorB,roomType); await db.SaveChangesAsync(ct);
async Task<long> InsertRoom(long branch,long floor,string number)
{
    await using var cmd=db.Database.GetDbConnection().CreateCommand();
    // Room V1 trigger disallows direct OUTPUT without INTO. Read the inserted identity in this batch.
    cmd.CommandText="INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status,is_active) VALUES(@branch,@floor,@type,@number,'DIRTY',1); SELECT CONVERT(bigint,SCOPE_IDENTITY());";
    cmd.Parameters.Add(new SqlParameter("@branch",branch));cmd.Parameters.Add(new SqlParameter("@floor",floor));
    cmd.Parameters.Add(new SqlParameter("@type",roomType.RoomTypeId));cmd.Parameters.Add(new SqlParameter("@number",number));
    return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
}
var room=await InsertRoom(a,floorA.FloorId,"T85B_ROOM");
var emptyRoom=await InsertRoom(a,floorA.FloorId,"T85B_EMPTY");
var foreignRoom=await InsertRoom(b,floorB.FloorId,"T85B_FOREIGN");
var template=new InspectionChecklist {ChecklistCode="T85B_HISTORY",ChecklistName="Historical checklist",RoomTypeId=roomType.RoomTypeId,IsActive=true,CreatedBy=users["admin"].UserId,CreatedAt=now};
var version=new InspectionChecklistVersion {Checklist=template,VersionNo=1,Status="PUBLISHED",EffectiveAt=now.AddDays(-5)};
var item=new InspectionChecklistItem {Version=version,Criterion="Historical criterion",IsRequired=true,IsActive=false,SortOrder=1,PassCondition="No dust"};
var unanswered=new InspectionChecklistItem {Version=version,Criterion="Unanswered criterion",IsRequired=false,IsActive=true,SortOrder=2};
db.AddRange(item,unanswered);await db.SaveChangesAsync(ct);
var cleaningType=await db.ReferenceValues.Where(v=>v.GroupCode=="CLEANING_TYPE").Select(v=>v.ReferenceId).FirstAsync(ct);
var at=now.AddDays(-1);
CleaningTask Cycle(long roomId,string no,string status)=>new() {RoomId=roomId,TaskNo=no,CleaningTypeId=cleaningType,Status=status,CreatedAt=at,CreatedBy=users["admin"].UserId};
var cycle=Cycle(room,"T85B_CYCLE","COMPLETED");cycle.ActualStartAt=at;cycle.ActualEndAt=at.AddHours(2);
var cancelled=Cycle(room,"T85B_CANCELLED","CANCELLED");
var foreignCycle=Cycle(foreignRoom,"T85B_FOREIGN","COMPLETED");
db.AddRange(cycle,cancelled,foreignCycle);await db.SaveChangesAsync(ct);
RoomInspection Inspection(long sourceRoom,CleaningTask source,string no,string result,DateTime when)=>new() {
    RoomId=sourceRoom,CleaningTaskId=source.CleaningTaskId,InspectionNo=no,VersionId=version.VersionId,
    InspectorUserId=users["supervisor"].UserId,Status="COMPLETED",Result=result,CreatedAt=when,InspectedAt=when,SummaryNote="Historical summary"};
var failed=Inspection(room,cycle,"T85B_FAIL","FAIL",at.AddHours(1));
var passed=Inspection(room,cycle,"T85B_PASS","PASS",at.AddHours(2));
var foreignInspection=Inspection(foreignRoom,foreignCycle,"T85B_FOREIGN","FAIL",at.AddHours(1));
var corrupt=Inspection(foreignRoom,cycle,"T85B_CORRUPT_LINK","FAIL",at.AddHours(3));
db.AddRange(failed,passed,foreignInspection,corrupt);await db.SaveChangesAsync(ct);
db.AddRange(new InspectionDefect {InspectionId=failed.InspectionId,ChecklistItemId=item.ItemId,Description="Dust",Status="OPEN"},
    new InspectionResult {InspectionId=failed.InspectionId,ChecklistItemId=item.ItemId,Outcome="FAIL",Note="Historical failure",InspectedAt=at},
    new CleaningAssignment {CleaningTaskId=cycle.CleaningTaskId,AssigneeUserId=users["own"].UserId,AssignedBy=users["admin"].UserId,AssignedAt=at.AddHours(-1),EndedAt=at.AddHours(1),AssignmentStatus="REASSIGNED",Reason="Shift ended"},
    new CleaningAssignment {CleaningTaskId=cycle.CleaningTaskId,AssigneeUserId=users["admin"].UserId,AssignedBy=users["admin"].UserId,AssignedAt=at.AddHours(1),AssignmentStatus="COMPLETED"},
    new CleaningProgressLog {CleaningTaskId=cycle.CleaningTaskId,ProgressPercent=25,Status="BLOCKED",Note="Missing supplies",LoggedBy=users["own"].UserId,LoggedAt=at},
    new CleaningProgressLog {CleaningTaskId=cycle.CleaningTaskId,ProgressPercent=100,Status="COMPLETED",LoggedBy=users["admin"].UserId,LoggedAt=at.AddHours(1)},
    new CleaningRework {CleaningTaskId=cycle.CleaningTaskId,InspectionId=failed.InspectionId,AssigneeUserId=users["own"].UserId,Reason="Remove dust",Status="COMPLETED",RequestedAt=at.AddHours(1),CompletedAt=at.AddHours(2)},
    new AuditLog {BranchId=a,ActorUserId=users["admin"].UserId,EntityType="cleaning_tasks",EntityId=cancelled.CleaningTaskId.ToString(),Action="CANCEL",OccurredAt=at,NewValues="{\"reason\":\"Room not checked out\"}"});
await db.SaveChangesAsync(ct);
var evidenceRoot=Path.Combine(root,"scripts/verification/Task85/PhaseB/private-evidence");
if(Directory.Exists(evidenceRoot))throw new Exception("Unused evidence directory required");
Directory.CreateDirectory(evidenceRoot);
var pdf=Encoding.ASCII.GetBytes("%PDF-1.7\nTask85B historical evidence\n%%EOF");
await File.WriteAllBytesAsync(Path.Combine(evidenceRoot,"history.pdf"),pdf,ct);
Attachment Evidence(long source,string key,DateTime? deleted=null)=>new() {EntityType="CLEANING_TASK",EntityId=source,FileName="history.pdf",OriginalFileName="history.pdf",StorageKey=key,FileUrl="https://private.invalid/should-not-leak",MimeType="application/pdf",FileSizeBytes=pdf.Length,UploadedAt=at,UploadedBy=users["admin"].UserId,DeletedAt=deleted};
var attachment=Evidence(cycle.CleaningTaskId,"history.pdf");var unrelated=Evidence(foreignCycle.CleaningTaskId,"history.pdf");
var deleted=Evidence(cycle.CleaningTaskId,"history.pdf",at);var traversal=Evidence(cycle.CleaningTaskId,"../history.pdf");
db.AddRange(attachment,unrelated,deleted,traversal);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();

using var rsa = RSA.Create(2048);
var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(san.Build());
using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
var certFile = Path.Combine(Path.GetTempPath(), "HotelOps-CM-" + Guid.NewGuid().ToString("N") + ".pfx");
var certPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
await File.WriteAllBytesAsync(certFile, certificate.Export(X509ContentType.Pfx, certPassword), ct);
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "src/HotelOps.Api"), UseShellExecute = false,
    CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
start.ArgumentList.Add(Path.Combine(root, "scripts/verification/Task85/PhaseB/artifacts/integrated/bin/HotelOps.Api/debug/HotelOps.Api.dll"));
start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; start.Environment["DOTNET_ENVIRONMENT"] = "Development";
start.Environment["ASPNETCORE_URLS"] = "https://127.0.0.1:" + port;
start.Environment["ConnectionStrings__HotelOpsDb"] = connection.ConnectionString;
start.Environment["Kestrel__Certificates__Default__Path"] = certFile;
start.Environment["Kestrel__Certificates__Default__Password"] = certPassword;
start.Environment["Logging__LogLevel__Default"] = "None";
start.Environment["Evidence__PrivateRoot"] = evidenceRoot;
Process? process = null;
try
{
    process = Process.Start(start) ?? throw new InvalidOperationException("API process did not start.");
    var stdout = process.StandardOutput.ReadToEndAsync(ct); var stderr = process.StandardError.ReadToEndAsync(ct);
    using var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false,
        ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.GetCertHashString() == certificate.GetCertHashString() };
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(20) };
    var ready = false;
    for (var attempt = 0; attempt < 60 && !process.HasExited; attempt++)
    {
        try { using var response = await http.GetAsync("/api/health/database", ct); if (response.IsSuccessStatusCode) { ready = true; break; } }
        catch (HttpRequestException) { }
        await Task.Delay(250, ct);
    }
    Check(ready, "actual production API startup and prerequisite schema gate");
    async Task<HttpResponseMessage> Send(HttpMethod method, string path, Jar jar, object? body = null, CancellationToken? token = null)
    {
        using var message = new HttpRequestMessage(method, path);
        if (jar.Cookies.Count != 0) message.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
        if (jar.Token is not null) message.Headers.Add(HotelOpsAntiforgery.HeaderName, jar.Token);
        if (body is not null) message.Content = JsonContent.Create(body);
        var response = await http.SendAsync(message, token ?? ct); jar.Read(response); return response;
    }
    async Task<Jar> Login(string name)
    {
        var jar = new Jar();
        using (var bootstrap = await Send(HttpMethod.Get, "/api/auth/csrf", jar))
        {
            Check(bootstrap.IsSuccessStatusCode, "actual CSRF bootstrap " + name);
            using var json = JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync(ct)); jar.Token = json.RootElement.GetProperty("csrfToken").GetString();
        }
        using var response = await Send(HttpMethod.Post, "/api/auth/login", jar, new { Username = users[name].Username, Password = password });
        Check(response.IsSuccessStatusCode, "actual credential login and SQL session " + name);
        using var login = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); jar.Token = login.RootElement.GetProperty("csrfToken").GetString();
        return jar;
    }
    var supervisor = await Login("supervisor"); var other = await Login("other"); var denied = await Login("denied");
    var admin = await Login("admin"); var restricted = await Login("restricted"); var own = await Login("own");
    var anonymous = new Jar();
    async Task<JsonElement?> Expect(Jar jar, string path, int status, string label)
    {
        using var response = await Send(HttpMethod.Get, path, jar);
        Check((int)response.StatusCode == status && response.Headers.Location is null, label);
        var content = await response.Content.ReadAsStringAsync(ct);
        Check(!content.Contains("passwordHash", StringComparison.OrdinalIgnoreCase)
            && !content.Contains("sessionKey", StringComparison.OrdinalIgnoreCase)
            && !content.Contains("SqlException", StringComparison.OrdinalIgnoreCase), label + " no sensitive/provider disclosure");
        if (!response.IsSuccessStatusCode) return null;
        Check(response.Headers.CacheControl?.NoStore == true, label + " no-store");
        using var json = JsonDocument.Parse(content); return json.RootElement.Clone();
    }
    var route=$"/api/rooms/{room}/turnover-history?branchId={a}";
    var detail=$"/api/rooms/{room}/turnover-history/{cycle.CleaningTaskId}?branchId={a}";
    var readTables=new[]{"cleaning_tasks","cleaning_assignments","cleaning_progress_logs","room_inspections","inspection_checklist_versions","inspection_checklist_items","inspection_results","inspection_defects","cleaning_reworks","attachments","audit_logs","rooms"};
    var beforeReads=await Fingerprint(readTables);
    foreach(var (jar,code,label) in new[]{(anonymous,401,"anonymous"),(denied,403,"cleaning grant without inspection grant"),(own,403,"OWN cannot promote branch history"),(restricted,403,"restricted")})
    {await Expect(jar,route,code,label+" list");await Expect(jar,detail,code,label+" detail");}
    var list=(await Expect(supervisor,route,200,"branch supervisor list"))!.Value;
    Check(list.GetProperty("total").GetInt32()==2,"two explicit cleaning cycles; no foreign cycle inferred");
    Check(list.GetProperty("items")[0].GetProperty("id").GetInt64()==cancelled.CleaningTaskId,"newest timestamp ties use stable cycle ID order");
    var stored=list.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("id").GetInt64()==cycle.CleaningTaskId);
    Check(stored.GetProperty("result").GetString()=="PASS","latest inspection within exact room/cycle; corrupt foreign link excluded");
    Check(stored.GetProperty("reworkCount").GetInt32()==1 && stored.GetProperty("assignees").GetArrayLength()==2,"rework count and full assignment history");
    Check(stored.GetProperty("start").ValueKind==JsonValueKind.String && stored.GetProperty("end").ValueKind==JsonValueKind.String,"persisted start/end times returned");
    Check(list.GetProperty("items")[0].GetProperty("cancellationReason").GetString()=="Room not checked out","recorded cancellation reason");
    await Expect(admin,route,200,"global ALL reads branch history");
    var body=(await Expect(supervisor,detail,200,"cycle detail"))!.Value;
    Check(body.GetProperty("progress").GetArrayLength()==2 && body.GetProperty("progress")[0].GetProperty("note").GetString()=="Missing supplies","progress/blocker timeline");
    Check(body.GetProperty("inspections").GetArrayLength()==2 && body.GetProperty("reworks").GetArrayLength()==1,"only relationally consistent inspections and reworks");
    var inspection=body.GetProperty("inspections")[0];
    Check(inspection.GetProperty("inspection").GetProperty("versionId").GetInt64()==version.VersionId,"historical checklist version retained");
    Check(inspection.GetProperty("criteria").GetArrayLength()==2 && inspection.GetProperty("defects").GetArrayLength()==1,"inactive/unanswered historical criteria and defects preserved");
    var serialized=body.GetRawText();
    Check(!serialized.Contains("storageKey",StringComparison.OrdinalIgnoreCase)&&!serialized.Contains("private.invalid")&&!serialized.Contains("fileUrl",StringComparison.OrdinalIgnoreCase),"no private file path/provider URL disclosure");
    foreach(var path in new[]{route+"&pageSize=1",route+"&page=2&pageSize=1",route+"&result=PASS",route+"&from="+Uri.EscapeDataString(at.AddMinutes(-1).ToString("O"))+"&to="+Uri.EscapeDataString(at.AddMinutes(1).ToString("O"))})
        await Expect(supervisor,path,200,"supported pagination/date/result filter");
    var noMatch=(await Expect(supervisor,route+"&result=FAIL",200,"result no match"))!.Value;
    Check(noMatch.GetProperty("items").GetArrayLength()==0 && noMatch.GetProperty("filters").GetProperty("result").GetString()=="FAIL","empty list preserves normalized filters");
    var empty=(await Expect(supervisor,$"/api/rooms/{emptyRoom}/turnover-history?branchId={a}",200,"room without cycles"))!.Value;
    Check(empty.GetProperty("total").GetInt32()==0,"known empty room distinct from missing room");
    await Expect(supervisor,$"/api/rooms/{foreignRoom}/turnover-history?branchId={b}",403,"cross-branch denied");
    await Expect(supervisor,$"/api/rooms/{foreignRoom}/turnover-history?branchId={a}",404,"foreign room masked in authorized branch");
    await Expect(supervisor,$"/api/rooms/{room}/turnover-history/{foreignCycle.CleaningTaskId}?branchId={a}",404,"foreign cycle cannot be adopted");
    await Expect(supervisor,$"/api/rooms/{long.MaxValue}/turnover-history?branchId={a}",404,"missing room");
    foreach(var query in new[]{"page=0","pageSize=0","pageSize=101","page=2147483647&pageSize=100","result=INVALID","branchId=0","inspectorId=1","housekeeperId=1","search=x","from=2026-01-02T00%3A00%3A00Z&to=2026-01-01T00%3A00%3A00Z"})
        await Expect(supervisor,$"/api/rooms/{room}/turnover-history?"+query,400,"invalid query "+query);
    var fileRoute=$"/api/rooms/{room}/turnover-history/{cycle.CleaningTaskId}/evidence/";
    using(var download=await Send(HttpMethod.Get,fileRoute+attachment.AttachmentId+$"?branchId={a}",supervisor))
        Check(download.IsSuccessStatusCode && (await download.Content.ReadAsByteArrayAsync(ct)).SequenceEqual(pdf),"real source-authorized private PDF bytes");
    foreach(var (jar,code,label) in new[]{(anonymous,401,"anonymous"),(denied,403,"permission missing"),(own,403,"OWN"),(other,403,"cross-branch")})
    {using var response=await Send(HttpMethod.Get,fileRoute+attachment.AttachmentId+$"?branchId={a}",jar);Check((int)response.StatusCode==code,"attachment "+label+" denied");}
    foreach(var id in new[]{unrelated.AttachmentId,deleted.AttachmentId,traversal.AttachmentId,long.MaxValue})
    {using var response=await Send(HttpMethod.Get,fileRoute+id+$"?branchId={a}",supervisor);Check(response.StatusCode==HttpStatusCode.NotFound,"unrelated/deleted/traversal/missing file denied "+id);}
    Check(beforeReads==await Fingerprint(readTables),"UC038 GETs leave exact business/audit data unchanged");
    await Expect(supervisor,$"/api/branches/{a}/cleaning-monitor?PageSize=1",200,"UC037 production list regression");
    var monitor=(await Expect(supervisor,$"/api/branches/{a}/cleaning-monitor/cleaning/{cycle.CleaningTaskId}?timelinePage=2&timelinePageSize=1",200,"UC037 paged detail regression"))!.Value;
    Check(monitor.GetProperty("timelinePage").ValueKind==JsonValueKind.Object,"UC037 Task82 timeline metadata retained");
    await Expect(admin,"/api/room-types",200,"UC142 current production V1 list regression");
    await Expect(admin,$"/api/room-types/{roomType.RoomTypeId}",200,"UC142 current legacy-owned type detail on V1 schema regression");
    Check(beforeReads==await Fingerprint(readTables),"regression reads preserve business and audit data");
    await db.UserRoles.Where(x=>x.UserId==users["supervisor"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(r=>r.IsActive,false),ct);
    await Expect(supervisor,route,403,"role revocation read from SQL");
    await db.UserRoles.Where(x=>x.UserId==users["supervisor"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(r=>r.IsActive,true),ct);
    await db.Users.Where(x=>x.UserId==users["other"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.EmploymentStatus,"INACTIVE"),ct);
    await Expect(other,$"/api/rooms/{foreignRoom}/turnover-history?branchId={b}",401,"inactive employment invalidates session");
    await db.UserSessions.Where(x=>x.UserId==users["supervisor"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(s=>s.RevokedAt,now),ct);
    await Expect(supervisor,route,401,"revoked SQL session");
    await db.Users.Where(x=>x.UserId==users["own"].UserId).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.AccountStatus,"INACTIVE"),ct);
    await Expect(own,route,401,"inactive account invalidates SQL session");
    Check(catalogBefore==await Fingerprint("roles","permissions","role_permissions","branches","departments","reference_values"),"RBAC catalog and canonical master data unchanged");
    await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new {success=true,database=args[0],checks=cases.Count,cases,
        kind="CURRENT_SOURCE_REAL_PRODUCTION_HTTPS_SQL_UC038",completedUCs=26,verifiedPoints=60},new JsonSerializerOptions{WriteIndented=true}),ct);
    Console.WriteLine($"PASS {cases.Count} Task85B real HTTPS/SQL checks");
}


finally
{
    try
    {
        if (process is not null)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None); process.Dispose();
        }
    }
    finally { File.Delete(certFile); } // Only this invocation's exact temporary certificate.
}

sealed class Jar
{
    public Dictionary<string, string> Cookies { get; } = new();
    public string? Token { get; set; }
    public void Read(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;
        foreach (var value in values)
        {
            var pair = value.Split(';')[0].Split('=', 2);
            if (pair.Length == 2) Cookies[pair[0]] = pair[1];
        }
    }
}
