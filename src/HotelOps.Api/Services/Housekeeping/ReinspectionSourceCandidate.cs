using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

internal sealed record ReinspectionRemediation(long DefectId, string? Note, long? RemediatedBy, DateTime? RemediatedAt);
internal sealed record ReinspectionSourceCandidate(long InspectionId, long ReworkId, long OriginalInspectionId,
    long AssignedChecklistVersionId, long OriginalChecklistVersionId, string InspectionRevision, string ReworkRevision,
    IReadOnlyList<ReinspectionRemediation> Defects)
{
    // Same task is necessary but NOT a persisted Inspection->Rework assignment authority.
    internal bool AuthoritativeBindingAccepted => false;
    internal static IReadOnlyList<string> MissingContracts { get; } = Array.AsReadOnly(new[] {
        "EXACT_REINSPECTION_REWORK_BINDING_REQUIRED", "REINSPECTION_CHECKLIST_VERSION_POLICY_REQUIRED",
        "PER_DEFECT_REINSPECTION_RESULT_STORAGE_REQUIRED", "FAILED_REWORK_TRANSITION_SCHEMA_REQUIRED",
        "REINSPECTION_NOTIFICATION_AND_PROJECTION_REQUIRED" });
}
public sealed partial class RoomTypeCreationService
{
    // Explicit identities only. No latest assignment/rework, new status or evidence-byte access.
    // Internal preview remains unaccepted; no production endpoint exposes this candidate.
    internal async Task<ReinspectionSourceCandidate> ReadReinspectionCandidateInCallerTransactionAsync(
        SessionReference ticket, long sid, long branch, long inspection, long rework,
        string inspectionTag, string reworkTag, CancellationToken ct)
    {
        var session = new RejectReadSession(this, db, ticket, sid);
        session.RequireCallerTransaction();
        if (branch <= 0 || inspection <= 0 || rework <= 0 || !WorkflowStrongTag(inspectionTag) || !WorkflowStrongTag(reworkTag))
            throw new InvalidOperationException("REINSPECTION_IDENTITY_INVALID");
        if (await session.AuthorizeAsync(branch, ct) is not null) throw new InvalidOperationException("REINSPECTION_SOURCE_FORBIDDEN");
        if (await db.Database.SqlQuery<int>($"SELECT CASE WHEN COL_LENGTH('dbo.cleaning_rework_defects','remediation_note') IS NOT NULL AND COL_LENGTH('dbo.cleaning_rework_defects','remediated_by') IS NOT NULL AND COL_LENGTH('dbo.cleaning_rework_defects','remediated_at') IS NOT NULL THEN 1 ELSE 0 END AS Value").SingleAsync(ct) != 1)
            throw new InvalidOperationException("REINSPECTION_REMEDIATION_SCHEMA_REQUIRED");
        var source = await session.ReadLockedAsync(branch, inspection, ct)
            ?? throw new InvalidOperationException("REINSPECTION_SOURCE_NOT_FOUND");
        if (WorkflowTag(source.Domain.Revision) != inspectionTag) throw new InvalidOperationException("REINSPECTION_SOURCE_STALE");
        if (source.CurrentCleaningTaskId != source.Domain.CleaningTask.CleaningTaskId
            || source.Domain.CleaningTask.Status != "IN_PROGRESS"
            || (await CleaningState(source.Domain.CleaningTask.CleaningTaskId, ct)).State != "REWORK_REQUIRED"
            || source.Domain.WorkflowState is not ("ASSIGNED" or "INSPECTING" or "PENDING_CONCLUSION"))
            throw new InvalidOperationException("REINSPECTION_CURRENT_SOURCE_REQUIRED");
        var rows = await db.Database.SqlQuery<ReinspectionRow>($"""
            SELECT w.inspection_id AS OriginalInspectionId,i.version_id AS OriginalVersionId,w.row_version AS Revision
            FROM dbo.cleaning_reworks w WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK) ON i.inspection_id=w.inspection_id
            WHERE w.rework_id={rework} AND w.cleaning_task_id={source.CurrentCleaningTaskId}
              AND i.cleaning_task_id=w.cleaning_task_id AND i.room_id={source.Domain.Inspection.RoomId}
              AND i.inspection_id<>{inspection} AND i.workflow_status='FAILED' AND i.result='FAIL' AND i.status='COMPLETED'
              AND w.workflow_status='PENDING_REINSPECTION' AND w.status='IN_PROGRESS'
            """).ToArrayAsync(ct);
        if (rows.Length != 1) throw new InvalidOperationException("REINSPECTION_REWORK_SOURCE_REQUIRED");
        if (WorkflowTag(rows[0].Revision) != reworkTag) throw new InvalidOperationException("REINSPECTION_REWORK_STALE");
        // Same separation-of-duties relationship checks as UC041, with no role inference.
        if (await db.CleaningAssignments.AsNoTracking().AnyAsync(a => a.CleaningTaskId == source.CurrentCleaningTaskId
                && a.AssigneeUserId == ticket.UserId && a.AssignmentStatus != "CANCELLED", ct)
            || await db.CleaningProgressLogs.AsNoTracking().AnyAsync(p => p.CleaningTaskId == source.CurrentCleaningTaskId && p.LoggedBy == ticket.UserId, ct)
            || await db.CleaningReworks.AsNoTracking().AnyAsync(w => w.CleaningTaskId == source.CurrentCleaningTaskId && w.AssigneeUserId == ticket.UserId, ct))
            throw new InvalidOperationException("INSPECTOR_CLEANER_CONFLICT");
        var defects = await db.Database.SqlQuery<RemediationRow>($"""
            SELECT f.inspection_id AS OriginalInspectionId,d.defect_id AS DefectId,d.remediation_note AS Note,d.remediated_by AS RemediatedBy,d.remediated_at AS RemediatedAt
            FROM dbo.cleaning_rework_defects d WITH(HOLDLOCK)
            JOIN dbo.inspection_defects f ON f.defect_id=d.defect_id
            WHERE d.rework_id={rework} ORDER BY d.defect_id
            """).ToArrayAsync(ct);
        if (defects.Length == 0 || defects.Any(d => d.OriginalInspectionId != rows[0].OriginalInspectionId || d.RemediatedBy == null || d.RemediatedAt == null))
            throw new InvalidOperationException("REINSPECTION_REMEDIATION_REQUIRED");
        if (await session.AuthorizeAsync(branch, ct) is not null) throw new InvalidOperationException("REINSPECTION_SOURCE_FORBIDDEN");
        return new(inspection,rework,rows[0].OriginalInspectionId,source.Domain.Version.VersionId,rows[0].OriginalVersionId,
            inspectionTag,reworkTag,defects.Select(d => new ReinspectionRemediation(d.DefectId,d.Note,d.RemediatedBy,d.RemediatedAt)).ToArray());
    }
    private sealed class ReinspectionRow
    { public long OriginalInspectionId {get;set;} public long OriginalVersionId {get;set;} public byte[] Revision {get;set;} = []; }
    private sealed class RemediationRow
    { public long OriginalInspectionId {get;set;} public long DefectId {get;set;} public string? Note {get;set;} public long? RemediatedBy {get;set;} public DateTime? RemediatedAt {get;set;} }

}
