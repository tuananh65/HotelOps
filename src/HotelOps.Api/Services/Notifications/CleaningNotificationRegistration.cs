using Microsoft.Extensions.DependencyInjection.Extensions;
namespace HotelOps.Api.Services.Notifications;
public static class CleaningNotificationRegistration
{
    // Explicit reviewed caller composition. No startup, producer catalog, worker or policy activation.
    public static IServiceCollection AddHotelOpsCleaningNotificationCompatibility(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<NotificationCommitGuard>();
        services.TryAddScoped<ICleaningNotificationActivation,CleaningNotificationDisabled>();
        services.TryAddScoped<ICleaningActorAuthority,CleaningActorAuthority>();
        services.TryAddScoped<ICleaningAssignmentStore,CleaningAssignmentStore>();
        services.TryAddScoped<CleaningNotificationParticipant>();
        services.TryAddScoped<ICleaningNotificationRuntime,CleaningNotificationRuntime>();
        return services;
    }
}
