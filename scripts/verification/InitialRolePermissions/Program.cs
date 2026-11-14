using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Http;
using HotelOps.Api.Services.Rbac;
using HotelOps.Api.DTOs.Rbac;
using HotelOps.Api.Entities;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
var count = 0;
void Check(bool result, string name) { if (!result) throw new Exception("FAIL " + name); count++; Console.WriteLine("PASS " + name); }
InitialRolePermissionsRequest Request(params InitialPermissionGrant[] grants) => new(1, grants.Length == 0 ? [new(10, "BRANCH")] : grants);
async Task<InitialRolePermissionsResult> Run(Fake factory, InitialRolePermissionsRequest? request = null) => await new InitialRolePermissionsService(factory).ConfigureAsync(7, request ?? Request(), default);
var good = new Fake(); var result = await Run(good); Check(result.Status == 201 && result.Value!.RoleId == 7 && result.Value.Grants.SequenceEqual(new[] { new InitialPermissionGrant(10, "BRANCH") }), "real domain service positive initial configuration");
Check(good.Grants.Count == 1 && good.Audits == 1 && good.Commits == 1 && good.Authorizations == 2 && good.FinalChecks == 1, "one atomic grant/audit commit with final admission");
Check((await Run(good)).Status == 409 && good.Audits == 1, "sequential duplicate cannot append grants or audit");
var race = new Fake(); var outcomes = await Task.WhenAll(Run(race), Run(race)); Check(outcomes.Count(r => r.Status == 201) == 1 && outcomes.Count(r => r.Status == 409) == 1 && race.Audits == 1, "serialized concurrent initial configurations have one winner");
foreach (var code in new[] { "SYS_ADMIN", "DEPT_MANAGER", "HK_STAFF", "RECEPTION", "OWNER", "sys_admin", " ADMIN_CUSTOM " }) { var f = new Fake { RoleCode = code }; Check((await Run(f)).Status == 409 && f.Commits == 0, "canonical/reserved identity protected: " + code); }
foreach (var mode in new[] { "system", "inactive", "history", "grants", "missing" }) { var f = new Fake { System = mode == "system", Active = mode != "inactive", History = mode == "history", Missing = mode == "missing" }; if (mode == "grants") f.Grants.Add(new(40, "OWN")); Check((await Run(f)).Status == (mode == "missing" ? 404 : 409) && f.Commits == 0, "target boundary " + mode); }
foreach (var status in new[] { 401, 403, 503 }) { var f = new Fake { Admission = status }; Check((await Run(f)).Status == status && f.Reads == 0 && f.Audits == 0, "authorization before role lookup " + status); }
foreach (var req in new[] { new InitialRolePermissionsRequest(2, [new(10, "BRANCH")]), new(1, []), new(1, [new(10, "BRANCH"), new(10, "OWN")]), new(1, [new(10, "ALL")]), new(1, [new(0, "OWN")]), new(1, Enumerable.Range(1, 33).Select(i => new InitialPermissionGrant(i, "OWN")).ToArray()) }) { var f = new Fake(); Check((await Run(f, req)).Status == 400 && f.Commits == 0, "strict version/shape/scope validation"); }
foreach (var grant in new[] { new InitialPermissionGrant(99, "OWN"), new(30, "BRANCH"), new(10, "OWN"), new(40, "BRANCH") }) { var f = new Fake(); Check((await Run(f, Request(grant))).Status == 400 && f.Grants.Count == 0, "unknown/admin/unsupported ceiling denied"); }
var own = new Fake(); Check((await Run(own, Request(new(20, "OWN"), new(40, "OWN")))).Status == 201 && own.Grants.Count == 2, "permitted own grants succeed together");
foreach (var fault in new[] { "audit", "durable", "commit" }) { var f = new Fake { Fault = fault }; try { await Run(f); throw new Exception("Expected fault"); } catch (InjectedFault) { } Check(f.Grants.Count == 0 && f.Audits == 0 && f.Commits == 0, "rollback seam " + fault); }
var cancelSource = new CancellationTokenSource(); var cancelledFactory = new Fake { Cancel = cancelSource }; var cancelled = false;
try { await new InitialRolePermissionsService(cancelledFactory).ConfigureAsync(7, Request(), cancelSource.Token); } catch (OperationCanceledException) { cancelled = true; }
Check(cancelled && cancelledFactory.Grants.Count == 0 && cancelledFactory.Audits == 0, "actual cancellation before commit rolls back staged grants/audit");
var expired = new Fake { FinalAdmission = 401 }; Check((await Run(expired)).Status == 401 && expired.Grants.Count == 0 && expired.Audits == 0, "session expires at final authorization: rollback");
var changed = new Fake { FinalHistory = true }; Check((await Run(changed)).Status == 409 && changed.Grants.Count == 0, "final history re-read blocks commit");
foreach (var json in new[] { "{\"contractVersion\":1,\"grants\":[],\"userId\":7}", "{\"contractVersion\":1,\"grants\":[{\"permissionId\":10,\"dataScope\":\"OWN\",\"token\":\"bad\"}]}" }) { var denied = false; try { JsonSerializer.Deserialize<InitialRolePermissionsRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)); } catch (JsonException) { denied = true; } Check(denied, "unknown request properties rejected"); }
Check(Attribute.IsDefined(typeof(InitialRolePermissionsCandidateController), typeof(NonControllerAttribute)), "candidate controller remains inactive");
using (var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=offline.invalid;Database=offline;Integrated Security=true;Encrypt=true;TrustServerCertificate=false").Options))
{
    var sql = db.RolePermissions.Where(g => g.RoleId == 7).Select(g => new InitialPermissionGrant(g.PermissionId, g.DataScope)).ToQueryString(); Check(sql.Contains("[role_permissions]") && sql.Contains("[data_scope]"), "offline EF grant projection translation only");
    var role = db.Model.FindEntityType(typeof(Role))!; var grant = db.Model.FindEntityType(typeof(RolePermission))!; Check(!role.GetProperties().Any(p => p.IsConcurrencyToken) && !grant.GetProperties().Any(p => p.IsConcurrencyToken), "no invented role rowversion");
}
var parts = new ApplicationPartManager(); parts.ApplicationParts.Add(new AssemblyPart(typeof(InitialRolePermissionsCandidateController).Assembly)); parts.FeatureProviders.Add(new ControllerFeatureProvider()); var controllers = new ControllerFeature(); parts.PopulateFeature(controllers);
Check(!controllers.Controllers.Any(t => t.AsType() == typeof(InitialRolePermissionsCandidateController)), "actual MVC discovery excludes initial grant writer");
Check(Attribute.IsDefined(typeof(InitialRolePermissionsCandidateController), typeof(ValidateAntiForgeryTokenAttribute)), "cookie anti-forgery attribute retained");
var http = new DefaultHttpContext(); http.Request.Scheme = "http"; var controller = new InitialRolePermissionsCandidateController(null!, null!, null!, null!, null!) { ControllerContext = new() { HttpContext = http } };
Check(await controller.Configure(7, Request(), default) is ObjectResult { StatusCode: 400 }, "HTTP denied before transaction");
http.Request.Scheme = "https"; http.Request.Headers.IfMatch = "W/\"fake\"";
Check(await controller.Configure(7, Request(), default) is ObjectResult { StatusCode: 400 }, "fake role If-Match denied before transaction");
Console.WriteLine($"RESULT {count} PASS; fake transaction concurrency/rollback are NOT SQL acceptance.");
sealed class InjectedFault : Exception;
sealed class Fake : IInitialRolePermissionsTransactions
{
    internal readonly SemaphoreSlim Mutex = new(1, 1); internal List<InitialPermissionGrant> Grants = []; internal int Audits, Commits, Reads, Authorizations, FinalChecks;
    internal CancellationTokenSource? Cancel;
    internal string RoleCode = "CUSTOM_TEST", Fault = ""; internal bool System, History, Missing, FinalHistory; internal bool Active = true; internal int Admission = 200, FinalAdmission = 200;
    public async Task<IInitialRolePermissionsTransaction> BeginAsync(CancellationToken ct) { await Mutex.WaitAsync(ct); return new Tx(this); }
    sealed class Tx(Fake f) : IInitialRolePermissionsTransaction
    {
        readonly List<InitialPermissionGrant> local = f.Grants.ToList(); int audit, authCalls, reads;
        public Task<int> AuthorizeAsync(CancellationToken ct) { f.Authorizations++; return Task.FromResult(++authCalls == 1 ? f.Admission : f.FinalAdmission); }
        public Task<InitialRoleState?> ReadAsync(long id, CancellationToken ct) { f.Reads++; reads++; return Task.FromResult(f.Missing ? null : new InitialRoleState(new Role { RoleId = id, RoleCode = f.RoleCode, IsActive = f.Active, IsSystemRole = f.System }, f.History || (reads > 1 && f.FinalHistory), local.ToArray())); }
        public Task<Permission[]> CatalogAsync(long[] ids, CancellationToken ct) => Task.FromResult(new[] { new Permission { PermissionId = 10, PermissionCode = "ROOM_VIEW" }, new() { PermissionId = 20, PermissionCode = "ATTENDANCE_RECORD" }, new() { PermissionId = 30, PermissionCode = "USER_MANAGE" }, new() { PermissionId = 40, PermissionCode = "SHIFT_VIEW" } }.Where(p => ids.Contains(p.PermissionId)).ToArray());
        public Task InsertAndAuditAsync(long id, InitialPermissionGrant[] grants, CancellationToken ct) { local.AddRange(grants); if (f.Fault == "audit") throw new InjectedFault(); audit++; return Task.CompletedTask; }
        public Task PreserveAdministrationAsync(CancellationToken ct) { f.FinalChecks++; if (f.Fault == "durable") throw new InjectedFault(); f.Cancel?.Cancel(); return Task.CompletedTask; }
        public Task CommitAsync(CancellationToken ct) { if (f.Fault == "commit") throw new InjectedFault(); f.Grants = local.ToList(); f.Audits += audit; f.Commits++; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { f.Mutex.Release(); return ValueTask.CompletedTask; }
    }
}