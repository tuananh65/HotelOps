using HotelOps.Api.Data;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.WorkTime;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.HotelStructure;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

if (args.Length != 1 || File.Exists(args[0])) throw new ArgumentException("Unused receipt required");
var checks = new List<string>();
void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add(label); }
var services = new ServiceCollection();
services.AddLogging(); services.AddHttpContextAccessor();
services.AddSingleton<IWebHostEnvironment>(new VerificationEnvironment());
services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
services.AddSingleton<TimeProvider>(TimeProvider.System);
// Unused, deliberately unreachable connection: this runner must never open SQL.
services.AddDbContext<HotelOpsDbContext>(o => o.UseSqlServer("Server=unused;Database=unused;Integrated Security=true"));
services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
services.AddScoped<IPermissionDataSource, EfPermissionDataSource>();
services.AddScoped<ICleaningTaskAccessReader, EfCleaningTaskAccessReader>();
services.AddScoped<IPermissionResourceResolver>(sp => new WorkdayPermissionResourceResolver(
    sp.GetRequiredService<HotelOpsDbContext>(), ActivatorUtilities.CreateInstance<CleaningTaskResourceResolver>(sp)));
services.AddScoped<IPermissionService, PermissionService>();
services.AddScoped<ISessionRevocationService, SessionRevocationService>();
services.AddScoped<ISqlSessionService, SqlSessionService>();
services.AddScoped<IWorkTimeTransactionParticipant, SqlWorkTimeTransactionParticipant>();
services.AddScoped<SqlWorkTimeTransactionParticipant>(sp => (SqlWorkTimeTransactionParticipant)sp.GetRequiredService<IWorkTimeTransactionParticipant>());
services.AddScoped<OwnWorkdaySnapshotReader>(); services.AddScoped<WorkdayService>();
services.AddScoped<RoomTypeDirectoryService>();
services.AddScoped<TurnoverHistoryService>(); // Proposed registration; production Program is untouched.
services.AddScoped<TurnoverHistoryController>(); // Proposed controller compiled via validation-only targets.
using var provider = services.BuildServiceProvider(new ServiceProviderOptions {ValidateOnBuild=true, ValidateScopes=true});
using var a = provider.CreateScope(); using var b = provider.CreateScope();
var participant = a.ServiceProvider.GetRequiredService<IWorkTimeTransactionParticipant>();
Check(ReferenceEquals(participant, a.ServiceProvider.GetRequiredService<SqlWorkTimeTransactionParticipant>()), "Existing A1 participant interface/concrete same scoped instance");
Check(!ReferenceEquals(participant, b.ServiceProvider.GetRequiredService<IWorkTimeTransactionParticipant>()), "Existing participant scopes isolated");
_ = a.ServiceProvider.GetRequiredService<WorkdayService>();
_ = a.ServiceProvider.GetRequiredService<RoomTypeDirectoryService>();
var history = a.ServiceProvider.GetRequiredService<TurnoverHistoryService>();
Check(ReferenceEquals(history, a.ServiceProvider.GetRequiredService<TurnoverHistoryService>()), "Proposed history service scoped lifetime resolves");
Check(!ReferenceEquals(history, b.ServiceProvider.GetRequiredService<TurnoverHistoryService>()), "History service isolated scopes");
var db = a.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
var field = typeof(TurnoverHistoryService).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(f => f.FieldType == typeof(HotelOpsDbContext));
Check(ReferenceEquals(field.GetValue(history),db), "History uses same scoped DbContext");
var controller = a.ServiceProvider.GetRequiredService<TurnoverHistoryController>();
Check(controller is not null, "Proposed controller and dependencies resolve with scope validation");
foreach(var query in new OperationalHistoryQuery[] {
    new(Page:0),new(PageSize:0),new(PageSize:101),new(Page:int.MaxValue,PageSize:100),
    new(BranchId:0),new(RoomId:2),new(InspectorId:1),new(HousekeeperId:1),new(Search:"x"),
    new(Result:"INVALID"),new(From:new DateTime(2026,1,2,0,0,0,DateTimeKind.Utc),To:new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc)),
    new(From:new DateTime(2026,1,1,0,0,0,DateTimeKind.Unspecified)) })
{
    try { await history.TurnoverHistoryAsync(null!,1,1,query,null,default); }
    catch(ValidationException) {checks.Add("Invalid turnover filter rejected before SQL: "+query);continue;}
    throw new Exception("Invalid turnover filter accepted");
}
foreach(var ids in new[] {(0L,(long?)null),(1L,(long?)0),(-1L,(long?)1)})
{
    try {await history.TurnoverHistoryAsync(null!,1,ids.Item1,new(),ids.Item2,default);}
    catch(ValidationException) {checks.Add("Invalid authoritative room/cycle ID rejected before SQL");continue;}
    throw new Exception("Invalid ID accepted");
}
// Build the real inspection projection against the unchanged relational EF model without connecting SQL.
var projection=typeof(TurnoverHistoryService).GetMethod("HistoryInspectionRows",BindingFlags.Static|BindingFlags.NonPublic)!;
var queryable=(IQueryable)projection.Invoke(null,[db.RoomInspections.AsNoTracking()])!;
Check(queryable.ToQueryString().Contains("inspection_id"),"Historical inspection projection translates against current EF model");
Check(!db.ChangeTracker.Entries().Any(),"No entities tracked or staged by DI/validation/translation");
controller!.ControllerContext=new ControllerContext {HttpContext=new DefaultHttpContext()};
Check(await controller.Turnovers(1,new(),default) is ObjectResult {StatusCode:400},"Controller refuses plaintext HTTP before invoking service");
controller.Request.Scheme="https";
Check(await controller.Turnovers(1,new(),default) is ObjectResult {StatusCode:401},"Controller refuses anonymous HTTPS before SQL");
var routes=typeof(TurnoverHistoryController).GetMethods().SelectMany(m=>m.GetCustomAttributes<HttpGetAttribute>()).ToArray();
Check(routes.Length==3 && routes.All(r=>r.Template!.Contains("turnover-history")),"Only three read-only turnover routes proposed; UC048 excluded");
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(new {success=true,checks=checks.Count,cases=checks,
    scope="PROPOSED_DI_BOUNDARY_AND_EF_TRANSLATION_ONLY_NO_SQL_NO_HTTPS_SERVER",productionDiApplied=false,sqlConnections=0},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks.Count} Task85B proposed DI/boundary/EF-translation checks; live HTTPS/SQL NOT RUN");

sealed class VerificationEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "Task85B.Verification";
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
