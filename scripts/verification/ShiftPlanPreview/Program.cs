using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.DTOs.WorkTime;
using HotelOps.Api.Services.WorkTime;

var count = 0;
void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); count++; Console.WriteLine("PASS: " + label); }
var date = new DateOnly(2026, 11, 9);
ShiftPlanDraft Draft(long employee = 7, long template = 10, long branch = 1) => new(employee, branch, branch + 10, template, date);
async Task<ShiftPlanPreview> Preview(Reader reader, params ShiftPlanDraft[] drafts) => await new ShiftPlanPreviewService(reader).PreviewAsync(drafts, default);
var reader = new Reader();
var clear = await Preview(reader, Draft());
Check(clear.Rows.Count == 1 && clear.Rows[0].Issues.Count == 0, "valid plan produces diagnostic preview");
Check(clear.RequiresServerRevalidation, "zero issues never authorize save");
Check(clear.Rows[0].StartUtc == new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero), "timezone comes from trusted reader");
Check(clear.Rows[0].EndUtc - clear.Rows[0].StartUtc == TimeSpan.FromHours(8), "preview contains planned interval");
foreach (var invalid in new[] { Draft(0), Draft(template: 0), Draft(branch: 0) })
{
    reader = new Reader();
    var result = await Preview(reader, invalid);
    Check(result.Rows[0].Issues.Contains(ShiftPlanIssue.InvalidDraft) && reader.ScopeCalls == 0, "invalid references never read facts");
}
foreach (var mode in new[] { "scope", "timezone", "missing-timezone", "template", "inactive-template", "wrong-template-branch", "employee", "inactive-employee", "no-access", "department", "time", "existing", "unavailable", "timeout" })
{
    reader = new Reader { Mode = mode };
    var result = await Preview(reader, Draft());
    var expected = mode switch
    {
        "scope" => ShiftPlanIssue.ScopeUnavailable,
        "timezone" or "missing-timezone" => ShiftPlanIssue.TimezoneUnavailable,
        "template" or "inactive-template" or "wrong-template-branch" => ShiftPlanIssue.TemplateUnavailable,
        "employee" or "inactive-employee" or "no-access" => ShiftPlanIssue.EmployeeUnavailable,
        "department" => ShiftPlanIssue.EmployeeDepartmentUnconfirmed,
        "time" => ShiftPlanIssue.InvalidTime,
        "existing" => ShiftPlanIssue.ExistingOverlap,
        _ => ShiftPlanIssue.DataUnavailable
    };
    Check(result.Rows[0].Issues.Contains(expected), "fail closed diagnostic: " + mode);
    Check(!JsonSerializer.Serialize(result).Contains("SECRET"), "no raw failure disclosure: " + mode);
}
var sameEmployee = await Preview(new Reader(), Draft(), Draft(template: 11));
Check(sameEmployee.Rows.All(r => r.Issues.Contains(ShiftPlanIssue.ProposedOverlap)), "same employee proposed overlap marks both rows");
var crossBranch = await Preview(new Reader(), Draft(), Draft(template: 20, branch: 2));
Check(crossBranch.Rows.All(r => r.Issues.Contains(ShiftPlanIssue.ProposedOverlap)), "employee overlap includes other branch");
var adjacent = await Preview(new Reader(), Draft(), Draft(template: 12));
Check(adjacent.Rows.All(r => r.Issues.Count == 0), "adjacent proposed shifts remain non-overlapping");
var different = await Preview(new Reader(), Draft(), Draft(employee: 8));
Check(different.Rows.All(r => r.Issues.Count == 0), "different employees can share time interval");
reader = new Reader();
var empty = await Preview(reader);
Check(empty.Rows.Count == 0 && reader.ScopeCalls == 0, "empty preview does not query");
try { await new ShiftPlanPreviewService(reader).PreviewAsync(Enumerable.Repeat(Draft(), 501).ToArray(), default); throw new Exception("Limit ignored"); }
catch (ArgumentException) { Check(reader.ScopeCalls == 0, "technical batch bound before reads"); }
try { await new ShiftPlanPreviewService(reader).PreviewAsync([Draft()], new CancellationToken(true)); throw new Exception("Cancellation ignored"); }
catch (OperationCanceledException) { Check(reader.ScopeCalls == 0, "cancellation before reads"); }
var mutable = new[] { Draft() };
reader = new Reader { OnScope = () => mutable[0] = Draft(employee: 99) };
await new ShiftPlanPreviewService(reader).PreviewAsync(mutable, default);
Check(reader.LastEmployee == 7, "caller cannot replace plan across awaits");
reader = new Reader { CancelDuringRead = true };
try { await Preview(reader, Draft()); throw new Exception("Read cancellation swallowed"); }
catch (OperationCanceledException) { Check(true, "cancellation during reader propagates"); }
using (var connection = new SqlConnection())
using (var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).AddInterceptors(new ForbidConnection()).Options))
{
    var sqlReader = new SqlShiftPlanPreviewReader(db);
    string Sql<T>(string method, params object[] args) => ((IQueryable<T>)typeof(SqlShiftPlanPreviewReader).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(sqlReader, args)!).ToQueryString();
    var scopeSql = Sql<ShiftPlanScope>("ScopeQuery", 1L, 11L);
    Check(scopeSql.Contains("[departments]") && scopeSql.Contains("[branches]") && scopeSql.Contains("[timezone]"), "real scope query translates mapped tables and timezone");
    Check(scopeSql.Contains("[department_id]") && scopeSql.Contains("[branch_id]") && scopeSql.Contains("[is_active]"), "scope predicates translate");
    var templateSql = Sql<ShiftPlanTemplate>("TemplateQuery", 10L);
    Check(templateSql.Contains("[shift_templates]") && templateSql.Contains("[start_time]") && templateSql.Contains("[end_time]") && templateSql.Contains("[crosses_midnight]"), "real template query translates mapped wall-clock fields");
    var employeeSql = Sql<ShiftPlanEmployee>("EmployeeQuery", 7L, 1L, new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc));
    Check(employeeSql.Contains("[users]") && employeeSql.Contains("[user_branch_access]") && employeeSql.Contains("EXISTS"), "real employee membership projection translates as EXISTS");
    Check(employeeSql.Contains("[granted_at]") && employeeSql.Contains("[revoked_at]") && employeeSql.Contains("[employment_status]") && employeeSql.Contains("[deleted_at]"), "employee eligibility and current membership predicates translate");
    Check(Sql<DateTime>("ClockQuery").Contains("SYSUTCDATETIME()"), "real clock query retains SQL UTC source");
    Check(connection.State == ConnectionState.Closed && connection.ConnectionString.Length == 0, "translation uses empty unconnected SQL connection");
    Check(!db.ChangeTracker.Entries().Any(), "translation creates no tracked entities");
}
// Internal presentation compatibility only: no public HTTP contract or endpoint is authorized.
var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var document = JsonSerializer.SerializeToElement(clear, web);
Check(document.GetProperty("requiresServerRevalidation").GetBoolean(), "internal presentation retains revalidation marker");
Check(document.GetProperty("rows")[0].GetProperty("index").GetInt32() == 0
    && document.GetProperty("rows")[0].GetProperty("startUtc").GetDateTimeOffset() == clear.Rows[0].StartUtc,
    "internal projection uses camel-case row index and UTC interval");
