using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // UC047's original-inspection link is derived from the proven assignment, never a
    // caller-supplied original ID. It grants no inspection execution/mutation authority.
    public Task<RoomTypeBatchResult> ReadReinspectionOriginalAsync(SessionReference ticket, long sid, long branch,
        long inspection, string? root, CancellationToken ct, long? attachmentId = null)
    {
        if (branch <= 0 || inspection <= 0 || attachmentId <= 0)
            throw new ValidationException("Invalid reinspection identifier.");
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            var proof = await ReadReinspectionInCallerTransactionAsync(actor, branch, inspection, root, null, token, allowTerminal: true);
            if (proof.Status != RoomTypeBatchStatus.Success || proof.Value is not ReinspectionExecutionDto bound) return proof;
            var original = await HistoryInspectionRows(db.RoomInspections.AsNoTracking().Where(i =>
                i.InspectionId == bound.OriginalInspectionId && i.RoomId == bound.RoomId
                && i.CleaningTaskId == bound.CleaningTaskId && i.VersionId == bound.VersionId
                && i.Status == "COMPLETED" && i.Result == "FAIL")).SingleOrDefaultAsync(token);
            if (original == null || original.BranchId != branch) return new(RoomTypeBatchStatus.Unavailable);
            var state = await InspectionState(original.Id, token);
            if (state.State != "FAILED") return new(RoomTypeBatchStatus.Unavailable);
            var detail = (await HistoryInspectionDetails([original], token))[0];
            // History's general projection also contains cleaning-task files. UC047
            // exposes only files owned by this exact original inspection.
            var evidence = detail.Evidence.Where(e => e.EntityType == "INSPECTION" && e.EntityId == original.Id)
                .Select(e => e with { DownloadPath = $"/api/branches/{branch}/reinspections/{inspection}/original/evidence/{e.Id}" }).ToArray();
            if (attachmentId is { } file)
            {
                var download = await HistoryFile(evidence, file, root, token);
                return download == null ? new(RoomTypeBatchStatus.NotFound) : new(RoomTypeBatchStatus.Success, download);
            }
            var allowed = evidence.Select(e => e.Id).ToHashSet();
            var criteria = detail.Criteria.Select(c => c with { EvidenceIds = (c.EvidenceIds ?? []).Where(allowed.Contains).ToArray() }).ToArray();
            var defects = detail.Defects.Select(d => d with { EvidenceIds = (d.EvidenceIds ?? []).Where(allowed.Contains).ToArray() }).ToArray();
            var tag = WorkflowTag(state.Revision);
            return new(RoomTypeBatchStatus.Success, new ReinspectionOriginalDto(inspection, bound.ReworkId, branch,
                original.Id, original.RoomId, bound.CleaningTaskId, original.InspectorId, original.VersionId,
                state.State, tag, original.Summary, criteria, defects, evidence), tag);
        }, ct, requireRoomTypeSchema: false, permissionCode: "INSPECTION_VIEW",
            eligibility: token => EligibleInspector(ticket.UserId, branch, token));
    }
}
