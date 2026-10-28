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
builder.Services.AddScoped<StartupSchemaValidator>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserProfileAccess>();
builder.Services.AddScoped<HotelOps.Api.Services.Users.UserProfileService>();

builder.Services.AddDbContext<HotelOpsDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("HotelOpsDb")));

// Add services to the container.
builder.Services.AddScoped<HotelOps.Api.Services.Users.IUserDirectoryService, HotelOps.Api.Services.Users.UserDirectoryService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IMaintenanceWorkService, MaintenanceWorkService>();
builder.Services.AddScoped<InventoryService>();
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
builder.Services.AddScoped<IPermissionResourceResolver, CleaningTaskResourceResolver>();
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

app.UseAuthentication();
app.UseMiddleware<InvalidCookieMiddleware>();
app.UseMiddleware<ApiCsrfMiddleware>();
app.UseMiddleware<RestrictedSessionMiddleware>();
app.UseAuthorization();
app.UseMiddleware<SessionTouchMiddleware>();

app.MapControllers();

app.Run();
