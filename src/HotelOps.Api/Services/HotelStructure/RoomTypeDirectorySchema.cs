using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

// Historical UC017 read-only compatibility gate, without shared model changes.
internal static class RoomTypeDirectorySchema
{
    public static async Task<int> ReadAsync(HotelOpsDbContext db, CancellationToken ct)
    {
        var state=await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN
             COL_LENGTH(N'dbo.room_types',N'branch_id') IS NULL AND COL_LENGTH(N'dbo.room_types',N'standard_cleaning_minutes') IS NULL
             AND COL_LENGTH(N'dbo.room_types',N'default_checklist_id') IS NULL
             AND NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Contract') THEN 0
            WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Contract' AND CONVERT(nvarchar(128),value)=N'branch-v1')
             AND (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.room_types') AND is_nullable=1 AND is_computed=0
              AND ((name IN(N'branch_id',N'default_checklist_id') AND system_type_id=127 AND max_length=8)
               OR (name=N'standard_cleaning_minutes' AND system_type_id=56 AND max_length=4)))=3
             AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.room_types') AND name IN(N'FK_room_types_branch_v1',N'FK_room_types_default_checklist_v1') AND is_disabled=0 AND is_not_trusted=0)=2
             AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.room_types') AND name=N'CK_room_types_uc017_shape' AND is_disabled=0 AND is_not_trusted=0)
             AND (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.room_types') AND name IN(N'UX_room_types_branch_code',N'UX_room_types_legacy_code') AND is_unique=1 AND is_disabled=0 AND has_filter=1)=2
             AND (NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Deployment')
              OR (EXISTS(SELECT 1 FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name=N'HotelOps.UC017.Deployment' AND CONVERT(nvarchar(128),value)=N'Task69-v1')
               AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN(N'TR_rooms_uc017_type_branch',N'TR_room_types_uc017_branch_immutable') AND is_disabled=0
                AND parent_id IN(OBJECT_ID(N'dbo.rooms'),OBJECT_ID(N'dbo.room_types')))=2)) THEN 1 ELSE 2 END AS [Value]
            """).SingleAsync(ct);
        return state;
    }
}
