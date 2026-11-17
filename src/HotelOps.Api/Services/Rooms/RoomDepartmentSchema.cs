using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Rooms;
internal static class RoomDepartmentSchema
{
    internal static Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) => db.Database.SqlQueryRaw<bool>("""
        SELECT CONVERT(bit,CASE WHEN COL_LENGTH('dbo.room_department_mappings','row_version')=8
        AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_room_department_scope_v1') AND is_disabled=0)
        AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.room_department_mappings')
          AND name IN('FK_room_department_room','FK_room_department_department','FK_room_department_audit')
          AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=3
        THEN 1 ELSE 0 END) AS Value
        """).SingleAsync(ct);
}
