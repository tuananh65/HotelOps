using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.WorkTime;
using HotelOps.Api.Security;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;

// MVC discovery only. Does NOT execute production Program, start a listener,
// resolve WorkdayService/DbContext, or connect to SQL. No fixture or auth bypass.
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    checks++; Console.WriteLine("PASS: " + name);
}
var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton<IWebHostEnvironment>(new VerificationEnvironment());
services.AddControllers().AddApplicationPart(typeof(WorkdayReadsController).Assembly);
using var provider = services.BuildServiceProvider();
var actions = provider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
    .OfType<ControllerActionDescriptor>().ToArray();
var reads = actions.Where(a => a.ControllerTypeInfo.AsType() == typeof(WorkdayReadsController)).ToArray();
Check(reads.Length == 2, "MVC discovers exactly two Workday read actions");
Check(!actions.Any(a => a.ControllerTypeInfo.AsType() == typeof(WorkdaysController)), "legacy WorkdaysController including Start/End remains undiscovered");
foreach (var action in reads)
{
    var template = action.AttributeRouteInfo!.Template!;
    var expected = action.ActionName == "Current" ? "current" : "own-shifts";
    Check(template == "api/branches/{branchId:long}/workdays/" + expected, "exact route " + expected);
    Check(action.ActionConstraints!.OfType<HttpMethodActionConstraint>().Single().HttpMethods.SequenceEqual(["GET"]), "GET only " + expected);
    Check(action.EndpointMetadata.OfType<IAuthorizeData>().Any(a => a.AuthenticationSchemes == HotelOpsCookieTicket.Scheme), "cookie authentication metadata " + expected);
    Check(!action.EndpointMetadata.OfType<IAllowAnonymous>().Any(), "not anonymous " + expected);
    var route = Microsoft.AspNetCore.Routing.Template.TemplateParser.Parse(template);
    var parameter = route.Parameters.Single();
    Check(parameter.Name == "branchId" && parameter.InlineConstraints.Single().Constraint == "long", "branch long route constraint " + expected);
    Check(action.MethodInfo.GetParameters().Select(p => p.ParameterType).SequenceEqual([typeof(long), typeof(CancellationToken)]), "no caller user ID and cancellation propagated " + expected);
}
var workdayActions = actions.Where(a => a.AttributeRouteInfo?.Template?.Contains("workdays", StringComparison.OrdinalIgnoreCase) == true).ToArray();
Check(workdayActions.Length == 2 && workdayActions.All(reads.Contains), "no other Workday endpoint or mutation exposed");
var collisions = actions.SelectMany(a => a.ActionConstraints?.OfType<HttpMethodActionConstraint>()
    .SelectMany(c => c.HttpMethods.Select(method => (Action: a, Method: method))) ?? [])
    .Where(x => reads.Contains(x.Action))
    .All(x => actions.Count(a => string.Equals(a.AttributeRouteInfo?.Template, x.Action.AttributeRouteInfo?.Template, StringComparison.OrdinalIgnoreCase)
        && a.ActionConstraints!.OfType<HttpMethodActionConstraint>().Any(c => c.HttpMethods.Contains(x.Method))) == 1);
Check(collisions, "no exact read method/route collisions in MVC inventory");
var cache = typeof(WorkdayReadsController).GetCustomAttribute<ResponseCacheAttribute>();
Check(cache is { NoStore: true, Location: ResponseCacheLocation.None }, "no-store response metadata");
var constraint = new Microsoft.AspNetCore.Routing.Constraints.LongRouteConstraint();
Check(constraint.Match(null, null, "branchId", new RouteValueDictionary { ["branchId"] = "9223372036854775807" }, RouteDirection.IncomingRequest), "long maximum route match");
Check(!constraint.Match(null, null, "branchId", new RouteValueDictionary { ["branchId"] = "9223372036854775808" }, RouteDirection.IncomingRequest), "long overflow route rejected");
Check(!constraint.Match(null, null, "branchId", new RouteValueDictionary { ["branchId"] = "abc" }, RouteDirection.IncomingRequest), "non-numeric route rejected");

