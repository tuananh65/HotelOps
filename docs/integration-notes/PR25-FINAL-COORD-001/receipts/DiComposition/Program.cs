using HotelOps.Api.Data;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Middleware;
using Microsoft.AspNetCore.Identity;
using HotelOps.Api.Services.Assets;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.Inventory;
using Microsoft.EntityFrameworkCore;

try {
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, EnvironmentName = "Development" });
// DEV2-owned dependency participants only; no producer/worker or deactivation activation.
builder.Services.AddScoped<HotelOps.Api.Services.Dependencies.Dev2DependencyReader>();
builder.Services.AddScoped<HotelOps.Api.Services.Dependencies.Dev2PlacementAuthority>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.Fe12MaintenanceDependencyAdapter>();
builder.Services.AddScoped<StartupSchemaValidator>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserProfileAccess>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserProfileService>();

builder.Services.AddDbContext<HotelOpsDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("HotelOpsDb")));

// Add services to the container.
// FS-001: additive source integration; preserve all develop registrations.
builder.Services.AddScoped<HotelOps.Api.Services.Housekeeping.CleaningMonitorService>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.RoomTypeDirectoryService>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.TurnoverHistoryService>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.BranchAdministrationService>();
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.IWorkTimeTransactionParticipant, HotelOps.Api.Services.WorkTime.SqlWorkTimeTransactionParticipant>();
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.SqlWorkTimeTransactionParticipant>(sp => (HotelOps.Api.Services.WorkTime.SqlWorkTimeTransactionParticipant)sp.GetRequiredService<HotelOps.Api.Services.WorkTime.IWorkTimeTransactionParticipant>());
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.OwnWorkdaySnapshotReader>();
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.WorkdayService>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.IUserDirectoryService, HotelOps.Api.Services.Users.UserDirectoryService>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserAccessReviewService>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.HotelStructureCreationService>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.BranchStructureService>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.HotelStructureUpdateService>();
builder.Services.AddScoped<HotelOps.Api.Services.Rooms.RoomDirectoryService>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.RoomTypeCreationService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IMaintenanceWorkService, MaintenanceWorkService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<InventoryAccess>();
builder.Services.AddScoped<IInventoryIssueCapacity>(services => services.GetRequiredService<InventoryService>());
builder.Services.AddScoped<HotelOps.Api.Services.Replenishments.ReplenishmentService>();
builder.Services.AddScoped<IInventoryReceiptPosting>(services => services.GetRequiredService<InventoryService>());
builder.Services.AddScoped<IInventoryQuantityContext>(services => services.GetRequiredService<InventoryService>());
builder.Services.AddScoped<HotelOps.Api.Services.MaterialRequests.MaterialRequestService>();
builder.Services.AddScoped<IInventoryIssuePosting>(services => services.GetRequiredService<InventoryService>());

builder.Services.Configure<PasswordHasherOptions>(options =>
{
    options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
    options.IterationCount = PasswordCredentialService.BaselineIterationCount;
});
builder.Services.AddScoped<IPasswordCredentialService, PasswordCredentialService>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<ICredentialLockoutService, CredentialLockoutService>();
builder.Services.AddScoped<ISessionRevocationService, SessionRevocationService>();
builder.Services.AddScoped<ITemporaryCredentialService, TemporaryCredentialService>();
builder.Services.AddScoped<IInitialCredentialTransactionParticipant>(services =>
    (IInitialCredentialTransactionParticipant)services.GetRequiredService<ITemporaryCredentialService>());
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserAccountCreationService>();
builder.Services.AddScoped<ISqlSessionService, SqlSessionService>();
builder.Services.AddScoped<IAuthenticationLoginService, AuthenticationLoginService>();
builder.Services.AddScoped<IAuthenticationCredentialWorkflow, AuthenticationCredentialWorkflow>();
builder.Services.AddSingleton<ReauthenticationProofStore>();
builder.Services.AddScoped<AdminReauthenticationService>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserCredentialResetAccess>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.AdminCredentialResetWorkflow>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserAccountStatusAccess>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserAccountActivationService>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserBranchAssignmentAccess>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserBranchAssignmentService>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserRoleAssignmentService>();
builder.Services.AddHotelOpsCookieInfrastructure(builder.Environment);
builder.Services.AddHotelOpsAntiforgery();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<IPermissionDataSource, EfPermissionDataSource>();
builder.Services.AddScoped<ICleaningTaskAccessReader, EfCleaningTaskAccessReader>();
builder.Services.AddScoped<IPermissionResourceResolver>(sp => new HotelOps.Api.Services.WorkTime.WorkdayPermissionResourceResolver(
    sp.GetRequiredService<HotelOpsDbContext>(),
    ActivatorUtilities.CreateInstance<CleaningTaskResourceResolver>(sp)));
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddHotelOpsPermissionAuthorization();

