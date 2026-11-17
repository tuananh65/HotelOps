using HotelOps.Api.Data;
using HotelOps.Api.Services.WorkTime;

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
        var preventive = await new PreventiveResponsibilityQueries(db).ReadAsync(targetId, ct);
        var workdays = await new WorkdayResponsibilityQueries(db).ReadAsync(targetId, ct);
        var handovers = await InternalRequestHandoverResponsibilities.ReadAsync(db, targetId, ct);
        var shiftHandovers = await ShiftHandoverResponsibilities.ReadAsync(db, targetId, ct);
        // Scheduled and unscheduled days now share the deployed Workday/interval review.
        // Retire only the obsolete model-level flags when its schema contract is present;
        // preserve every row-level inconsistency, open responsibility and unrelated coverage gate.
        var workdayModel = await WorkdayMembershipStore.InstalledAsync(db, ct);
        var dev1Unresolved = dev1.Unresolved.Where(code => !workdayModel || code != "WORKDAY_CONTRACT");
        var unsupported = dev1.Unsupported.Where(code => (!workdayModel || code != "UNSCHEDULED_WORKDAY_MODEL")
            && (shiftHandovers.Unresolved.Count != 0 || code != "MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL")).ToArray();
        // Runtime target data only. Release acceptance is enforced separately by
        // UserAccountDeactivationService immediately before its first mutation.
        return new(dev1.Blockers.Concat(dev2).Concat(workdays.Blockers).Concat(preventive.Blockers).Concat(handovers.Blockers).Concat(shiftHandovers.Blockers).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            dev1Unresolved.Concat(dev2States).Concat(workdays.Unresolved).Concat(preventive.Unresolved).Concat(handovers.Unresolved).Concat(shiftHandovers.Unresolved).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray())
        {
            Unsupported = unsupported
        };
    }
}
