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
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.ShiftHandoverService>();
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.ShiftHandoverDelivery>();
builder.Services.AddScoped<StartupSchemaValidator>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserProfileAccess>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserProfileService>();
builder.Services.AddScoped<HotelOps.Api.Services.Authentication.HomeNavigationService>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.InsightAccess>();
builder.Services.AddOptions<HotelOps.Api.Services.Notifications.NotificationCenterOptions>().BindConfiguration("NotificationCenter").Validate(o => o.RetentionDays is >= 1 and <= 3650).ValidateOnStart();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.NotificationCenterService>();
builder.Services.AddOptions<HotelOps.Api.Services.Notifications.InspectionNotificationWorkerOptions>()
    .BindConfiguration("InspectionNotifications").Validate(o => o.PollSeconds is >= 1 and <= 3600).ValidateOnStart();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.IInspectionNotificationDelivery, HotelOps.Api.Services.Notifications.InspectionPassedNotificationDelivery>();
builder.Services.AddHostedService<HotelOps.Api.Services.Notifications.InspectionNotificationWorker>();
builder.Services.AddOptions<HotelOps.Api.Services.Notifications.CleaningClosureNotificationOptions>()
    .BindConfiguration("CleaningClosureNotifications").Validate(o => o.PollSeconds is >= 1 and <= 3600).ValidateOnStart();
builder.Services.AddHostedService<HotelOps.Api.Services.Notifications.CleaningClosureNotificationWorker>();
builder.Services.AddOptions<HotelOps.Api.Services.Notifications.TaskNotificationOptions>()
    .BindConfiguration("TaskNotifications").Validate(o=>o.PollSeconds is >=1 and <=3600).ValidateOnStart();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.TaskSubmissionNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.TaskDeclineNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.TaskAssignmentNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.TaskPlanNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.UnscheduledWorkdayDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.TimeCorrectionDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.IShiftPlanNotificationParticipant,HotelOps.Api.Services.Notifications.ShiftPlanNotificationParticipant>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.ShiftAssignmentNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.SqlSecureShiftPreview>();
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.ShiftPlanSaveService>();
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.ShiftRosterService>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.EmployeeMembershipService>();
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.ShiftAssignmentCancellation>();
builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.ShiftDefinitionService>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.MaintenanceSubmissionDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.MaintenanceReturnDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.MaintenanceDeclineDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Maintenance.MaintenanceDeclineService>();
builder.Services.AddScoped<HotelOps.Api.Services.Maintenance.MaintenanceAssignmentService>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.MaintenanceAssignmentDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.MaintenanceClosureDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.IncidentRoomReleaseDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.IncidentRoomBlockDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Incidents.IncidentRoomBlockService>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.IncidentAssetFollowupDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.AssetHandoverDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.TaskSeriesNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.TaskReopenNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.TaskVerificationNotificationDelivery>();
builder.Services.AddHostedService<HotelOps.Api.Services.Notifications.TaskNotificationWorker>();
builder.Services.AddOptions<HotelOps.Api.Services.Notifications.WorkAlertOptions>().BindConfiguration("WorkAlerts");
builder.Services.AddHostedService<HotelOps.Api.Services.Notifications.WorkAlertWorker>();
builder.Services.AddOptions<HotelOps.Api.Services.Notifications.LowStockOptions>().BindConfiguration("LowStockAlerts");
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.LowStockAlerts>();
builder.Services.AddHostedService<HotelOps.Api.Services.Notifications.LowStockWorker>();
builder.Services.AddOptions<HotelOps.Api.Services.MaterialRequests.MaterialApprovalNotificationOptions>()
    .BindConfiguration("MaterialApprovalNotifications").Validate(o => o.PollSeconds is >= 1 and <= 3600).ValidateOnStart();
builder.Services.AddScoped<HotelOps.Api.Services.MaterialRequests.MaterialApprovalNotificationDelivery>();
builder.Services.AddHostedService<HotelOps.Api.Services.MaterialRequests.MaterialApprovalNotificationWorker>();
builder.Services.AddOptions<HotelOps.Api.Services.Incidents.IncidentNotificationOptions>()
    .BindConfiguration("IncidentNotifications").Validate(o => o.PollSeconds is >= 1 and <= 3600).ValidateOnStart();
builder.Services.AddScoped<HotelOps.Api.Services.Incidents.IncidentReportedNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Incidents.IncidentCancelledNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Incidents.IncidentCancellationService>();
builder.Services.AddOptions<IncidentReopenOptions>().BindConfiguration("IncidentReopen").Validate(o => o.WindowDays > 0).ValidateOnStart();
builder.Services.AddScoped<IncidentReopenService>();
builder.Services.AddScoped<IncidentReopenedNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.PreventiveMaintenance.PreventiveAssignmentNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Incidents.IncidentEscalationService>();
builder.Services.AddScoped<HotelOps.Api.Services.Incidents.IncidentEscalatedNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Incidents.IncidentCriticalNotificationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Incidents.IncidentClassificationService>();
builder.Services.AddScoped<HotelOps.Api.Services.PreventiveMaintenance.PreventiveScheduleService>();
builder.Services.AddScoped<HotelOps.Api.Services.PreventiveMaintenance.PreventiveOccurrenceReadService>();
builder.Services.AddScoped<HotelOps.Api.Services.PreventiveMaintenance.PreventiveOccurrenceManagementService>();
builder.Services.AddScoped<HotelOps.Api.Services.PreventiveMaintenance.PreventiveOccurrenceExecutionService>();
builder.Services.AddOptions<HotelOps.Api.Services.PreventiveMaintenance.PreventiveGenerationOptions>()
    .BindConfiguration("PreventiveGeneration").Validate(o => o.LeadDays is >= 0 and <= 365).ValidateOnStart();
