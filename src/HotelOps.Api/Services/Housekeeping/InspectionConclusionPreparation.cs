using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

// Caller-owned, read-only preparation. No endpoint, receipt, staging write or commit.
// Existing notification participants are cleaning-only candidates and cannot attest
// InspectionFailed/ReworkRequested. These results must never be accepted as proof.
public sealed partial class RoomTypeCreationService
{
    internal static readonly string[] ApproveWriteDependencies = [
        "INSPECTION_NOTIFICATION_STAGING_UNACCEPTED", "ROOM_STATUS_PROJECTION_CONTRACT_REQUIRED",
        "DURABLE_INSPECTION_RECEIPT_CONTRACT_REQUIRED"];

    internal Task<RejectInspectionLoadResult> PrepareRejectInCallerTransactionAsync(SessionReference ticket, long sid,
        RejectInspectionLoadCommand command, CancellationToken ct)
        => LoadRejectInspectionSourceAsync(ticket, sid, command, ct);

    internal async Task<RejectInspectionLoadResult> PrepareApproveInCallerTransactionAsync(SessionReference ticket, long sid,
        long branch, long inspection, string expectedETag, CancellationToken ct)
    {
        var loaded = await RejectInspectionLoading.LoadConclusionAsync(new RejectReadSession(this, db, ticket, sid),
            branch, inspection, source => ApproveInspectionValidator.Validate(source, expectedETag).Errors, ct);
        if (loaded.Status != RoomTypeBatchStatus.Success) return loaded;
        var task = loaded.Source!.Domain.CleaningTask.CleaningTaskId;
        var phase = await CleaningState(task, ct);
        if (phase.State != "PENDING_INSPECTION" || loaded.Source.Domain.CleaningTask.Status != "IN_PROGRESS")
            return new(RoomTypeBatchStatus.Inactive, "CLEANING_PHASE_CONFLICT");
        // Same persisted separation-of-duties checks as existing assignment writer (GBR-059).
        if (await db.CleaningAssignments.AsNoTracking().AnyAsync(a => a.CleaningTaskId == task && a.AssigneeUserId == ticket.UserId && a.AssignmentStatus != "CANCELLED", ct)
            || await db.CleaningProgressLogs.AsNoTracking().AnyAsync(p => p.CleaningTaskId == task && p.LoggedBy == ticket.UserId, ct))
            return new(RoomTypeBatchStatus.Forbidden, "INSPECTOR_CLEANER_CONFLICT");
        // Reinspection belongs to UC047. Do not imply it is a normal UC044 approval.
        if (await db.CleaningReworks.AsNoTracking().AnyAsync(w => w.CleaningTaskId == task, ct))
            return new(RoomTypeBatchStatus.Unavailable, "REINSPECTION_CONTRACT_REQUIRED");
        // No effective-block/Ready interpretation here: the FE04 lifecycle participant owns it.
        return loaded;
    }

    internal async Task<RoomTypeBatchResult> PrepareReworkInCallerTransactionAsync(SessionReference ticket, long sid,
        long branch, long inspection, string expectedETag, CancellationToken ct)
    {
        var session = new RejectReadSession(this, db, ticket, sid);
        session.RequireCallerTransaction();
        if (branch <= 0 || inspection <= 0 || !WorkflowStrongTag(expectedETag))
            return new(RoomTypeBatchStatus.InvalidReference);
        if (await session.AuthorizeAsync(branch, ct) is { } denied) return new(denied);
        var result = await ReworkSourceSnapshotAsync(branch, inspection, expectedETag, ct);
        if (result.Status != RoomTypeBatchStatus.Success) return result;
        if (await session.AuthorizeAsync(branch, ct) is { } revoked) return new(revoked);
        // AuthorityEstablished remains FALSE. No synthetic fs016 proof or receipt is created.
        return result;
    }
}
