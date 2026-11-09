using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HotelOps.Api.Services.Notifications;

// Owner attestation only: implementations MUST NOT acquire new Users locks here.
// Before downstream/domain locks, DEV1 must retain the complete discovered Users set
// in ascending UserId order, in this exact caller transaction. A newly discovered ID
// requires rollback; it cannot extend that set after source locks have been acquired.
public interface IFe05RecipientPrelockAuthority
{
    Task RequireAlreadyLockedAsync(HotelOpsDbContext db, Fe05NotificationSource source,
        long recipientId, CancellationToken ct);
}

public sealed class UnacceptedFe05RecipientPrelocks : IFe05RecipientPrelockAuthority
{
    public Task RequireAlreadyLockedAsync(HotelOpsDbContext db, Fe05NotificationSource source,
        long recipientId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        throw new Fe05NotificationUnavailableException("FE05_RECIPIENT_USER_FIRST_PROTOCOL_NOT_ACCEPTED");
    }
}

// DECISION08 selects the business roles; only Rework has an accepted persisted role binding.
// Eligibility and per-resource authorization are still enforced by Fe05SqlRecipientPolicy.
public sealed class Fe05Decision08MandatoryRecipients(IFe05RecipientPrelockAuthority prelocks)
    : IFe05MandatoryRecipientAuthority
{
    public async Task<IReadOnlyList<long>> ResolveMandatoryIdsAsync(HotelOpsDbContext db,
        Fe05NotificationSource source, Fe05NotificationKind kind, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Fe05CallerTransaction.Require(db);
        // This interface has no OperationId: validate persisted source identity only,
        // without minting an operation or changing the existing event fingerprint.
        if (!Enum.IsDefined(kind) || source.ActorId<=0 || source.BranchId<=0 || source.RoomId<=0 ||
            source.InspectionId<=0 || source.CleaningTaskId<=0 || source.ChecklistVersionId<=0 ||
            source.CurrentCleaningTaskId!=source.CleaningTaskId || source.InspectionStatus!="COMPLETED" ||
            source.InspectionResult!="FAIL" || source.WorkflowState!="FAILED" ||
            source.OccurredAt==default || source.OccurredAt.Offset!=TimeSpan.Zero ||
            source.FailedResultIds is not {Count:>0 and <=200} || source.FailedResultIds.Any(id=>id<=0) ||
            (kind==Fe05NotificationKind.CleaningReworkRequested ? source.ReworkId is not >0 : source.ReworkId is not null))
            throw new Fe05NotificationUnavailableException("FE05_SOURCE_UNAVAILABLE");
        if (kind == Fe05NotificationKind.InspectionFailed)
            throw new Fe05NotificationUnavailableException("FE05_SUPERVISOR_AND_ORIGINAL_CLEANER_RELATIONSHIPS_REQUIRED");

        // Exact PK/FK path; no latest-assignment/log/role/creator fallback. Raw mapped SQL
        // uses the existing FS016 row_version/workflow columns without editing generated EF.
        var row = await db.Database.SqlQuery<ReworkRecipientRow>($"""
            SELECT w.rework_id AS ReworkId,w.inspection_id AS InspectionId,
              w.cleaning_task_id AS CleaningTaskId,w.assignee_user_id AS AssigneeUserId,
              i.room_id AS RoomId,r.branch_id AS BranchId,i.version_id AS VersionId,
              c.cleaning_task_id AS CurrentTaskId,i.row_version AS InspectionRevision,
              w.row_version AS ReworkRevision,w.requested_at AS RequestedAt
            FROM dbo.cleaning_reworks w WITH(HOLDLOCK)
            JOIN dbo.room_inspections i WITH(HOLDLOCK) ON i.inspection_id=w.inspection_id
              AND i.cleaning_task_id=w.cleaning_task_id
            JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=w.cleaning_task_id AND t.room_id=i.room_id
            JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id
            JOIN dbo.room_cleaning_cycle_state c WITH(HOLDLOCK) ON c.room_id=i.room_id AND c.cleaning_task_id=t.cleaning_task_id
            WHERE w.rework_id={source.ReworkId} AND i.inspection_id={source.InspectionId}
              AND t.cleaning_task_id={source.CleaningTaskId} AND r.room_id={source.RoomId} AND r.branch_id={source.BranchId}
              AND i.version_id={source.ChecklistVersionId} AND w.status='PENDING' AND w.workflow_status='PENDING_ACCEPTANCE'
              AND i.status='COMPLETED' AND i.result='FAIL' AND i.workflow_status='FAILED'
              AND i.inspected_at IS NOT NULL AND w.requested_at>=i.inspected_at AND w.due_at IS NOT NULL
              AND t.status='IN_PROGRESS' AND t.workflow_status='REWORK_REQUIRED'
              AND NOT EXISTS(SELECT 1 FROM dbo.room_inspections newer WITH(HOLDLOCK)
                WHERE newer.cleaning_task_id=i.cleaning_task_id AND newer.inspection_id>i.inspection_id)
            """).SingleOrDefaultAsync(ct);
        if (row is null || row.AssigneeUserId is not >0 || row.ReworkId!=source.ReworkId ||
            row.InspectionId!=source.InspectionId || row.CleaningTaskId!=source.CleaningTaskId ||
            row.RoomId!=source.RoomId || row.BranchId!=source.BranchId || row.VersionId!=source.ChecklistVersionId ||
            row.CurrentTaskId!=source.CurrentCleaningTaskId)
            throw new Fe05NotificationUnavailableException("FE05_EXACT_REWORK_ASSIGNEE_REQUIRED");
        if (row.InspectionRevision.Length!=8 || row.ReworkRevision.Length!=8 ||
            !StringComparer.Ordinal.Equals('"'+Convert.ToHexString(row.InspectionRevision)+'"', source.Revision) ||
            !StringComparer.Ordinal.Equals('"'+Convert.ToHexString(row.ReworkRevision)+'"', source.ReworkRevision) ||
            new DateTimeOffset(DateTime.SpecifyKind(row.RequestedAt,DateTimeKind.Utc))!=source.OccurredAt)
            throw new NotificationConflictException();
        await prelocks.RequireAlreadyLockedAsync(db,source,row.AssigneeUserId.Value,ct);
        ct.ThrowIfCancellationRequested();
        return Array.AsReadOnly(new[]{row.AssigneeUserId.Value});
    }

    private sealed class ReworkRecipientRow
    {
        public long ReworkId {get;set;} public long InspectionId {get;set;}
        public long CleaningTaskId {get;set;} public long? AssigneeUserId {get;set;}
        public long RoomId {get;set;} public long BranchId {get;set;} public long VersionId {get;set;}
        public long CurrentTaskId {get;set;} public byte[] InspectionRevision {get;set;}=[];
        public byte[] ReworkRevision {get;set;}=[]; public DateTime RequestedAt {get;set;}
    }
}

public static class Fe05Decision08RecipientRegistration
{
    // Explicit isolated composition only. No call is added to Program/default registration.
    // Without DEV1 prelock/source-read adapters this candidate remains closed.
    public static IServiceCollection AddFe05Decision08RecipientCandidate(this IServiceCollection services)
    {
        services.TryAddScoped<IFe05RecipientPrelockAuthority,UnacceptedFe05RecipientPrelocks>();
        services.Replace(ServiceDescriptor.Scoped<IFe05MandatoryRecipientAuthority,Fe05Decision08MandatoryRecipients>());
        services.Replace(ServiceDescriptor.Scoped<IFe05NotificationRecipientPolicy,Fe05SqlRecipientPolicy>());
        return services;
    }
}