builder.Services.AddControllers().ConfigureApiBehaviorOptions(AuthenticationApiBehavior.Configure);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Probe only: configure an unopened placeholder; do not execute production startup SQL.
builder.Configuration["ConnectionStrings:HotelOpsDb"] = "Server=invalid;Database=unopened;Integrated Security=true";
builder.Host.UseDefaultServiceProvider(options => { options.ValidateOnBuild = true; options.ValidateScopes = true; });
// The sandbox cannot write Windows Event Log. Keep probe diagnostics on console.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
int checks = 0;
void Check(bool value, string label) { if (!value) throw new Exception("FAIL " + label); checks++; Console.WriteLine("PASS " + label); }
foreach (var type in new[] { typeof(HotelOps.Api.Services.Dependencies.Dev2DependencyReader), typeof(HotelOps.Api.Services.Dependencies.Dev2PlacementAuthority), typeof(HotelOps.Api.Services.Notifications.Fe12MaintenanceDependencyAdapter) })
    Check(builder.Services.Count(d => d.ServiceType == type && d.Lifetime == Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped) == 1, "exactly one scoped " + type.Name);
await using var app = builder.Build();
Check(true, "actual Program service registrations validate on build");
await using var scope = app.Services.CreateAsyncScope();
foreach (var type in new[] { typeof(HotelOps.Api.Services.Dependencies.Dev2DependencyReader), typeof(HotelOps.Api.Services.Dependencies.Dev2PlacementAuthority), typeof(HotelOps.Api.Services.Notifications.Fe12MaintenanceDependencyAdapter), typeof(IAssetService), typeof(IIncidentService), typeof(ICurrentUserAccessor), typeof(IPermissionService), typeof(ISqlSessionService) })
    Check(scope.ServiceProvider.GetRequiredService(type) != null, "resolve " + type.Name);
var assets = scope.ServiceProvider.GetRequiredService<IAssetService>();
var incidents = scope.ServiceProvider.GetRequiredService<IIncidentService>();
Check(typeof(AssetService).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Single(f => f.FieldType == typeof(HotelOps.Api.Services.Dependencies.Dev2PlacementAuthority)).GetValue(assets) != null, "Asset writer receives trusted placement authority");
Check(typeof(IncidentService).GetField("_placementAuthority", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(incidents) != null, "Incident writer receives trusted placement authority");
var db = scope.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
Check(db.Database.GetDbConnection().State == System.Data.ConnectionState.Closed && !db.ChangeTracker.Entries().Any(), "DI probe opens no SQL and tracks no entities");
Check(!builder.Services.Any(d => d.ServiceType.Name.Contains("Notification") && typeof(Microsoft.Extensions.Hosting.IHostedService).IsAssignableFrom(d.ImplementationType ?? d.ServiceType)), "no Notification hosted worker registration");
Console.WriteLine($"RESULT: {checks} PASS; DI only, no production startup validator, SQL, HTTP listener or writes.");

} catch (Exception failure) { Console.Error.WriteLine(failure); Environment.ExitCode = 1; }
