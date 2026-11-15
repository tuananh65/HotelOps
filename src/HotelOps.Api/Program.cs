using HotelOps.Api.Hosting;
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

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<HotelOps.Api.Services.Dependencies.Dev2DependencyReader>();
builder.Services.AddScoped<HotelOps.Api.Services.Dependencies.Dev2PlacementAuthority>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.Fe12MaintenanceDependencyAdapter>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.RoomTypeDirectoryService>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.BranchAdministrationService>();
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.IWorkTimeTransactionParticipant, HotelOps.Api.Services.WorkTime.SqlWorkTimeTransactionParticipant>();
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.SqlWorkTimeTransactionParticipant>(sp => (HotelOps.Api.Services.WorkTime.SqlWorkTimeTransactionParticipant)sp.GetRequiredService<HotelOps.Api.Services.WorkTime.IWorkTimeTransactionParticipant>());
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.OwnWorkdaySnapshotReader>();
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.WorkdayService>();
builder.Services.AddScoped<StartupSchemaValidator>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserProfileAccess>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserProfileService>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.InsightAccess>();
builder.Services.AddOptions<HotelOps.Api.Services.Notifications.NotificationCenterOptions>().BindConfiguration("NotificationCenter").Validate(o => o.RetentionDays is >= 1 and <= 3650).ValidateOnStart();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.NotificationCenterService>();
builder.Services.AddOptions<HotelOps.Api.Services.Notifications.InspectionNotificationWorkerOptions>()
    .BindConfiguration("InspectionNotifications").Validate(o => o.PollSeconds is >= 1 and <= 3600).ValidateOnStart();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.IInspectionNotificationDelivery, HotelOps.Api.Services.Notifications.InspectionPassedNotificationDelivery>();
builder.Services.AddHostedService<HotelOps.Api.Services.Notifications.InspectionNotificationWorker>();
builder.Services.AddOptions<HotelOps.Api.Services.Notifications.TaskNotificationOptions>()
    .BindConfiguration("TaskNotifications").Validate(o=>o.PollSeconds is >=1 and <=3600).ValidateOnStart();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.TaskSubmissionNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.TaskDeclineNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.TaskAssignmentNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.TaskVerificationNotificationDelivery>();
builder.Services.AddHostedService<HotelOps.Api.Services.Notifications.TaskNotificationWorker>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.AuditReadService>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.DashboardService>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.ReportAccess>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.ReportService>();
builder.Services.AddSingleton<HotelOps.Api.Services.Insights.ReportSnapshotStore>();

var isolatedSqlRole=IsolatedSqlApplicationRole.FromConfiguration(builder.Configuration,builder.Environment);
if(isolatedSqlRole!=null)builder.Services.AddSingleton(isolatedSqlRole);
builder.Services.AddDbContext<HotelOpsDbContext>(options =>
{
    options.AddInterceptors(HotelOps.Api.Services.Notifications.CandidateCommitGuard.Instance, HotelOps.Api.Services.Notifications.CandidateCommandGuard.Instance);
    if(isolatedSqlRole!=null)options.AddInterceptors(isolatedSqlRole);
    options.UseSqlServer(builder.Configuration.GetConnectionString("HotelOpsDb"));
});

// Add services to the container.
// FE-04: additive registrations; provisional execution remains opt-in and isolated.
builder.Services.AddScoped<HotelOps.Api.Services.Housekeeping.CleaningMonitorService>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.TurnoverHistoryService>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.ProvisionalCleaningNotification>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.ProvisionalCleaningDelivery>();
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
builder.Services.AddScoped<CleaningTaskResourceResolver>();
builder.Services.AddScoped<IPermissionResourceResolver, HotelOps.Api.Services.WorkTime.WorkdayPermissionResourceResolver>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddHotelOpsPermissionAuthorization();

builder.Services.AddControllers().ConfigureApiBehaviorOptions(AuthenticationApiBehavior.Configure);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();
await using (var schemaScope = app.Services.CreateAsyncScope())
{
    var compatibility = await schemaScope.ServiceProvider.GetRequiredService<StartupSchemaValidator>()
        .ValidateAsync(app.Lifetime.ApplicationStopping);
    if (compatibility != SchemaCompatibility.Compatible)
        throw new InvalidOperationException($"Startup schema validation failed: {compatibility}. HTTP listener was not started.");
}
app.UseMiddleware<AuthenticationExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCanonicalFrontend();

app.UseAuthentication();
app.UseMiddleware<InvalidCookieMiddleware>();
app.UseMiddleware<ApiCsrfMiddleware>();
app.UseMiddleware<RestrictedSessionMiddleware>();
app.UseAuthorization();
app.UseMiddleware<SessionTouchMiddleware>();

app.MapControllers();

app.Run();
