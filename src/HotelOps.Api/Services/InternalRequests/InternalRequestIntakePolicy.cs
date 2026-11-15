using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

// Explicit reviewed catalog classification. Never infer material needs from names/codes.
internal sealed class InternalRequestIntakeType
{
    public long TypeId { get; set; }
    public bool MaterialNeed { get; set; }
}
internal static class InternalRequestIntakePolicy
{
    internal static IQueryable<InternalRequestIntakeType> Types(HotelOpsDbContext db)=>db.Database.SqlQuery<InternalRequestIntakeType>($"SELECT request_type_id AS TypeId,is_material_need AS MaterialNeed FROM dbo.internal_request_type_policy");
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct)=>
        await db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestIntake.V1' AND CONVERT(nvarchar(20),value)='1')
          AND COL_LENGTH('dbo.internal_request_type_policy','is_material_need')=1
          AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN('FK_ir_intake_type','FK_ir_intake_event','FK_ir_intake_recipient','FK_ir_intake_department','FK_ir_intake_notification')
            AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=5
          AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_type_policy') AND name='PK_internal_request_type_policy' AND is_primary_key=1 AND is_disabled=0)
          AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_intake_outbox') AND name='PK_internal_request_intake_outbox' AND is_primary_key=1 AND is_disabled=0)
          AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.attachments')
            AND name='CK_attachments_entity_type' AND is_disabled=0 AND is_not_trusted=0 AND definition LIKE '%''INTERNAL_REQUEST_INTAKE''%')
          THEN 1 ELSE 0 END AS Value
        """).SingleAsync(ct)==1;
}
