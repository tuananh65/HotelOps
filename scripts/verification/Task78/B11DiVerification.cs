using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.WorkTime;
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
