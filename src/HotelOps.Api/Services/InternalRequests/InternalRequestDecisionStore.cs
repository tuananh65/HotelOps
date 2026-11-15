using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

internal static class InternalRequestDecisionStore
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.InternalRequestDecision.V1' AND CONVERT(nvarchar(20),value)='1')
              AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.internal_requests')
                AND name='row_version' AND system_type_id=189 AND is_nullable=0)
              AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.internal_requests')
                AND name='CK_internal_requests_status' AND is_disabled=0 AND is_not_trusted=0
                AND definition LIKE '%''PENDING_ACCEPTANCE''%' AND definition LIKE '%''ACCEPTED''%')
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.internal_request_decision_outbox')
                AND name IN('FK_ir_decision_event','FK_ir_decision_requester','FK_ir_decision_notification')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=3
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)==1;

    internal static Task<byte[]> RevisionAsync(HotelOpsDbContext db,long id,CancellationToken ct) =>
        db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.internal_requests WHERE internal_request_id={id}").SingleAsync(ct);
}