builder.Services.AddScoped<HotelOps.Api.Services.PreventiveMaintenance.PreventiveOccurrenceGeneration>();
builder.Services.AddHostedService<HotelOps.Api.Services.PreventiveMaintenance.PreventiveGenerationWorker>();
builder.Services.AddHostedService<HotelOps.Api.Services.Incidents.IncidentNotificationWorker>();
builder.Services.AddOptions<HotelOps.Api.Services.OperationalTasks.TaskSeriesOptions>().BindConfiguration("TaskSeriesGeneration")
    .Validate(o => o.RetrySeconds is >= 30 and <= 3600).ValidateOnStart();
builder.Services.AddScoped<HotelOps.Api.Services.OperationalTasks.TaskSeriesGeneration>();
builder.Services.AddHostedService<HotelOps.Api.Services.OperationalTasks.TaskSeriesWorker>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.AuditReadService>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.AuditAccess>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.DashboardService>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.DashboardSessionGuard>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.DashboardDrillDownService>();
builder.Services.AddHostedService<HotelOps.Api.Services.Insights.AuditArchiveWorker>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.RoomDashboardService>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.ReportAccess>();
builder.Services.AddScoped<HotelOps.Api.Services.Insights.ReportService>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestReadAccess>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestProcessAuthority>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestDecisionService>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestProgressAuthority>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestProgressService>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestTaskLinks>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestCleaningLinks>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestIncidentLinks>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestCreationContext>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestMaterialLinks>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestProgressDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestCompletionService>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestReopenService>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestReopenDelivery>();
builder.Services.AddOptions<HotelOps.Api.Services.InternalRequests.InternalRequestReopenOptions>().BindConfiguration("InternalRequestReopen").Validate(o => o.WindowDays > 0).ValidateOnStart();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestCompletionDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestEscalationDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestEscalationService>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestHandoverDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestAssignmentService>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestAssignmentDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestRouteService>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestRouteDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestWithdrawalService>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestWithdrawalDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestIntakeService>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestHandoverService>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestIntakeDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestDecisionDelivery>();
builder.Services.AddOptions<HotelOps.Api.Services.InternalRequests.InternalRequestNotificationOptions>()
    .BindConfiguration("InternalRequestNotifications").Validate(o=>o.PollSeconds is >=1 and <=3600).ValidateOnStart();
builder.Services.AddHostedService<HotelOps.Api.Services.InternalRequests.InternalRequestNotificationWorker>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.InternalRequestReadService>();
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
builder.Services.AddScoped<HotelOps.Api.Services.Notifications.ProvisionalCleaningReassignmentDelivery>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserDirectoryScopeState>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserDirectoryScopeResolver>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.IUserDirectoryService, HotelOps.Api.Services.Users.UserDirectoryService>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserAccessReviewService>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.HotelStructureCreationService>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.BranchStructureService>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.HotelStructureUpdateService>();
builder.Services.AddScoped<HotelOps.Api.Services.Rooms.RoomDirectoryService>();
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.RoomTypeCreationService>();
 builder.Services.AddScoped<HotelOps.Api.Services.OperationalTasks.WorkCategoryAdministrationService>();
builder.Services.AddScoped<HotelOps.Api.Services.Incidents.PrioritySlaAdministrationService>();
builder.Services.AddScoped<HotelOps.Api.Services.OperationalTasks.ReferenceAdministrationService>();
builder.Services.AddScoped<HotelOps.Api.Services.InternalRequests.RequestTypeAdministrationService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<HotelOps.Api.Services.Incidents.IncidentEvidenceWrite>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IMaintenanceWorkService, MaintenanceWorkService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<InventoryAccess>();
builder.Services.AddScoped<StockBalanceReadService>();
builder.Services.AddScoped<MinimumStockService>();
builder.Services.AddScoped<UnitManagementService>();
builder.Services.AddScoped<WarehouseManagementService>();
builder.Services.AddScoped<ReceiptDraftAuthority>();
builder.Services.AddScoped<ReceiptWorkspaceService>();
builder.Services.AddScoped<IInventoryIssueCapacity>(services => services.GetRequiredService<InventoryService>());
builder.Services.AddScoped<HotelOps.Api.Services.Replenishments.ReplenishmentService>();
builder.Services.AddScoped<IInventoryReceiptPosting>(services => services.GetRequiredService<InventoryService>());
builder.Services.AddScoped<IInventoryQuantityContext>(services => services.GetRequiredService<InventoryService>());
builder.Services.AddScoped<HotelOps.Api.Services.MaterialRequests.MaterialRequestService>();
builder.Services.AddScoped<HotelOps.Api.Services.MaterialRequests.MaterialCreationAccess>();
builder.Services.AddScoped<HotelOps.Api.Services.MaterialRequests.MaterialTaskLinks>();
builder.Services.AddScoped<HotelOps.Api.Services.MaterialRequests.MaterialCleaningLinks>();
builder.Services.AddScoped<HotelOps.Api.Services.MaterialRequests.MaterialOccurrenceLinks>();
builder.Services.AddScoped<HotelOps.Api.Services.MaterialRequests.MaterialIncidentLinks>();
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
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserDeactivationPreviewService>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserAccountDeactivationService>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserBranchAssignmentAccess>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserBranchAssignmentService>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserRoleAssignmentService>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserRoleRevocationService>();
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