// Test the HTTP adapter with a result delegate, not a fake SQL acceptance.
var execute = typeof(WorkdayReadsController).GetMethod("Execute", BindingFlags.NonPublic | BindingFlags.Instance)!;
async Task<(IActionResult Result, DefaultHttpContext Context)> Respond(string scheme, WorkdayResult value, Action? invoked = null)
{
    var context = new DefaultHttpContext(); context.Request.Scheme = scheme;
    var controller = new WorkdayReadsController(null!) { ControllerContext = new ControllerContext { HttpContext = context } };
    Func<Task<WorkdayResult>> action = () => { invoked?.Invoke(); return Task.FromResult(value); };
    var result = await (Task<IActionResult>)execute.Invoke(controller, [action])!;
    return (result, context);
}
var called = false;
var http = await Respond("http", new(200), () => called = true);
Check(http.Result is ObjectResult { StatusCode: 400, Value: ProblemDetails { Title: "HTTPS_REQUIRED" } } && !called, "HTTPS guard prevents service invocation");
Check((await Respond("https", new(204))).Result is NoContentResult, "current absent returns 204, not null JSON");
foreach (var status in new[] { 400, 401, 403, 409, 412, 503 })
{
    var result = await Respond("https", new(status, Code: "DEFINED_SERVICE_ERROR"));
    Check(result.Result is ObjectResult { Value: ProblemDetails p } obj && obj.StatusCode == status && p.Title == "DEFINED_SERVICE_ERROR", "service status/code preserved " + status);
    Check(!result.Context.Response.Headers.ContainsKey("ETag"), "error has no ETag " + status);
}
var snapshot = new OwnWorkingDaySnapshot(1, 2, 3, new(2026, 11, 8), 4, false,
    DateTimeOffset.Parse("2026-11-08T01:00:00Z"), "AAAAAAAAAAE=", null);
var current = await Respond("https", new(200, snapshot));
Check(current.Result is ObjectResult { StatusCode: 200 } success && ReferenceEquals(success.Value, snapshot), "current DTO forwarded without remapping");
Check(current.Context.Response.Headers.ETag == "\"AAAAAAAAAAE=\"", "quoted base64 rowversion ETag matches existing contract");
OwnEligibleShift[] shifts = [new(4, 5, 2, 3, new(2026, 11, 8), snapshot.StartedAtUtc, snapshot.StartedAtUtc.AddHours(8))];
var own = await Respond("https", new(200, shifts));
Check(own.Result is ObjectResult { StatusCode: 200 } list && ReferenceEquals(list.Value, shifts), "shift array DTO forwarded");
Check(!own.Context.Response.Headers.ContainsKey("ETag"), "own shifts does not invent ETag");
var json = JsonSerializer.SerializeToElement(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
Check(json.GetProperty("branchId").GetInt64() == 2 && json.GetProperty("openInterval").ValueKind == JsonValueKind.Null && !json.TryGetProperty("userId", out _), "camelCase current snapshot DTO compatible with frontend");
Check(JsonSerializer.SerializeToElement(shifts, new JsonSerializerOptions(JsonSerializerDefaults.Web))[0].GetProperty("shiftAssignmentId").GetInt64() == 4, "camelCase shifts array compatible with frontend");
// Source-level delegation checks complement discovery, not SQL authorization tests.
var serviceSource = File.ReadAllText("src/HotelOps.Api/Services/WorkTime/WorkdayService.cs");
foreach (var (method, permission) in new[] { ("OwnShiftsAsync", "SHIFT_VIEW"), ("CurrentAsync", "ATTENDANCE_RECORD") })
{
    Check(Regex.IsMatch(serviceSource, @"public Task<WorkdayResult> " + method + @"\(long branchId, CancellationToken ct\)\s*=> Execute\(branchId, """ + permission + @""","), "existing service independent permission binding " + method);
}
var controllerSource = File.ReadAllText("src/HotelOps.Api/Controllers/WorkdayReadsController.cs");
Check(controllerSource.Contains("Execute(() => service.OwnShiftsAsync(branchId, ct))") && controllerSource.Contains("Execute(() => service.CurrentAsync(branchId, ct))"), "controller delegates each read independently");
Check(!controllerSource.Contains("SHIFT_VIEW") && !controllerSource.Contains("ATTENDANCE_RECORD"), "no duplicate permission logic in HTTP adapter");
Console.WriteLine($"RESULT: {checks} PASS; SQL/HTTPS live NOT RUN.");

sealed class VerificationEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = typeof(WorkdayReadsController).Assembly.GetName().Name!;
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
