using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace HotelOps.Api.Services.HotelStructure;

// Explicit isolated owner candidate. Not invoked by Program or default registration.
public static class InspectionDecision08Candidate
{
    public static IServiceCollection AddInspectionDecision08OwnerCandidate(this IServiceCollection services)
    {
        services.TryAddScoped<IInspectionOriginalCleanerSource>(sp=>new InspectionR05CleanerSource(sp.GetRequiredService<HotelOpsDbContext>(),
            sp.GetRequiredService<HotelOps.Api.Security.Authorization.ICurrentUserAccessor>(),sp.GetRequiredService<HotelOps.Api.Security.Authorization.IPermissionService>(),
            ()=>sp.GetRequiredService<RoomTypeCreationService>()));
        services.AddScoped<InspectionRecipientRosterReader>();services.AddScoped<InspectionUsersPrelock>();
        services.AddScoped<InspectionOwnerOperation>();
        services.Replace(ServiceDescriptor.Scoped<IFe05RecipientPrelockAuthority>(sp=>sp.GetRequiredService<InspectionUsersPrelock>()));
        services.AddFe05Decision08RecipientCandidate();
        services.AddScoped<Fe05Decision08MandatoryRecipients>();
        services.Replace(ServiceDescriptor.Scoped<IFe05NotificationSource,Fe05SqlNotificationSource>());
        services.Replace(ServiceDescriptor.Scoped<IFe05MandatoryRecipientAuthority,InspectionMandatoryRecipients>());
        services.Replace(ServiceDescriptor.Scoped<IFe05SourceCoordination,InspectionOwnerSourceCoordination>());
        return services;
    }
}
internal sealed class InspectionMandatoryRecipients(InspectionUsersPrelock users,Fe05Decision08MandatoryRecipients rework)
    : IFe05MandatoryRecipientAuthority
{
    public async Task<IReadOnlyList<long>> ResolveMandatoryIdsAsync(HotelOpsDbContext db,Fe05NotificationSource source,Fe05NotificationKind kind,CancellationToken ct)
    {
        if(kind!=Fe05NotificationKind.InspectionFailed)return await rework.ResolveMandatoryIdsAsync(db,source,kind,ct);
        var roster=users.Roster;
        // Both relationships must be independently proven, before any deduplication.
        if(roster.Kind!=kind||roster.SupervisorId is not >0||roster.OriginalCleanerId is not >0||roster.CleanerBinding==null)
            throw new Fe05NotificationUnavailableException("FE05_BOTH_PERSISTED_RECIPIENT_ROLES_REQUIRED");
        foreach(var user in new[]{roster.SupervisorId.Value,roster.OriginalCleanerId.Value})
            await users.RequireAlreadyLockedAsync(db,source,user,ct);
        return Array.AsReadOnly(new[]{roster.SupervisorId.Value,roster.OriginalCleanerId.Value}.Distinct().Order().ToArray());
    }
}
