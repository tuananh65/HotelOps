using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

internal static class IncidentCurrentAssignment
{
    internal sealed record Row(long IncidentId,long? AssigneeId,string? AssigneeName,long? DepartmentId,string? DepartmentName);
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
            .Select(w=>new Row(w.IncidentId!.Value,w.AssigneeId,w.Assignee==null?null:w.Assignee.FullName,w.DepartmentId,w.Department==null?null:w.Department.DepartmentName));
    }
}
