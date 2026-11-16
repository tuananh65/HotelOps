using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

internal static class CleaningAssignmentSchema
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct)
        =>await HousekeepingSchema.InstalledAsync(db,ct)&&await db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningAssignment.V1' AND CONVERT(nvarchar(20),value)='1')
          AND OBJECT_ID('dbo.fs009_verify_assignment_guard','P') IS NOT NULL
          AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_fs009_publication_immutable','TR_fs009_operation_immutable','TR_fs009_publication_source') AND is_disabled=0)=3
          AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.notification_operation_reservations'),OBJECT_ID('dbo.notification_publications'),OBJECT_ID('dbo.notification_delivery_intents')) AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=6
          AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_publications') AND name='UX_fs009_publication_operation' AND is_unique=1 AND is_disabled=0)
          AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_delivery_intents') AND name='UQ_delivery_publication_recipient_v1' AND is_unique=1 AND is_disabled=0)
          AND COL_LENGTH('dbo.notification_operation_reservations','fingerprint')=32
          AND COL_LENGTH('dbo.notification_publications','source_revision')=8
          AND COL_LENGTH('dbo.notification_delivery_intents','row_version')=8
          AND (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN('CK_notification_result_v1','CK_delivery_inbox_v1','CK_delivery_claim_v1') AND is_disabled=0 AND is_not_trusted=0)=3
          THEN 1 ELSE 0 END AS Value
        """).SingleAsync(ct)==1;
}