var names = new[] { "InvalidDraft", "ScopeUnavailable", "TemplateUnavailable", "EmployeeUnavailable", "EmployeeDepartmentUnconfirmed", "TimezoneUnavailable", "InvalidTime", "ExistingOverlap", "ProposedOverlap", "DataUnavailable" };
Check(Enum.GetNames<ShiftPlanIssue>().SequenceEqual(names), "presentation diagnostic order matches existing internal enum");
var diagnostic = JsonSerializer.SerializeToElement(new ShiftPlanPreview([new(0, null, null, [ShiftPlanIssue.ExistingOverlap, ShiftPlanIssue.ProposedOverlap])]), web);
Check(diagnostic.GetProperty("rows")[0].GetProperty("issues").EnumerateArray().Select(x => x.GetInt32()).SequenceEqual(new[] { 7, 8 }), "internal enum projection remains numeric, not invented public error codes");
Check(!document.TryGetProperty("canSave", out _) && !document.TryGetProperty("etag", out _) && !document.TryGetProperty("employeeId", out _), "internal presentation does not invent authority version or identities");
Console.WriteLine($"RESULT: {count} PASS. Fake-reader behavior and offline EF translation only; live SQL, HTTP authorization, writes and audit/notification acceptance NOT_RUN.");

sealed class Reader : IShiftPlanPreviewReader
{
    public string Mode { get; init; } = "";
    public int ScopeCalls { get; private set; }
    public long LastEmployee { get; private set; }
    public Action? OnScope { get; init; }
    public bool CancelDuringRead { get; init; }
    public Task<ShiftPlanScope?> ReadScopeAsync(long branchId, long departmentId, CancellationToken ct)
    {
        ScopeCalls++; OnScope?.Invoke();
        if (Mode == "unavailable") throw new InvalidOperationException("SECRET");
        if (Mode == "timeout") throw new TimeoutException("SECRET");
        return Task.FromResult<ShiftPlanScope?>(Mode == "scope" ? null : new(Mode == "timezone" ? "not/a/timezone" : Mode == "missing-timezone" ? null : "UTC"));
    }
    public Task<ShiftPlanTemplate?> ReadTemplateAsync(long templateId, CancellationToken ct)
    {
        if (CancelDuringRead) throw new OperationCanceledException();
        var branch = templateId == 20 ? 2 : 1;
        return Task.FromResult<ShiftPlanTemplate?>(Mode == "template" ? null : new(templateId, Mode == "wrong-template-branch" ? 99 : branch,
            Mode != "inactive-template", templateId == 11 ? new(12, 0) : templateId == 12 ? new(16, 0) : new(8, 0),
            Mode == "time" ? new(8, 0) : templateId == 12 ? new(20, 0) : new(16, 0), false));
    }
    public Task<ShiftPlanEmployee?> ReadEmployeeAsync(long employeeId, long branchId, CancellationToken ct)
    {
        LastEmployee = employeeId;
        return Task.FromResult<ShiftPlanEmployee?>(Mode == "employee" ? null : new(employeeId, Mode != "inactive-employee", Mode != "no-access", Mode == "department" ? null : branchId + 10));
    }
    public Task<bool> HasOverlapAsync(long employeeId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct) => Task.FromResult(Mode == "existing");
}
sealed class ForbidConnection : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) => throw new InvalidOperationException("SQL opening forbidden in offline verification.");
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) => throw new InvalidOperationException("SQL opening forbidden in offline verification.");
}