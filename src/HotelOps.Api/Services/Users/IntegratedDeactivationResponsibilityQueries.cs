using HotelOps.Api.Data;

namespace HotelOps.Api.Services.Users;

/// <summary>Combines existing predicates without modifying the protected Task46 helper.</summary>
internal sealed class IntegratedDeactivationResponsibilityQueries(HotelOpsDbContext db)
{
    internal async Task<Dev1ResponsibilityReview> ReadAsync(long targetId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Integrated review requires the deactivation transaction and retained User locks.");
        var dev1 = await new ApprovedDev1ResponsibilityQueries(db).ReadAsync(targetId, ct);
        var dev2 = await new DeactivationResponsibilityQueries(db).ReadApprovedDev2BlockersAsync(targetId, ct);
        var dev2States = await new Dev2ResponsibilityStateQueries(db).ReadUnresolvedAsync(targetId, ct);
        var workdays = await new WorkdayResponsibilityQueries(db).ReadAsync(targetId, ct);
        // Runtime target data only. Release acceptance is enforced separately by
        // UserAccountDeactivationService immediately before its first mutation.
        return new(dev1.Blockers.Concat(dev2).Concat(workdays.Blockers).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            dev1.Unresolved.Concat(dev2States).Concat(workdays.Unresolved).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray())
        {
            Unsupported = dev1.Unsupported
        };
    }
}
