using HotelOps.Api.Data;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.PreventiveMaintenance;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class PreventiveAlertRow
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long? AssigneeId { get; set; }
    public string State { get; set; } = "";
    public DateTime DueAt { get; set; }
}

internal static class PreventiveAlertSource
{
    internal static IQueryable<PreventiveAlertRow> Query(HotelOpsDbContext db)=>db.Database.SqlQuery<PreventiveAlertRow>($"""
        SELECT d.run_id AS Id,d.branch_id AS BranchId,d.assignee_id AS AssigneeId,d.workflow_status AS State,r.planned_at AS DueAt
        FROM dbo.preventive_occurrence_details d
        JOIN dbo.preventive_maintenance_runs r ON r.run_id=d.run_id
        JOIN dbo.preventive_maintenance_schedules s ON s.schedule_id=r.schedule_id
        JOIN dbo.audit_logs a ON a.audit_id=d.generation_audit_id AND a.entity_type='preventive_maintenance_runs'
          AND a.entity_id=CONVERT(varchar(30),d.run_id) AND a.action='CREATE' AND a.occurred_at=d.generated_at AND a.branch_id=d.branch_id
        WHERE ((s.asset_id IS NOT NULL AND s.room_id IS NULL AND s.area_id IS NULL AND EXISTS(SELECT 1 FROM dbo.assets x WHERE x.asset_id=s.asset_id AND x.branch_id=d.branch_id))
          OR (s.room_id IS NOT NULL AND s.asset_id IS NULL AND s.area_id IS NULL AND EXISTS(SELECT 1 FROM dbo.rooms x WHERE x.room_id=s.room_id AND x.branch_id=d.branch_id))
          OR (s.area_id IS NOT NULL AND s.asset_id IS NULL AND s.room_id IS NULL AND EXISTS(SELECT 1 FROM dbo.areas x WHERE x.area_id=s.area_id AND x.branch_id=d.branch_id)))
        """);

    internal static async Task<WorkAlertCondition> ConditionAsync(HotelOpsDbContext db,WorkAlertSourceRow episode,DateTime now,decimal threshold,CancellationToken ct)
    {
        if(!await PreventiveOccurrenceGeneration.InstalledAsync(db,ct))return WorkAlertCondition.Unknown;
        var row=await Query(db).SingleOrDefaultAsync(r=>r.Id==episode.SourceId&&r.BranchId==episode.BranchId,ct);
        if(row==null||row.State is not ("PENDING_ASSIGNMENT" or "ASSIGNED" or "IN_PROGRESS" or "ON_HOLD" or "COMPLETED" or "CANCELLED"))return WorkAlertCondition.Unknown;
        // The existing generation snapshot stores scheduled due, not a start.
        // Do not turn generated_at or planned_at into an invented allowed start.
        return WorkAlertWindow.Evaluate(row.State is "COMPLETED" or "CANCELLED",row.DueAt,null,now,threshold);
    }

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db,long user,WorkAlertSourceRow episode,CancellationToken ct)
    {
        if(!await PreventiveOccurrenceGeneration.InstalledAsync(db,ct))return false;
        var row=await Query(db).SingleOrDefaultAsync(r=>r.Id==episode.SourceId&&r.BranchId==episode.BranchId,ct);
        if(row==null)return false;
        var scope=await MaintenanceReadAccess.ReadAsync(db,user,episode.BranchId,ct);
        return scope.Lead||scope.Staff&&row.AssigneeId==user;
    }
}
