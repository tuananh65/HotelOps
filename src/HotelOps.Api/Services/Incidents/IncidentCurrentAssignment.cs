using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

internal static class IncidentCurrentAssignment
{
    // Historical assignees retain their work receipts, not authority over a reopened incident.
    internal static IQueryable<Incident> AssignedTo(IQueryable<Incident> query,long user,bool activeOnly)
        => query.Where(i=>i.MaintenanceWorks.Any(w=>w.AssigneeId==user && !w.PreventiveMaintenanceRuns.Any()
            && (w.Department==null || w.Department.BranchId==i.BranchId)
            && !i.IncidentEvents.Any(e=>e.EventType=="REOPENED" && e.OccurredAt>w.CreatedAt)
            && i.MaintenanceWorks.Count(other=>!other.PreventiveMaintenanceRuns.Any()
                && !i.IncidentEvents.Any(e=>e.EventType=="REOPENED" && e.OccurredAt>other.CreatedAt))==1
            && (!activeOnly || w.Status=="PENDING" || w.Status=="IN_PROGRESS" || w.Status=="ON_HOLD")));

    internal sealed class Row
    {
        public long IncidentId { get; init; }
        public long? AssigneeId { get; init; }
        public string? AssigneeName { get; init; }
        public long? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
    }
    internal static IQueryable<Row> Query(HotelOpsDbContext db)
    {
        var current=db.MaintenanceWorks.AsNoTracking().Where(w=>w.IncidentId!=null && !w.PreventiveMaintenanceRuns.Any()
            && !db.IncidentEvents.Any(e=>e.IncidentId==w.IncidentId && e.EventType=="REOPENED" && e.OccurredAt>w.CreatedAt));
        return current.Where(w=>current.Count(other=>other.IncidentId==w.IncidentId)==1
            && (w.DepartmentId==null || w.Department!.BranchId==w.Incident!.BranchId)
            && ((w.Status=="PENDING" && w.Incident!.Status=="OPEN")
                || ((w.Status=="IN_PROGRESS" || w.Status=="ON_HOLD") && w.Incident!.Status=="IN_PROGRESS")
                || (w.Status=="COMPLETED" && w.Incident!.Status=="RESOLVED")
                || (w.Status=="CLOSED" && w.Incident!.Status=="CLOSED")
                || (w.Status=="CANCELLED" && w.Incident!.Status=="CANCELLED")))
            .Select(w=>new Row { IncidentId=w.IncidentId!.Value, AssigneeId=w.AssigneeId,
                AssigneeName=w.Assignee==null?null:w.Assignee.FullName, DepartmentId=w.DepartmentId,
                DepartmentName=w.Department==null?null:w.Department.DepartmentName });
    }
}
