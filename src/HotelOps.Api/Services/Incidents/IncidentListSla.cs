using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

internal static class IncidentListSla
{
    internal sealed class Row
    {
        public long IncidentId {get;set;}
        public DateTime? ResponseDeadlineAt {get;set;}
        public DateTime? ResolutionDeadlineAt {get;set;}
    }
    internal static Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct)=>db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
          AND name=N'HotelOps.IncidentSla.V1' AND CONVERT(nvarchar(20),value)=N'1')
          AND (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incidents')
            AND name IN('response_deadline_at','resolution_deadline_at') AND system_type_id=42 AND scale=7 AND is_nullable=1)=2
        THEN 1 ELSE 0 END AS Value
        """).AnyAsync(v=>v==1,ct);
    // Matches IncidentSlaRead.Normalize. No planned maintenance dates or invented targets.
    internal static IQueryable<Row> Query(HotelOpsDbContext db)=>db.Database.SqlQueryRaw<Row>("""
        SELECT incident_id AS IncidentId,
          CASE WHEN response_deadline_at<discovered_at OR resolution_deadline_at<discovered_at THEN NULL ELSE response_deadline_at END AS ResponseDeadlineAt,
          CASE WHEN response_deadline_at<discovered_at OR resolution_deadline_at<discovered_at THEN NULL ELSE resolution_deadline_at END AS ResolutionDeadlineAt
        FROM dbo.incidents
        """);
}
