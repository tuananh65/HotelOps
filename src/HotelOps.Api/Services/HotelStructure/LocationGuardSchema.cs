using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

internal static class LocationGuardSchema
{
    internal static Task<bool> ReadyAsync(HotelOpsDbContext db, CancellationToken ct) => db.Database.SqlQuery<bool>($"""
        SELECT CONVERT(bit,CASE WHEN OBJECT_ID('dbo.LocationEligibleV1','FN') IS NOT NULL
          AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.LocationGuards.V1'
            AND CONVERT(nvarchar(10),value)='1')
          AND (SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND is_instead_of_trigger=0
            AND name IN('TR_location_areas_v1','TR_location_rooms_v1','TR_location_assets_v1',
              'TR_location_operational_tasks_v1','TR_location_incidents_v1','TR_location_incident_affected_rooms_v1',
              'TR_location_preventive_maintenance_schedules_v1','TR_location_preventive_maintenance_runs_v1',
              'TR_location_maintenance_works_v1'))=9 THEN 1 ELSE 0 END) AS Value
        """).SingleAsync(ct);
}
