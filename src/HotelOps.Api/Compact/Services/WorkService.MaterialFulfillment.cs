using HotelOps.Api.Compact.Contracts;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    // Posting and the handoff to inspection commit together. Reuse the domain's
    // handler, attendance, delivery, audit and child-workflow checks.
    public Task<object> ActDocument(long id, string command, ActionRequest input, CancellationToken ct) => atomic.Run(async () =>
    {
        await StaffSupplyService.Lock(db, ct);
        var result = await documents.Act(id, command, input, ct);
        if (command != "post") return result;
        var document = await db.InventoryDocuments.SingleAsync(d => d.DocumentId == id, ct);
        if (document.Kind != "ISSUE" || document.SourceDocumentId == null) return result;
        var source = await db.InventoryDocuments.SingleAsync(d => d.DocumentId == document.SourceDocumentId, ct);
        if (source.Kind != "MATERIAL_REQUEST" || source.Status != "FULLY_ISSUED" || source.SourceWorkId == null) return result;
        var issue = await db.WorkItems.SingleAsync(w => w.WorkId == source.SourceWorkId, ct);
        if (!await db.WorkEvents.AnyAsync(e => e.WorkId == issue.WorkId && e.EventType == "ROOM_ISSUE_REPORTED", ct)
            || await IssueStage(issue.WorkId, ct) != "ROOM_ISSUE_MATERIAL_REQUESTED"
            || await CurrentIssueMaterial(issue.WorkId, ct) != source.DocumentId) return result;
        await ActRoomIssue(issue.WorkId, "report-remedy", new RoomIssueActionRequest(
            Convert.ToBase64String(issue.RowVersion), Guid.NewGuid(),
            $"Đã cấp đủ vật tư theo chứng từ {document.DocumentNo}; chuyển giám sát kiểm tra thực tế.",
            DocumentId: source.DocumentId), ct);
        return result;
    }, ct);
}
