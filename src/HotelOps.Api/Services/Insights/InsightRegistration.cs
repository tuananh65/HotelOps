namespace HotelOps.Api.Services.Insights;

public static class InsightRegistration
{
    public static IServiceCollection AddHotelOpsInsights(this IServiceCollection services)
    {
        services.AddScoped<InsightAccess>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ReportReadService>();
        services.AddScoped<AuditReadService>();
        services.AddSingleton<ReportSnapshotStore>();
        return services;
    }
}
