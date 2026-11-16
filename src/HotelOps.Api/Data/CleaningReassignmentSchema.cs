using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

internal static class CleaningReassignmentSchema
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
        => await CleaningStartSchema.InstalledAsync(db, ct)
        && await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.CleaningReassignment.V1' AND CONVERT(nvarchar(10),value)='1')
            AND OBJECT_ID('dbo.fs013_reassign_with_evidence','P') IS NOT NULL
            AND OBJECT_ID('dbo.fs013_verify_reassignment','P') IS NOT NULL
            AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_cleaning_reassignment_immutable') AND is_disabled=0)
            AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN
              (OBJECT_ID('dbo.fs013_reassignment_operations'),OBJECT_ID('dbo.fs013_reassignment_intents'))
              AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=8
            AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.fs013_reassignment_intents')
              AND name='UQ_fs013_reassignment_recipient' AND is_unique=1 AND is_disabled=0)
            AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_fs013_reassignment_delivery' AND is_disabled=0 AND is_not_trusted=0)
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;
}
