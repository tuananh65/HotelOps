using Microsoft.EntityFrameworkCore;
using HotelOps.Api.Services.WorkTime;
using HotelOps.Api.DTOs.WorkTime;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Text.Json;
var count = 0; void Check(bool v, string n) { if (!v) throw new Exception("FAIL " + n); count++; Console.WriteLine("PASS " + n); }
ShiftPreviewRequest Input(params long[] users) => new(users.Select(x => new ShiftPreviewInput(x, 10, new(2028, 2, 29))).ToArray());
async Task<SecurePreviewResult> Run(Fixture f, ShiftPreviewRequest? r = null) => await new SecureShiftPlanPreviewService(f, f, f, f, f, TimeProvider.System).PreviewAsync(1, 2, r ?? Input(8), default);
foreach (var scope in new[] { "BRANCH", "ALL" }) { var f = new Fixture { Scope = scope }; var r = await Run(f); Check(r.Status == 200 && r.Value!.Rows[0].Issues.Count == 0, "real evaluator+resolver permits authorized " + scope); Check(r.Value!.RequiresServerRevalidation, "no version/save authority"); }
foreach (var mode in new[] { "own", "crossGrant", "expired", "revoked", "inactiveRole", "inactiveUser", "inactiveBranch", "wrongActorScope", "wrongEmployee", "wrongTemplate", "invalidSession", "restricted" })
{
    var f = new Fixture { Mode = mode }; var r = await Run(f); Check(r.Status == (mode == "invalidSession" ? 401 : 403), "deny " + mode); Check(f.ProtectedReads == 0 && r.Value == null, "no preview facts before complete admission " + mode);
}
var mixed = new Fixture(); var denied = await Run(mixed, Input(8, 9)); Check(denied.Status == 403 && denied.Value == null && mixed.ProtectedReads == 0, "mixed scope denies entire request without partial roster");
var hidden = await Run(new() { Hidden = true }); Check(hidden.Status == 200 && hidden.Value!.Rows[0].Issues.SequenceEqual(new[] { ShiftPlanIssue.DataUnavailable }) && hidden.Value.Rows[0].StartUtc == null, "hidden cross-scope overlap is unavailable, no timing/grant detail");
var overlap = await Run(new() { Overlap = true }); Check(overlap.Value!.Rows[0].Issues.Contains(ShiftPlanIssue.ExistingOverlap), "visible employee overlap");
var proposed = await Run(new(), Input(8, 8)); Check(proposed.Value!.Rows.All(r => r.Issues.Contains(ShiftPlanIssue.ProposedOverlap)), "proposed overlap conflicts");
var late = await Run(new() { InvalidateAfterRead = true }); Check(late.Status == 401 && late.Value == null, "final session invalidation discards all preview rows");
var unavailable = await Run(new() { Mode = "unavailable" }); Check(unavailable.Status == 503 && unavailable.Value == null, "permission store unavailable fails closed");
Check((await Run(new(), new(Array.Empty<ShiftPreviewInput>()))).Status == 400, "empty input rejected");
Check((await Run(new(), new(Enumerable.Repeat(new ShiftPreviewInput(8, 10, new(2028, 2, 29)), 501).ToArray()))).Status == 400, "501 rejected");
Check((await Run(new(), new([new(0, 10, new(2028, 2, 29))]))).Status == 400, "malicious ID rejected");
var bounded = await Run(new(), new(Enumerable.Repeat(new ShiftPreviewInput(8, 10, new(2028, 2, 29)), 500).ToArray())); Check(bounded.Status == 200 && bounded.Value!.Rows.Count == 500, "500 technical bound");
try { JsonSerializer.Deserialize<ShiftPreviewRequest>("{\"drafts\":[],\"role\":\"admin\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web)); throw new Exception("extra field"); } catch (JsonException) { Check(true, "strict root DTO"); }
try { JsonSerializer.Deserialize<ShiftPreviewRequest>("{\"drafts\":[{\"employeeId\":8,\"templateId\":10,\"shiftDate\":\"2028-02-29\",\"branchId\":9}]}", new JsonSerializerOptions(JsonSerializerDefaults.Web)); throw new Exception("extra nested"); } catch (JsonException) { Check(true, "strict nested DTO"); }
var manager = new ApplicationPartManager(); manager.ApplicationParts.Add(new AssemblyPart(typeof(ShiftPlanPreviewController).Assembly)); manager.FeatureProviders.Add(new ControllerFeatureProvider()); var feature = new ControllerFeature(); manager.PopulateFeature(feature); Check(!feature.Controllers.Any(t => t.AsType() == typeof(ShiftPlanPreviewController)), "public preview route stays production OFF");
var controller = new ShiftPlanPreviewController(null!) { ControllerContext = new() { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };
Check(((ObjectResult)await controller.Preview(1, 2, Input(8), default)).StatusCode == 400, "HTTPS guard"); controller.Request.Scheme = "https"; Check(((ObjectResult)await controller.Preview(1, 2, Input(8), default)).StatusCode == 401, "cookie guard");
Check(typeof(ShiftPlanPreviewController).GetMethod("Preview")!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), false).Length == 1, "read-only POST requires CSRF");
using (var sql = new Microsoft.Data.SqlClient.SqlConnection()) using (var db = new HotelOps.Api.Data.HotelOpsDbContext(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<HotelOps.Api.Data.HotelOpsDbContext>().UseSqlServer(sql).Options))
{
    var f = new Fixture(); var save = new ShiftPlanSaveService(db, f, f, null!, TimeProvider.System);
    var ticket = HotelOps.Api.Services.Authentication.SessionReference.FromProtectedTicket(7, new string('A', 43), false, DateTime.UtcNow);
    Check((await save.SaveAsync(ticket, 70, 1, 2, Input(8), default)).Status == 503, "save remains immutable pre-SQL gated");
    Check(sql.State == System.Data.ConnectionState.Closed && !db.ChangeTracker.Entries().Any(), "no shifts/audit/notification before release");
    Check((await save.SaveAsync(ticket, 71, 1, 2, Input(8), default)).Status == 401, "save session replacement denied");
    Check((await save.SaveAsync(ticket, 70, 1, 2, new([]), default)).Status == 400, "save empty plan rejected");
    try { ((IList<string>)ShiftPlanSaveService.PendingReleaseGates).Clear(); throw new Exception("mutable gate"); } catch (NotSupportedException) { Check(true, "save release evidence immutable"); }
}
Check((await Run(new() { Mode = "global", Scope = "ALL" })).Status == 200, "unbound ALL still requires server resource membership");
var missingPermission = await Run(new() { Mode = "missing", Scope = "ALL" }); Check(missingPermission.Status == 403, "SHIFT_VIEW cannot replace SHIFT_MANAGE");
var lateScope = await Run(new() { InvalidateScopeAfterRead = true }); Check(lateScope.Status == 403 && lateScope.Value == null, "employee department replacement discards protected result");
var cross = new Fixture { Mode = "global", Scope = "ALL" }; var crossResult = await new SecureShiftPlanPreviewService(cross, cross, cross, cross, cross, TimeProvider.System).PreviewAsync(99, 2, Input(8), default);
Check(crossResult.Status == 403 && cross.ProtectedReads == 0, "unbound ALL never bypasses actor branch/department membership");
using (var scopeDb = new HotelOps.Api.Data.HotelOpsDbContext(new DbContextOptionsBuilder<HotelOps.Api.Data.HotelOpsDbContext>().UseSqlServer("Server=offline.invalid;Database=NoConnection;Integrated Security=True;Encrypt=True;TrustServerCertificate=False").Options))
{
    var reader = new SqlShiftPreviewScopeData(scopeDb, TimeProvider.System);
    var query = (IQueryable<HotelOps.Api.Entities.User>)typeof(SqlShiftPreviewScopeData).GetMethod("Eligible", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(reader, [7L, 1L, 2L])!;
    var sql = query.ToQueryString();
    foreach (var column in new[] { "primary_branch_id", "department_id", "revoked_at", "granted_at", "employment_status" }) Check(sql.Contains(column), "production scope EF translation includes " + column + " (offline only)");
}
Console.WriteLine($"RESULT {count}/{count} PASS; SQL/HTTPS and public activation NOT_RUN.");
sealed class Fixture : ICurrentUserAccessor, IPermissionDataSource, IShiftPreviewScopeData, IShiftPlanPreviewReader, IShiftPreviewSessionAdmission
{
    public string Mode = "", Scope = "BRANCH"; public bool Hidden, Overlap, InvalidateAfterRead, InvalidateScopeAfterRead; public int ProtectedReads;
    public bool TryGetCurrentUser(out CurrentUserInfo u) { u = new(7, 70, Mode == "restricted"); return true; }
    public Task<bool> IsActiveUserAsync(long u, CancellationToken ct) => Task.FromResult(Mode != "inactiveUser");
    public Task<bool> IsActiveBranchAsync(long b, CancellationToken ct) => Task.FromResult(Mode != "inactiveBranch");
    public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long u, string code, DateTime now, CancellationToken ct) { if (Mode == "unavailable") throw new TimeoutException("PRIVATE"); return Task.FromResult<IReadOnlyList<PermissionGrantPath>>([new(Mode == "missing" ? "SHIFT_VIEW" : code, Mode == "own" ? "OWN" : Scope, Mode == "global" ? null : Mode == "crossGrant" ? 99 : 1, Mode != "revoked", Mode != "inactiveRole", now.AddDays(-1), Mode == "expired" ? now.AddSeconds(-1) : null, true, true)]); }
    public Task<bool> ActorScopeAsync(long a, long b, long d, CancellationToken ct) => Task.FromResult(a == 7 && b == 1 && d == 2 && Mode != "wrongActorScope");
    public Task<bool> EmployeeScopeAsync(long e, long b, long d, CancellationToken ct) => Task.FromResult(e == 8 && b == 1 && d == 2 && Mode != "wrongEmployee" && (!InvalidateScopeAfterRead || ProtectedReads == 0));
    public Task<bool> TemplateScopeAsync(long t, long b, CancellationToken ct) => Task.FromResult(t == 10 && b == 1 && Mode != "wrongTemplate");
    public Task<bool> HiddenOverlapAsync(long e, long b, long d, DateTimeOffset a, DateTimeOffset z, CancellationToken ct) => Task.FromResult(Hidden);
    public Task<bool> ValidateAsync(CancellationToken ct) => Task.FromResult(Mode != "invalidSession" && (!InvalidateAfterRead || ProtectedReads == 0));
    public Task<ShiftPlanScope?> ReadScopeAsync(long b, long d, CancellationToken ct) { ProtectedReads++; return Task.FromResult<ShiftPlanScope?>(new("UTC")); }
    public Task<ShiftPlanTemplate?> ReadTemplateAsync(long t, CancellationToken ct) => Task.FromResult<ShiftPlanTemplate?>(new(10, 1, true, new(8, 0), new(16, 0), false));
    public Task<ShiftPlanEmployee?> ReadEmployeeAsync(long e, long b, CancellationToken ct) => Task.FromResult<ShiftPlanEmployee?>(new(8, true, true, 2));
    public Task<bool> HasOverlapAsync(long e, DateTimeOffset a, DateTimeOffset b, CancellationToken ct) => Task.FromResult(Overlap);
}