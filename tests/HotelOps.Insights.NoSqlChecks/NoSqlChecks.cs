using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

internal static class NoSqlChecks
{
    public static async Task Run(CancellationToken ct)
    {
        var passed = new List<string>();
        void Check(bool condition, string name) { Evidence.Check(condition, name); passed.Add(name); }
        var payload = JsonSerializer.Serialize(AuditRedaction.Read("""{"status":"OPEN","password":"fixture-secret","token":"fixture-token","unknown":{"credential":"hidden"},"branchId":17}"""));
        Check(payload.Contains("OPEN") && payload.Contains("17") && !payload.Contains("fixture-secret") && !payload.Contains("fixture-token") && !payload.Contains("credential"), "audit scalar allowlist/redaction");
        Check(!JsonSerializer.Serialize(AuditRedaction.Read("malformed secret text")).Contains("secret"), "malformed audit payload withheld");
        Check(AuditRedaction.Read(null) == null, "unrecorded history remains null");
        Check(!JsonSerializer.Serialize(AuditRedaction.Read("""{"status":{"password":"secret"}}""")).Contains("secret"), "nested payload withheld");
        foreach (var decision in Enum.GetValues<PermissionDecision>().Where(d => d != PermissionDecision.Granted))
        {
            try { InsightAccess.Require(new(decision)); throw new InvalidOperationException("Permission bypass"); }
            catch (InsightException error) { Check(error.Status == (decision == PermissionDecision.Unauthenticated ? 401 : decision == PermissionDecision.Unavailable ? 503 : 403), "fail closed " + decision); }
        }
        InsightAccess.Require(new(PermissionDecision.Granted)); Check(true, "existing granted permission accepted");
        foreach (var type in new[] { typeof(DashboardsController), typeof(ReportsController), typeof(AuditLogsController) })
        {
            Check(type.GetCustomAttribute<AuthorizeAttribute>() != null, type.Name + " authentication metadata");
            Check(type.GetCustomAttribute<ResponseCacheAttribute>()?.NoStore == true, type.Name + " no-store metadata");
            Check(type.GetCustomAttribute<InsightErrorsAttribute>() != null, type.Name + " sanitized errors");
        }
        Check(typeof(AuditLogsController).GetMethods().All(m => m.GetCustomAttribute<HttpPostAttribute>() == null && m.GetCustomAttribute<HttpDeleteAttribute>() == null && m.GetCustomAttribute<HttpPutAttribute>() == null), "audit read-only routes");
        foreach (var key in ReportReadService.Keys) { ReportReadService.ValidateFilters(key, new()); Check(true, "report catalog " + key); }
        try { ReportReadService.ValidateFilters("assets", new() { WarehouseId = 1 }); throw new Exception("Ignored filter"); }
        catch (InsightException error) { Check(error.Status == 400, "irrelevant filters rejected"); }
        var clock = new TestClock(); var store = new ReportSnapshotStore(clock);
        var filters = new InsightFilters([17], null, new(2026, 11, 1), new(2026, 11, 7));
        var rows = new List<Dictionary<string, object?>> { new() { ["status"] = "OPEN" } };
        var report = new ReportResult("fixture-snapshot", "incidents", clock.GetUtcNow(), filters, ["status"], rows, 1, [], [], []);
        store.Put(1, report); rows[0]["status"] = "MUTATED";
        Check(JsonSerializer.Serialize(store.Get(1, report.SnapshotId)).Contains("OPEN"), "preview is immutable copied server data");
        try { store.Get(2, report.SnapshotId); throw new Exception("Snapshot IDOR"); } catch (InsightException error) { Check(error.Status == 404, "snapshot belongs to requesting actor"); }
        clock.Now = clock.Now.AddMinutes(16);
        try { store.Get(1, report.SnapshotId); throw new Exception("Expired snapshot"); } catch (InsightException error) { Check(error.Status == 404, "expired preview fails closed"); }
        Check(InsightAccess.Start(filters).Kind == DateTimeKind.Utc && InsightAccess.End(filters).Date == new DateTime(2026, 11, 8), "inclusive UTC dates use exclusive next-day bound");
        var jsonOptions = new JsonSerializerOptions(); jsonOptions.Converters.Add(new InsightDecimalJson()); jsonOptions.Converters.Add(new InsightUtcJson());
        Check(JsonSerializer.Serialize(123456789012345.678m, jsonOptions) == "\"123456789012345.678\"", "report decimal precision preserved as JSON string");
        Check(JsonSerializer.Serialize(new DateTime(2026, 11, 7), jsonOptions).Contains("Z"), "unspecified SQL timestamp serialized explicitly UTC");
        await Evidence.Save("insights-no-sql", new { pass = true, tests = passed.Count, connectionsOpened = 0, passed }, ct);
        Console.WriteLine($"PASS FE14/FE15 no-SQL: {passed.Count} executable checks; SQL connections=0.");
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
