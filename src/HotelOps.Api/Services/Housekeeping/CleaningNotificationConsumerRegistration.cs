using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Housekeeping;

public static class CleaningNotificationConsumerRegistration
{
    // Composition only: activation stays disabled and existing DEV1 commit/command guards are retained.
    public static IServiceCollection AddHotelOpsFe04CleaningNotificationConsumer(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHotelOpsNotifications(configuration);
        services.AddHotelOpsCleaningNotificationCompatibility();
        services.AddDbContext<HotelOpsDbContext>((provider, options) => options
            .AddInterceptors(CandidateCommitGuard.Instance, CandidateCommandGuard.Instance,
                provider.GetRequiredService<NotificationCommitGuard>())
            .UseSqlServer(configuration.GetConnectionString("HotelOpsDb")));
        return services;
    }
}
