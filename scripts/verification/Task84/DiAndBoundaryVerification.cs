using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.WorkTime;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Controllers;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging(); services.AddHttpContextAccessor();
services.AddSingleton<TimeProvider>(TimeProvider.System);
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
services.AddScoped<RoomTypeDirectoryController>(); // Verification only: controller is not in production yet.
using var provider = services.BuildServiceProvider(new ServiceProviderOptions {ValidateOnBuild=true, ValidateScopes=true});
using var a = provider.CreateScope(); using var b = provider.CreateScope();
var participant = a.ServiceProvider.GetRequiredService<IWorkTimeTransactionParticipant>();
if (!ReferenceEquals(participant, a.ServiceProvider.GetRequiredService<SqlWorkTimeTransactionParticipant>()))
    throw new InvalidOperationException("Duplicate scoped SQL participant");
if (ReferenceEquals(participant, b.ServiceProvider.GetRequiredService<IWorkTimeTransactionParticipant>()))
    throw new InvalidOperationException("Participant leaked across scopes");
_ = a.ServiceProvider.GetRequiredService<WorkdayService>();
_ = a.ServiceProvider.GetRequiredService<IPermissionService>();
_ = a.ServiceProvider.GetRequiredService<IPermissionResourceResolver>();
Console.WriteLine("PASS: B1.1 DI resolves with ValidateOnBuild/ValidateScopes, no cycles, same scoped interface/concrete participant; distinct scopes isolated. No SQL connection.");

if (args.Length != 1 || File.Exists(args[0])) throw new ArgumentException("Unused receipt required");
var checks = new List<string> { "B1.1 real DI graph validates scopes and cycles", "A1 participant interface/concrete same scoped instance", "A1 participant isolated across scopes" };
void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks.Add(label); }
var directory = a.ServiceProvider.GetRequiredService<RoomTypeDirectoryService>();
Check(ReferenceEquals(directory, a.ServiceProvider.GetRequiredService<RoomTypeDirectoryService>()), "Directory scoped lifetime");
Check(!ReferenceEquals(directory, b.ServiceProvider.GetRequiredService<RoomTypeDirectoryService>()), "Directory isolated across scopes");
_ = a.ServiceProvider.GetRequiredService<RoomTypeDirectoryController>(); checks.Add("Proposed controller resolves through DI without manual construction");
var dbField = typeof(RoomTypeDirectoryService).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(x => x.FieldType == typeof(HotelOpsDbContext));
Check(ReferenceEquals(dbField.GetValue(directory), a.ServiceProvider.GetRequiredService<HotelOpsDbContext>()), "Directory uses same scoped DbContext");
foreach (var input in new[] { (0,20,"code"), (1,0,"code"), (1,101,"code"), (int.MaxValue,100,"code"), (1,20,"unknown") })
{
    try { await directory.ListAsync(null!, 1, null, null, null, input.Item1, input.Item2, input.Item3, false, default); }
    catch (ValidationException) { checks.Add("Reject invalid list pagination/sort before SQL: " + input); continue; }
    throw new Exception("Invalid list query accepted");
}
foreach (var search in new[] { new string('x',151), "a\nb" })
{
    try { await directory.ListAsync(null!,1,null,null,search,1,20,"code",false,default); }
    catch (ValidationException) { checks.Add("Reject oversized/control keyword before SQL"); continue; }
    throw new Exception("Invalid keyword accepted");
}
try { await directory.DetailAsync(null!,1,null,0,default); throw new Exception("Invalid id accepted"); }
catch (ValidationException) { checks.Add("Reject invalid detail ID before SQL"); }
var proposedRoutes = typeof(RoomTypeDirectoryController).GetMethods().SelectMany(x => x.GetCustomAttributes<HttpGetAttribute>()).Select(x => x.Template).ToArray();
Check(proposedRoutes.Order().SequenceEqual(new[] { "api/room-types", "api/room-types/{id:long}" }.Order()), "Exactly two historical GET routes; no writer routes");
var existingRoutes = typeof(HotelOps.Api.Controllers.WorkdaysController).Assembly.GetTypes().Where(t => t != typeof(RoomTypeDirectoryController)).SelectMany(t => t.GetMethods())
    .SelectMany(m => m.GetCustomAttributes<HttpGetAttribute>()).Select(x => x.Template).Where(x => x != null);
Check(!existingRoutes.Intersect(proposedRoutes).Any(), "No conflict with current API GET routes");
var rowsMethod = typeof(RoomTypeDirectoryService).GetMethod("Rows", BindingFlags.Instance | BindingFlags.NonPublic)!;
var rows = (IQueryable)rowsMethod.Invoke(directory,null)!;
var sql = rows.ToQueryString();
Check(sql.Contains("branch_id AS BranchId") && sql.Contains("default_checklist_id AS DefaultChecklistId"), "EF unmapped read projection translates without changing global model");
Check(!a.ServiceProvider.GetRequiredService<HotelOpsDbContext>().Model.GetEntityTypes().Any(x => x.ClrType.Name == "RoomTypeDirectoryRow"), "Read projection not registered as shared EF entity");
await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(new { success=true,checks=checks.Count,cases=checks,
    kind="FRESH_SOURCE_DI_STATIC_AND_ISOLATED_VALIDATION_NO_SQL_NO_HTTPS", proposedRegistrationOnly=true },new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks.Count} Task84 DI/static/boundary checks; SQL and production startup NOT RUN.");