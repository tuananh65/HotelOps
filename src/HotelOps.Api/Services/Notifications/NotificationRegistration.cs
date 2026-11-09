using HotelOps.Api.Data;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HotelOps.Api.Services.Notifications;

public static class NotificationRegistration
{
    // The caller must also attach the registered singleton guard in AddDbContext.
    // No event profiles are registered: business producer authority remains OFF.
    public static IServiceCollection AddHotelOpsNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<NotificationCommitGuard>();
        services.AddScoped<NotificationSourcePolicy>();
        services.AddScoped<INotificationPublisher, NotificationPublisher>();
        services.AddScoped<NotificationTransactionParticipant>();
        // N01 is an internal fail-closed seam, not a production producer catalog entry.
        services.TryAddScoped<IFe05SourceCoordination, UnacceptedFe05Coordination>();
        services.TryAddScoped<IFe05MandatoryRecipientAuthority, UnacceptedFe05MandatoryRecipients>();
        // SQL readers require reviewed owner composition; production stays closed.
        services.TryAddScoped<IFe05NotificationSource, UnacceptedFe05Source>();
        services.TryAddScoped<IFe05NotificationRecipientPolicy, UnacceptedFe05Recipients>();
        services.AddScoped<Fe05NotificationStaging>();
        services.AddScoped<NotificationInbox>();
        services.AddScoped<NotificationDelivery>();
        services.AddScoped<INotificationDeliveryBatch>(provider => provider.GetRequiredService<NotificationDelivery>());
        services.AddOptions<NotificationWorkerOptions>().Bind(configuration.GetSection("NotificationDelivery"))
            .Validate(x => !x.Enabled || x.PollIntervalSeconds is >= 1 and <= 3600,
                "Enabled notification delivery requires an explicit polling interval.").ValidateOnStart();
        if (configuration.GetValue<bool>("NotificationDelivery:Enabled"))
            services.AddHostedService<NotificationDeliveryWorker>();
        return services;
    }
}
