using HotelOps.Api.Services.Notifications;
namespace HotelOps.Api.Services.HotelStructure;
public static class InspectionCompositionRegistration
{
    public static IServiceCollection AddInspectionConclusionComposition(this IServiceCollection services,IConfiguration configuration)
    {
        if(configuration.GetValue<bool>("NotificationDelivery:Enabled"))
            throw new InvalidOperationException("Notification delivery activation is not authorized in this candidate.");
        services.AddHotelOpsNotifications(configuration);
        services.AddScoped<IFe05TrialExecutionAuthorization,InspectionClosedTrialExecution>();
        services.AddScoped<IFe05OperationStore,Fe05Fs013CandidateStore>();
        services.AddScoped<Fe05OperationStaging>();
        return services;
    }
}
