using System.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.PreventiveMaintenance;

public sealed partial class PreventiveOccurrenceReadService
{
    private async Task<long[]> EvidenceIdsAsync(long id,CancellationToken ct)
    {
        if(!await PreventiveOccurrenceExecutionService.CompletionInstalledAsync(db,ct))return [];
        var ids=await db.Database.SqlQuery<long>($"""
            SELECT TOP(6) t.attachment_id AS Value FROM dbo.preventive_occurrence_completions c
            JOIN dbo.audit_logs a ON a.audit_id=c.audit_id
            CROSS APPLY OPENJSON(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END,'$.attachmentIds') j
            JOIN dbo.attachments t ON t.attachment_id=TRY_CONVERT(bigint,j.value)
            WHERE c.run_id={id} AND t.entity_type='OCCURRENCE' AND t.entity_id=c.run_id
              AND t.uploaded_by=c.actor_id AND t.uploaded_at=c.completed_at AND t.deleted_at IS NULL
            """).ToArrayAsync(ct);
        if(ids.Length>5||ids.Distinct().Count()!=ids.Length)throw new InternalRequestReadException(409,"EVIDENCE_PROVENANCE_INVALID");
        return ids;
    }
    public async Task<IReadOnlyList<InternalRequestEvidenceView>> EvidenceAsync(SessionReference ticket,long sid,long branch,long id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var source=(await ReadAsync(ticket,sid,branch,id,0,ct)).Single();
        var ids=source.Status=="COMPLETED"?await EvidenceIdsAsync(id,ct):[];
        var rows=await PreventiveEvidenceStore.Query(db,id).Where(a=>ids.Contains(a.AttachmentId))
            .OrderBy(a=>a.AttachmentId).Select(a=>new{a.AttachmentId,a.OriginalFileName,a.FileName,a.MimeType,a.FileSizeBytes}).ToArrayAsync(ct);
        await ReadAsync(ticket,sid,branch,id,0,ct);
        await tx.CommitAsync(ct);
        return rows.Select(a=>new InternalRequestEvidenceView(a.AttachmentId,a.OriginalFileName??a.FileName,a.MimeType??"application/octet-stream",
            a.FileSizeBytes??0,$"/api/branches/{branch}/preventive-occurrences/{id}/evidence/{a.AttachmentId}")).ToArray();
    }
    public async Task<InternalRequestEvidenceFile> EvidenceFileAsync(SessionReference ticket,long sid,long branch,long id,long attachment,
        string root,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var source=(await ReadAsync(ticket,sid,branch,id,0,ct)).Single();
        if(source.Status!="COMPLETED"||!(await EvidenceIdsAsync(id,ct)).Contains(attachment))throw new InternalRequestReadException(404,"EVIDENCE_UNAVAILABLE");
        var row=await PreventiveEvidenceStore.Query(db,id).SingleOrDefaultAsync(a=>a.AttachmentId==attachment,ct)
            ??throw new InternalRequestReadException(404,"EVIDENCE_UNAVAILABLE");
        var file=await InternalRequestEvidence.ReadAsync(row,root,ct)??throw new InternalRequestReadException(404,"EVIDENCE_UNAVAILABLE");
        await ReadAsync(ticket,sid,branch,id,0,ct);
        await tx.CommitAsync(ct);return file;
    }
}
