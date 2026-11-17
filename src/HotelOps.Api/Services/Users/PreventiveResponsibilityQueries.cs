using HotelOps.Api.Data;
using HotelOps.Api.Services.PreventiveMaintenance;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

internal sealed class PreventiveResponsibilityQueries(HotelOpsDbContext db)
{
    private sealed class Row
    {
        public long RunId {get;set;}
        public long BranchId {get;set;}
        public string State {get;set;}="";
        public bool TargetValid {get;set;}
        public DateTime? PerformedAt {get;set;}
        public long? PerformedBy {get;set;}
        public long? WorkId {get;set;}
        public int OpenIntervals {get;set;}
    }
    internal async Task<Dev1ResponsibilityReview> ReadAsync(long target,CancellationToken ct, IReadOnlyList<RevokeGrant>? before=null, IReadOnlyList<RevokeGrant>? after=null, DateTime? now=null)
    {
        if(db.Database.CurrentTransaction==null)throw new InvalidOperationException("User responsibility review requires the owning transaction.");
        var blockers=new SortedSet<string>(StringComparer.Ordinal);
        var unresolved=new SortedSet<string>(StringComparer.Ordinal);
        if(!await PreventiveOccurrenceGeneration.InstalledAsync(db,ct))
            return new([], ["PREVENTIVE_RESPONSIBILITY_STORAGE_UNAVAILABLE"]);
        var rows=await db.Database.SqlQuery<Row>($"""
            SELECT d.run_id AS RunId,d.branch_id AS BranchId,d.workflow_status AS State,r.performed_at AS PerformedAt,r.performed_by AS PerformedBy,
              r.maintenance_work_id AS WorkId,
              (SELECT COUNT(*) FROM dbo.work_intervals i WITH(HOLDLOCK) WHERE i.work_item_kind='OCCURRENCE'
                AND i.work_item_id=d.run_id AND i.closed_at IS NULL) AS OpenIntervals,
              CONVERT(bit,CASE WHEN
                (s.asset_id IS NOT NULL AND s.room_id IS NULL AND s.area_id IS NULL
                  AND EXISTS(SELECT 1 FROM dbo.assets t WHERE t.asset_id=s.asset_id AND t.branch_id=d.branch_id))
                OR (s.room_id IS NOT NULL AND s.asset_id IS NULL AND s.area_id IS NULL
                  AND EXISTS(SELECT 1 FROM dbo.rooms t WHERE t.room_id=s.room_id AND t.branch_id=d.branch_id))
                OR (s.area_id IS NOT NULL AND s.asset_id IS NULL AND s.room_id IS NULL
                  AND EXISTS(SELECT 1 FROM dbo.areas t WHERE t.area_id=s.area_id AND t.branch_id=d.branch_id))
              THEN 1 ELSE 0 END) AS TargetValid
            FROM dbo.preventive_occurrence_details d WITH(HOLDLOCK)
            JOIN dbo.preventive_maintenance_runs r WITH(HOLDLOCK) ON r.run_id=d.run_id
            JOIN dbo.preventive_maintenance_schedules s WITH(HOLDLOCK) ON s.schedule_id=r.schedule_id
            WHERE d.assignee_id={target}
            """).ToArrayAsync(ct);
        foreach(var row in rows)
        {
            var fact=$"PREVENTIVE_OCCURRENCE:{row.RunId}";
            if(row.State is "ASSIGNED" or "IN_PROGRESS" or "ON_HOLD")
            {
                if(before==null && after==null)blockers.Add(fact);
                else
                {
                    var impact=before==null || after==null || now==null ? RevokeCapabilityImpact.Unknown
                        : RevokeRoleGrantImpact.PreventiveExecutor(before,after,row.BranchId,now.Value);
                    if(impact==RevokeCapabilityImpact.Lost)blockers.Add(fact);
                    if(impact==RevokeCapabilityImpact.Unknown)unresolved.Add(fact+"_AUTHORITY_UNRESOLVED");
                }
            }
            if(!row.TargetValid || row.WorkId!=null || row.State is not ("ASSIGNED" or "IN_PROGRESS" or "ON_HOLD" or "COMPLETED" or "CANCELLED")
                || (row.State=="COMPLETED" ? row.PerformedAt==null || row.PerformedBy!=target : row.PerformedAt!=null || row.PerformedBy!=null)
                || row.OpenIntervals!=(row.State=="IN_PROGRESS"?1:0))unresolved.Add(fact+"_INCONSISTENT");
        }
        return new(blockers.ToArray(),unresolved.ToArray());
    }
}
