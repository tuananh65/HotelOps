using HotelOps.Api.Entities.Notifications;

namespace HotelOps.Api.Services.Notifications;

// Trusted application bindings, not a client-controlled URL or a new event catalog.
// Cleaning syntax/policy are verified against DEV1 FS020 commit 8d49a908.
public sealed record NotificationSourceReference(string SourceType, long SourceId)
{
    public const string MaintenanceType = "maintenance_work";
    public const string CleaningType = "CLEANING_TASK";
    // FS020 already owns a durable FS013 operation/publication/intent graph. Inserting another
    // C04 intent would duplicate it and discard operation GUID/fingerprint/cycle/revision proof.
    public bool RequiresOperationGraphReconciliation => SourceType == CleaningType;

    public static NotificationSourceReference? Resolve(NotificationIntent intent)
    {
        if (intent.SourceId <= 0 || intent.BranchId <= 0 || intent.OccurrenceId <= 0) return null;
        if (intent.ReadPolicy == NotificationSourcePolicy.MaintenanceView &&
            intent.ActionPath == $"/maintenance/tasks/{intent.SourceId}")
            return new(MaintenanceType, intent.SourceId);
        // Exact equality rejects foreign branches/assignments, query injection, alternate hosts,
        // fragments, traversal, partial prefix matches and changed occurrence links.
        if (intent.ReadPolicy == NotificationSourcePolicy.CleaningView &&
            intent.ActionPath == $"/cleaning/tasks/{intent.SourceId}?branchId={intent.BranchId}&assignmentId={intent.OccurrenceId}")
            return new(CleaningType, intent.SourceId);
        return null;
    }
}
